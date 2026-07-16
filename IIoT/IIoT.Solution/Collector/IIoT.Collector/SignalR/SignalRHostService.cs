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
//  C-EX-12: IIoTHub 가 AlarmStateManager 를 생성자로 요구하게 되어(ACK 처리),
//           ASP.NET Core 자체 DI 컨테이너(builder.Services)에도 WPF 쪽
//           AlarmStateManager 싱글턴 인스턴스를 그대로 등록해 Hub 활성화 시
//           주입되도록 함 (기존 DeviceInstanceService 클로저 재사용 원칙과
//           동일 맥락 — 새 컨테이너를 만들지 않고 기존 인스턴스를 공유).
//  C-EX-13: IIoTHub 가 ForceWriteService 도 생성자로 요구하게 되어(Tag 강제쓰기),
//           동일 원칙으로 ASP.NET Core 자체 DI 컨테이너에 ForceWriteService
//           싱글턴 인스턴스도 함께 등록. IIoT.HMI(및 웹 클라이언트)가
//           conn.invoke("ForceWrite", ...) 로 Tag 를 원격 제어할 수 있게 됨.
//  생성: 2026-06-29 / 수정: 2026-07-07 (C-EX-12) / 2026-07-16 (C-EX-13)
// ══════════════════════════════════════════════════════════

using System.IO;
using System.Text.Json;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Engine;
using lssLib.Log;
using Microsoft.AspNetCore.Http;
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
    private readonly DeviceInstanceService _deviceInstanceService;   // ★ C-EX-01-7 신규
    private readonly AlarmStateManager     _alarmStateManager;       // ★ C-EX-12 신규
    private readonly ForceWriteService     _forceWriteService;       // ★ C-EX-13 신규

    private WebApplication? _app;
    private Thread?         _serverThread;
    private CancellationTokenSource? _cts;

    /// <summary>IIoTHubPusher — EventBus 핸들러에서 Push 용도로 사용</summary>
    public IIoTHubPusher? Pusher { get; private set; }

    // §2 ─ 생성자 ──────────────────────────────────────────

    public SignalRHostService(
        CollectorSettingsLoader settingsLoader,
        DeviceInstanceService deviceInstanceService,   // ★ C-EX-01-7 신규
        AlarmStateManager alarmStateManager,            // ★ C-EX-12 신규
        ForceWriteService forceWriteService)            // ★ C-EX-13 신규
    {
        _settingsLoader = settingsLoader;
        _deviceInstanceService = deviceInstanceService;
        _alarmStateManager = alarmStateManager;
        _forceWriteService = forceWriteService;
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

        // ── ★ C-EX-12 신규: IIoTHub 생성자가 요구하는 AlarmStateManager 를
        //    ASP.NET Core 자체 DI 컨테이너에도 등록 (WPF 쪽 싱글턴 인스턴스 그대로 공유).
        //    이렇게 하지 않으면 Hub 활성화 시 "서비스를 찾을 수 없음" 예외 발생.
        builder.Services.AddSingleton(_alarmStateManager);

        // ── ★ C-EX-13 신규: IIoTHub 생성자가 요구하는 ForceWriteService 도
        //    동일한 이유로 ASP.NET Core 자체 DI 컨테이너에 등록.
        builder.Services.AddSingleton(_forceWriteService);

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

        // ── ★ C-EX-01-7 신규: DeviceInstance 전체 스냅샷 REST 엔드포인트
        //    Monitor 가 최초 접속 시 전체 트리를 한 번에 받아오는 용도.
        //    이후 증분 변경은 기존 SignalR "TagValue"/"AlarmChanged" 이벤트로 수신.
        //    ※ 클로저로 DeviceInstanceService 를 직접 캡처 (ASP.NET Core 자체 DI 미사용
        //       — WPF 쪽 DI 컨테이너의 싱글턴 인스턴스를 그대로 재사용하기 위함)
        _app.MapGet("/api/devices", () =>
        {
            var devices = _deviceInstanceService.GetAll();
            return Results.Json(devices, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
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
