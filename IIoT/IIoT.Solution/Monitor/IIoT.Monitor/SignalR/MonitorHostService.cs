// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · SignalR/MonitorHostService.cs
//  역할: WPF 앱 안에서 ASP.NET Core WebApplication 을 별도 스레드로 실행
//        (Collector SignalRHostService C-11/C-EX-12 와 동일 패턴)
//        LiveTagAggregator/AlarmAggregator 의 실시간 값 갱신을 구독하여
//        MonitorHub 를 통해 모든 웹 클라이언트에 Relay 한다.
//
//        ★ 이 Relay 과정에서 payload 에 collectorId 를 반드시 포함시킨다 —
//          Collector 원본 payload 에는 아직 collectorId 가 없지만(C-EX-11
//          미완료), Monitor 는 "연결 출처" 기준으로 이미 collectorId 를
//          태깅해 두었으므로(MN-02/MN-03) 이를 그대로 웹 클라이언트에
//          전달하면 C-EX-11 없이도 웹 단에서 다중 Collector 구분이 가능하다.
//
//  ━━━ 클라이언트 연결 방법 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  const conn = new signalR.HubConnectionBuilder()
//      .withUrl("http://localhost:7879/monitor-hub")
//      .build();
//  conn.on("TagValue", (data) => console.log(data));
//  conn.on("AlarmChanged", (data) => console.log(data));
//  await conn.start();
//
//  GET http://localhost:7879/health
//  GET http://localhost:7879/api/snapshot   ← Collector 목록 + 상태 스냅샷
//
//  ━━━ 포트 변경 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  monitor.json: { "Web": { "Enabled": true, "Port": 7879 } }
//  MN-05: 신규
//  FIX: CS1061(UseUrls)/CS0103(Results) — using Microsoft.AspNetCore.Hosting,
//       Microsoft.AspNetCore.Http 누락 추가 (Collector SignalRHostService.cs
//       참조 시 옮겨 적으면서 빠뜨렸던 using 2개)
//  생성: 2026-07-08 / 수정: 2026-07-08 (using 누락 수정)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Models;
using lssLib.Log;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace IIoT.Monitor.SignalR;

