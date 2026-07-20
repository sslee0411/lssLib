// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Web/HmiWebHostService.cs
//  역할: 웹 브라우저 표시 확장(HM-11) — 자체 Kestrel + SignalR Hub + wwwroot 호스팅.
//        WPF 로컬 화면([레이아웃 편집] 탭, LayoutCanvasViewModel.Nodes)의 현재
//        활성 화면을 그대로 미러링해 웹 브라우저에서도 볼 수 있게 한다.
//        (IIoT.Collector SignalR/SignalRHostService.cs(C-11) 패턴 그대로 준용 —
//         WPF(WinExe)+ASP.NET Core 공존: 별도 non-pool Thread 에서 WebApplication
//         실행, FrameworkReference Microsoft.AspNetCore.App 필요)
//
//  ★ 범위 결정(1차, HM-11): 읽기 전용 표시만 구현. 웹은 WPF 의 "현재 활성
//    화면 1개"만 미러링하며, 웹에서 별도로 화면(페이지)을 선택하는 기능은
//    없다(항상 WPF 쪽 ActivePage 를 그대로 따라간다).
//
//  ★ HM-21: ACK/ForceWrite 를 웹에서도 지원한다. HmiWebHub 가 실제 라우팅
//    로직을 갖고 있으며(CollectorConnectionManager.AcknowledgeAlarmAsync/
//    ForceWriteAsync 로 위임), 이 서비스는 그 Hub 가 필요로 하는
//    CollectorConnectionManager/LayoutCanvasViewModel 인스턴스를 ASP.NET
//    Core 자체 DI 컨테이너(builder.Services)에 등록해 주는 역할만 한다 —
//    WebApplication.CreateBuilder() 는 WPF 앱의 DI 컨테이너(App.xaml.cs
//    _ConfigureServices)와 완전히 별개의 컨테이너이므로, Hub 생성자가
//    참조하려면 "이미 만들어진 그 싱글턴 인스턴스"를 여기서 그대로
//    다시 등록해 공유해야 한다.
//
//  ★ 갱신 전략: 노드 구조(추가/삭제/화면 전환) 또는 실시간 상태(값/알람)가
//    바뀔 때마다 즉시 Push 하지 않고 "dirty 플래그 + 500ms 주기 브로드캐스트"로
//    코일레싱한다 — 여러 카드가 동시에 갱신되는 폴링 사이클마다 직렬화·Push 가
//    반복되는 것을 방지한다(Collector Tag 폴링 주기 대비 충분히 빠른 반영
//    속도이면서도 트래픽은 낮춘다).
//
//  ★ 스레드 안전성: LayoutCanvasViewModel.Nodes(ObservableCollection)는 WPF UI
//    스레드 소유다. REST 핸들러/브로드캐스트 루프는 ASP.NET Core 자체 스레드
//    (비 UI 스레드)에서 실행되므로, 스냅샷 생성은 반드시 Dispatcher.InvokeAsync
//    로 UI 스레드에 마샬링한다(프로젝트 규칙 "UI 마샬링" 준수 — 단, OnExit
//    교착을 피하려는 취지의 ".Invoke 금지" 규칙은 블로킹 .Invoke() 에어 대한
//    것이므로, 여기서는 비-블로킹 await 가능한 InvokeAsync() 를 사용한다).
//    HmiWebHub 의 ACK/ForceWrite 메서드도 동일 원칙을 따른다.
//
//  HM-11: 신규
//  HM-21: CollectorConnectionManager 의존성 추가 + builder.Services 등록 추가.
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Config;
using IIoT.HMI.Core.Connection;
using IIoT.HMI.Core.Layout;
using IIoT.HMI.ViewModels;
using lssLib.Log;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace IIoT.HMI.Core.Web;

/// <summary>
/// HM-11: 웹 브라우저 표시용 자체 Kestrel + SignalR Hub + wwwroot 호스팅(DI 싱글턴).
/// HM-21: ACK/ForceWrite 라우팅에 필요한 CollectorConnectionManager 공유 추가.
/// </summary>
public sealed class HmiWebHostService : IAsyncDisposable
{
    private readonly HmiSettingsLoader        _settingsLoader;
    private readonly LayoutCanvasViewModel    _canvasVm;
    private readonly CollectorConnectionManager _connectionManager;

    private WebApplication?          _app;
    private Thread?                  _thread;
    private IHubContext<HmiWebHub>?  _hub;
    private CancellationTokenSource? _broadcastCts;

    private volatile bool _dirty;

    public HmiWebHostService(
        HmiSettingsLoader settingsLoader,
        LayoutCanvasViewModel canvasVm,
        CollectorConnectionManager connectionManager)
    {
        _settingsLoader    = settingsLoader;
        _canvasVm          = canvasVm;
        _connectionManager = connectionManager;
    }

    // §1 ─ 시작 ────────────────────────────────────────────────

