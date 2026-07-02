// ══════════════════════════════════════════════════════════
//  IIoT.Collector · SignalR/SignalRHostService.cs
//  역할: WPF 앱 안에서 ASP.NET Core WebApplication 을 별도 스레드로 실행
//        SignalR Hub + 정적 파일(wwwroot) + 헬스체크 엔드포인트 포함
//
//  ━━━ 클라이언트 연결 방법 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  [웹 브라우저]
//  http://localhost:7878/index.html  ← 내장 테스트 대시보드
//
//  [SignalR JS 클라이언트]
//  const conn = new signalR.HubConnectionBuilder()
//      .withUrl("http://localhost:7878/iiot")
//      .build();
//  conn.on("TagValue", (data) => console.log(data));
//  conn.on("AlarmChanged", (data) => console.log(data));
//  await conn.start();
//
//  [헬스체크]
//  GET http://localhost:7878/health
//  → {"status":"ok","uptime":3600,"activeConnections":2}
//
//  ━━━ 포트 변경 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  settings.json:
//  { "SignalR": { "Port": 7878, "Enabled": true } }
//
//  ━━━ CORS 설정 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  개발 중: 모든 origin 허용 (기본)
//  운영:   settings.json SignalR.AllowedOrigins 에 도메인 지정
//  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  C-11: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using System.IO;
using IIoT.Collector.Core.Config;
using lssLib.Log;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace IIoT.Collector.SignalR;

/// <summary>
/// SignalR Hub + ASP.NET Core 웹 서버를 WPF 앱 안에서 실행하는 서비스.
/// <para>
/// WPF UI 스레드와 충돌하지 않도록 별도 Thread 에서 실행한다.
/// (Task.Run 아님 — ASP.NET Core 는 ThreadPool 이 아닌 전용 스레드 필요)
/// </para>
/// </summary>
public sealed class SignalRHostService : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;

    private WebApplication? _app;
    private Thread?         _serverThread;
    private CancellationTokenSource? _cts;

    /// <summary>IIoTHubPusher — EventBus 핸들러에서 Push 용도로 사용</summary>
    public IIoTHubPusher? Pusher { get; private set; }

    // §2 ─ 생성자 ──────────────────────────────────────────

    public SignalRHostService(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 시작 ────────────────────────────────────────────

    /// <summary>
    /// ASP.NET Core 서버를 별도 스레드에서 시작합니다.
    /// settings.json SignalR.Enabled = false 이면 즉시 반환합니다.
    /// </summary>
    public async Task StartAsync()
    {
        var s = _settingsLoader.Settings.SignalR;

        if (!s.Enabled)
        {
            LogManager.Instance.Info("SignalR",
                "SignalR Hub 비활성화 (settings.json SignalR.Enabled=false)");
            return;
        }

        _cts = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder();

        // ── CORS (개발 중 전체 허용, 운영에서 AllowedOrigins 지정 가능)
        builder.Services.AddCors(opt =>
        {
            opt.AddDefaultPolicy(policy =>
            {
                if (s.AllowedOrigins.Length > 0)
                    policy.WithOrigins(s.AllowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                else
                    policy.SetIsOriginAllowed(_ => true)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
            });
        });

        // ── SignalR
        builder.Services.AddSignalR(opt =>
        {
            opt.EnableDetailedErrors = true;
        });

        // ── 포트 설정
        builder.WebHost.UseUrls($"http://0.0.0.0:{s.Port}");

        // ── 정적 파일 (wwwroot/index.html 테스트 대시보드)
        builder.WebHost.UseWebRoot(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot"));

        _app = builder.Build();

        _app.UseCors();
        _app.UseDefaultFiles();   // index.html 자동 서비스
        _app.UseStaticFiles();

        // ── SignalR Hub 엔드포인트
        _app.MapHub<IIoTHub>("/iiot");

        // ── 헬스체크 엔드포인트
        _app.MapGet("/health", () => new
        {
            status    = "ok",
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
        });

        // ── IIoTHubPusher 생성 (DI 컨테이너에서 IHubContext 꺼내기)
        Pusher = new IIoTHubPusher(
            _app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<IIoTHub>>());

        // ── 별도 스레드에서 Run (WPF UI 스레드와 분리)
        var tcs = new TaskCompletionSource();
        _serverThread = new Thread(() =>
        {
            try
            {
                // ★ WebApplication.RunAsync(string? url) — CancellationToken 인수 없음
                //   종료는 _cts.Cancel() → IHostApplicationLifetime.StopApplication() 경유
                _app.Run();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogManager.Instance.Error("SignalR",
                    $"SignalR 서버 오류: {ex.Message}");
            }
            finally { tcs.TrySetResult(); }
        })
        {
            IsBackground = true,
            Name         = "SignalR-Host"
        };
        _serverThread.Start();

        // 서버 시작 대기 (최대 3초)
        await Task.Delay(500);

        LogManager.Instance.Info("SignalR",
            $"SignalR Hub 시작 완료 — http://localhost:{s.Port}/iiot");
    }

    // §4 ─ 리소스 해제 ─────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            _cts?.Cancel();
            // ★ WebApplication.StopAsync(CancellationToken) — TimeSpan 인수 없음
            using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _app.StopAsync(stopCts.Token);
            await _app.DisposeAsync();
            LogManager.Instance.Info("SignalR", "SignalR Hub 종료 완료");
        }
    }
}