/// <summary>
/// Monitor 자체 SignalR Hub + ASP.NET Core 웹 서버를 WPF 앱 안에서 실행하는 서비스.
/// WPF UI 스레드와 충돌하지 않도록 별도 Thread 에서 실행한다(Collector와 동일 원칙).
/// </summary>
public sealed class MonitorHostService : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly MonitorSettingsLoader _settingsLoader;
    private readonly LiveTagAggregator     _tagAggregator;
    private readonly AlarmAggregator       _alarmAggregator;

    private WebApplication? _app;
    private Thread?         _serverThread;
    private MonitorHubPusher? _pusher;

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    // §2 ─ 생성자 ──────────────────────────────────────────

    public MonitorHostService(
        MonitorSettingsLoader settingsLoader,
        LiveTagAggregator     tagAggregator,
        AlarmAggregator       alarmAggregator)
    {
        _settingsLoader  = settingsLoader;
        _tagAggregator   = tagAggregator;
        _alarmAggregator = alarmAggregator;
    }

    // §3 ─ 시작 ────────────────────────────────────────────

    /// <summary>
    /// ASP.NET Core 서버를 별도 스레드에서 시작합니다.
    /// monitor.json Web.Enabled = false 이면 즉시 반환합니다.
    /// </summary>
    public async Task StartAsync()
    {
        var web = _settingsLoader.Settings.Web;

        if (!web.Enabled)
        {
            LogManager.Instance.Info("MonitorHost", "Monitor 웹 Hub 비활성화 (monitor.json Web.Enabled=false)");
            return;
        }

        var builder = WebApplication.CreateBuilder();

        builder.Services.AddCors(opt =>
        {
            opt.AddDefaultPolicy(policy =>
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });

        builder.Services.AddSignalR(opt => opt.EnableDetailedErrors = true);

        builder.WebHost.UseUrls($"http://0.0.0.0:{web.Port}");

        _app = builder.Build();

        _app.UseCors();
        _app.MapHub<MonitorHub>("/monitor-hub");

        _app.MapGet("/health", () => new
        {
            status    = "ok",
            timestamp = DateTimeOffset.UtcNow.ToString("O")
        });

        // ── 전체 Collector 목록 + Tag/알람 집계 스냅샷 (최초 접속용)
        _app.MapGet("/api/snapshot", () =>
        {
            var snapshot = new
            {
                collectors = _settingsLoader.Settings.Collectors.Select(c => new
                {
                    id     = c.Id,
                    name   = c.Name,
                    status = c.StatusText
                }),
                tags   = _tagAggregator.Rows.Count,
                alarms = _alarmAggregator.Rows.Count(a => a.Status == "Active")
            };
            return Results.Json(snapshot, _jsonOpts);
        });

        _pusher = new MonitorHubPusher(
            _app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<MonitorHub>>());

        // ★ Aggregator 실시간 값 → 웹 클라이언트 Relay 배선
        _WireRelay();

        _serverThread = new Thread(() =>
        {
            try { _app.Run(); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogManager.Instance.Error("MonitorHost", $"Monitor 웹 서버 오류: {ex.Message}");
            }
        })
        {
            IsBackground = true,
            Name         = "Monitor-WebHost"
        };
        _serverThread.Start();

        await Task.Delay(300);

        LogManager.Instance.Info("MonitorHost",
            $"Monitor 웹 Hub 시작 완료 — http://localhost:{web.Port}/monitor-hub");
    }

    // §4 ─ Aggregator → 웹 클라이언트 Relay ─────────────────

    private void _WireRelay()
    {
        foreach (var row in _tagAggregator.Rows)
            _AttachTagRow(row);
        _tagAggregator.Rows.CollectionChanged += _OnTagRowsChanged;

        foreach (var row in _alarmAggregator.Rows)
            _AttachAlarmRow(row);
        _alarmAggregator.Rows.CollectionChanged += _OnAlarmRowsChanged;
    }

    private void _OnTagRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;
        foreach (LiveTagRow row in e.NewItems)
            _AttachTagRow(row);
    }

    private void _AttachTagRow(LiveTagRow row)
    {
        row.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(LiveTagRow.EngValue)) return;
            if (s is not LiveTagRow r) return;

            _ = _pusher?.PushTagValueAsync(new
            {
                collectorId = r.CollectorId,   // ★ Collector 원본엔 없는 필드 — Monitor가 보강
                plcId       = r.PlcId,
                tagId       = r.TagId,
                rawValue    = r.RawValue,
                engValue    = r.EngValue,
                unit        = r.Unit,
                quality     = r.Quality,
                ts          = r.UpdatedAt.ToString("O")
            });
        };
    }

    private void _OnAlarmRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;
        foreach (AlarmRow row in e.NewItems)
            _AttachAlarmRow(row);
    }

    private void _AttachAlarmRow(AlarmRow row)
    {
        row.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(AlarmRow.Status)) return;
            if (s is not AlarmRow r) return;

            _ = _pusher?.PushAlarmAsync(new
            {
                collectorId = r.CollectorId,   // ★ Collector 원본엔 없는 필드 — Monitor가 보강
                alarmKey    = r.AlarmKey,
                plcId       = r.PlcId,
                tagId       = r.TagId,
                tagName     = r.TagName,
                level       = r.Level,
                status      = r.Status,
                message     = r.Message,
                engValue    = r.EngValue,
                ts          = r.OccurredAt.ToString("O")
            });
        };
    }

    // §5 ─ 리소스 해제 ─────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _app.StopAsync(stopCts.Token);
            await _app.DisposeAsync();
            LogManager.Instance.Info("MonitorHost", "Monitor 웹 Hub 종료 완료");
        }
    }
}
