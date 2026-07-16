// ══════════════════════════════════════════════════════════════════════
//  lssLib.SignalR · Hosting/SignalRHostService.cs
//  역할: SignalR 허브 호스트 래퍼 — 최소 구성의 Kestrel 웹 서버를 띄우고
//        지정한 Hub 를 매핑한다. (IIoT.Collector SignalRHostService /
//        IIoT.Monitor MonitorHostService 의 공통 부분을 일반화)
//  사용:
//    var host = new SignalRHostService<BroadcastHub>(new SignalRHostConfig(7890));
//    await host.StartAsync();
//    ...
//    await host.DisposeAsync();   // ★ 앱 종료 시 반드시 호출
//                                 //   (WPF 라면 5초 타임아웃 대기 권장 —
//                                 //    IIoT.Monitor 버그 #11 교훈)
//  주의:
//    - using Microsoft.AspNetCore.Builder/Hosting 필요 (버그 #13 교훈)
//    - 콘솔/파일 로깅은 기본 제거 — 호스트 앱의 로거와 중복 출력 방지
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace lssLib.SignalR;

/// <summary>
/// SignalR 허브 호스트 (제네릭 — 매핑할 Hub 타입 지정).
/// <para>StartAsync() 로 기동, DisposeAsync() 로 정리한다.</para>
/// </summary>
/// <typeparam name="THub">매핑할 허브 (Microsoft.AspNetCore.SignalR.Hub 상속)</typeparam>
public sealed class SignalRHostService<THub> : IAsyncDisposable
    where THub : Hub
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly SignalRHostConfig _config;
    private WebApplication?            _app;

    /// <summary>기동 여부</summary>
    public bool IsRunning => _app is not null;

    /// <summary>허브 컨텍스트 — 서버 코드에서 클라이언트로 직접 발행할 때 사용 (기동 후 유효)</summary>
    public IHubContext<THub>? HubContext { get; private set; }

    // §2 ─ 생성자 ──────────────────────────────────────────

    public SignalRHostService(SignalRHostConfig config) => _config = config;

    // §3 ─ 공개 메서드 ──────────────────────────────────────

    /// <summary>웹 서버를 기동하고 허브를 매핑한다 (재호출 무시).</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_app is not null) return;

        var builder = WebApplication.CreateBuilder();

        // 호스트 앱 로거와의 중복 출력 방지 — 필요 시 호출부에서 재구성
        builder.Logging.ClearProviders();

        builder.WebHost.UseUrls(_config.ListenUrl);
        builder.Services.AddSignalR();

        var app = builder.Build();
        app.MapHub<THub>(_config.HubPath);

        await app.StartAsync(ct);

        _app       = app;
        HubContext = app.Services.GetRequiredService<IHubContext<THub>>();
    }

    /// <summary>서버를 정지하고 리소스를 정리한다.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;

        var app = _app;
        _app       = null;
        HubContext = null;

        await app.StopAsync();
        await app.DisposeAsync();
    }
}