    public async Task StartAsync()
    {
        // ★ 시작 순서 방어: hmi.json 로드는 [Collector 관리] 탭 Loaded 시점(HM-01)에도
        //   이루어지지만, 이 서비스가 그보다 먼저 시작될 수 있으므로 여기서도 직접
        //   로드해 최신 설정을 보장한다(LoadAsync 는 여러 번 호출해도 안전 — 매번
        //   파일을 다시 읽어 Settings 를 갱신할 뿐).
        await _settingsLoader.LoadAsync();

        var web = _settingsLoader.Settings.Web;
        if (!web.Enabled)
        {
            LogManager.Instance.Info("HmiWebHostService", "웹 표시 기능 비활성화(Web.Enabled=false) — 시작하지 않음");
            return;
        }

        var builder = WebApplication.CreateBuilder();

        builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
            p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        builder.Services.AddSignalR(opt => opt.EnableDetailedErrors = true);

        // ★ HM-21: HmiWebHub(이 컨테이너가 직접 생성) 생성자가 요구하는 두 인스턴스를
        //   "이미 WPF 쪽에서 만들어진 그 싱글턴"으로 그대로 등록한다(새로 만들지 않음).
        //   ASP.NET Core 의 DI 컨테이너는 WPF 앱의 DI 컨테이너와 별개이므로 이 등록이
        //   없으면 HmiWebHub 생성 시 예외가 발생한다.
        builder.Services.AddSingleton(_connectionManager);
        builder.Services.AddSingleton(_canvasVm);

        builder.WebHost.UseUrls($"http://0.0.0.0:{web.Port}");
        builder.WebHost.UseWebRoot(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot"));

        _app = builder.Build();

        _app.UseCors();
        _app.UseDefaultFiles();
        _app.UseStaticFiles();

        _app.MapHub<HmiWebHub>("/hmi-hub");

        _app.MapGet("/health", () => new { status = "ok", timestamp = DateTime.Now.ToString("O") });

        // ★ 초기 페인트용 — 웹 페이지가 최초 접속 시 1회 호출해 현재 활성 화면의
        //   전체 노드 스냅샷을 받는다(이후 갱신은 전부 "NodesChanged" Push 로만 반영).
        _app.MapGet("/api/layout", async () => Results.Json(
            await _BuildSnapshotAsync(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        _hub = _app.Services.GetRequiredService<IHubContext<HmiWebHub>>();

        _WireRelay();

        // ★ Collector C-11 과 동일 이유: ASP.NET Core 는 실제 스레드가 필요하며
        //   (Task.Run 이 아님), WPF UI 스레드와 분리된 별도 non-pool Thread 에서
        //   블로킹 _app.Run() 을 호출한다.
        _thread = new Thread(() => _app.Run())
        {
            IsBackground = true,
            Name = "HMI-WebHost"
        };
        _thread.Start();

        _broadcastCts = new CancellationTokenSource();
        _ = _BroadcastLoopAsync(_broadcastCts.Token);

        await Task.Delay(500);

        LogManager.Instance.Info("HmiWebHostService", $"웹 표시 서버 시작 — http://localhost:{web.Port}");
    }

    // §2 ─ 노드 변경 감지 → dirty 표시 ──────────────────────────

    /// <summary>
    /// Nodes 컬렉션의 구조 변경(추가/삭제/화면 전환 시 Clear+재구성) 및 개별
    /// 노드의 실시간 상태 변경(PropertyChanged) 을 모두 dirty 플래그로만 기록한다
    /// (Monitor MonitorHostService._WireRelay() 와 동일한 relay 패턴).
    /// </summary>
    private void _WireRelay()
    {
        _canvasVm.Nodes.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (AbstractLayoutNode n in e.NewItems)
                    n.PropertyChanged += _OnNodePropertyChanged;

            if (e.OldItems is not null)
                foreach (AbstractLayoutNode n in e.OldItems)
                    n.PropertyChanged -= _OnNodePropertyChanged;

            _dirty = true;
        };

        foreach (var n in _canvasVm.Nodes)
            n.PropertyChanged += _OnNodePropertyChanged;
    }

    private void _OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;

    // §3 ─ 주기 브로드캐스트 ────────────────────────────────────

    private async Task _BroadcastLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, ct);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (!_dirty || _hub is null) continue;

            _dirty = false;

            try
            {
                var snapshot = await _BuildSnapshotAsync();
                await _hub.Clients.All.SendAsync("NodesChanged", snapshot, ct);
            }
            catch (Exception ex)
            {
                LogManager.Instance.Warn("HmiWebHostService", $"NodesChanged Push 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 현재 Nodes 스냅샷을 WebNodeDto 목록으로 변환한다. Dispatcher.InvokeAsync 로
    /// UI 스레드에서 실행해 ObservableCollection 스레드 안전성을 보장한다.
    /// </summary>
    private async Task<List<WebNodeDto>> _BuildSnapshotAsync()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return new List<WebNodeDto>();

        return await dispatcher.InvokeAsync(() => _canvasVm.Nodes.Select(n => new WebNodeDto(
            n.NodeId, n.NodeType, n.Label, n.IconGlyph, n.CategoryColor,
            n.X, n.Y, n.ZIndex, n.IsBound,
            n.ValueText, n.ValueQuality, n.EngValue,
            n.HasActiveAlarm, n.AlarmLevel, n.AlarmStatusText, n.AlarmMessage, n.AlarmTimeText,
            // ★ HM-21: ACK/ForceWrite 요청 라우팅을 위해 추가된 식별 필드
            n.AlarmKey, n.BoundCollectorId, n.BoundPlcId, n.BoundTagId, n.BoundTagName
        )).ToList());
    }

    // §4 ─ 종료 ────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _broadcastCts?.Cancel();

        if (_app is not null)
        {
            var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await _app.StopAsync(stopCts.Token); }
            catch (Exception ex) { LogManager.Instance.Warn("HmiWebHostService", $"웹 서버 종료 중 예외(무시): {ex.Message}"); }
            await _app.DisposeAsync();
        }
    }
}
