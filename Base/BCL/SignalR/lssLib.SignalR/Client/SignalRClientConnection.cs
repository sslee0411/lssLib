// ══════════════════════════════════════════════════════════════════════
//  lssLib.SignalR · Client/SignalRClientConnection.cs
//  역할: HubConnection 래퍼 — 자동 재연결·상태 이벤트·구독/호출 헬퍼.
//        (IIoT.Monitor CollectorConnection(MN-01B)의 공통 부분을 일반화)
//  사용:
//    var cli = new SignalRClientConnection(new SignalRClientConfig("localhost", 7890));
//    cli.Connected    += () => ...;
//    cli.On<string,string>(BroadcastHub.ReceiveMethod, (topic, payload) => ...);
//    await cli.StartAsync();
//    await cli.InvokeAsync("Subscribe", "topicA");
//    ...
//    await cli.DisposeAsync();   // ★ 앱 종료 시 반드시 호출
//  주의:
//    - 수신 핸들러는 백그라운드 스레드에서 호출됨 — WPF UI 갱신은
//      Dispatcher.BeginInvoke 사용 (Invoke 금지 — IIoT.Monitor 버그 #11 교훈)
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.SignalR.Client;

namespace lssLib.SignalR;

/// <summary>
/// SignalR 클라이언트 연결 래퍼 (자동 재연결 내장: 0/2/10/30초 간격).
/// </summary>
public sealed class SignalRClientConnection : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly SignalRClientConfig _config;
    private readonly HubConnection       _conn;

    /// <summary>접속 대상 허브 URL</summary>
    public string HubUrl => _config.HubUrl;

    /// <summary>현재 연결 상태</summary>
    public HubConnectionState State => _conn.State;

    /// <summary>연결 여부</summary>
    public bool IsConnected => _conn.State == HubConnectionState.Connected;

    // §2 ─ 이벤트 ──────────────────────────────────────────

    /// <summary>최초 연결 성공 시 (StartAsync 성공)</summary>
    public event Action? Connected;

    /// <summary>재연결 시도 중 (연결 끊김 감지)</summary>
    public event Action<string?>? Reconnecting;

    /// <summary>재연결 성공</summary>
    public event Action? Reconnected;

    /// <summary>연결 종료 (자동 재연결 포기 포함)</summary>
    public event Action<string?>? Closed;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public SignalRClientConnection(SignalRClientConfig config)
    {
        _config = config;

        _conn = new HubConnectionBuilder()
            .WithUrl(config.HubUrl)
            .WithAutomaticReconnect()   // 0 / 2 / 10 / 30초 간격 재시도
            .Build();

        _conn.Reconnecting += ex => { Reconnecting?.Invoke(ex?.Message); return Task.CompletedTask; };
        _conn.Reconnected  += _  => { Reconnected?.Invoke();             return Task.CompletedTask; };
        _conn.Closed       += ex => { Closed?.Invoke(ex?.Message);       return Task.CompletedTask; };
    }

    // §4 ─ 공개 메서드 ──────────────────────────────────────

    /// <summary>
    /// 허브에 접속한다. 최초 접속 실패 시 retrySec 간격으로 재시도
    /// (retryCount 회 한도, 0 = 1회만 시도).
    /// </summary>
    public async Task StartAsync(int retryCount = 0, int retrySec = 3,
                                 CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _conn.StartAsync(ct);
                Connected?.Invoke();
                return;
            }
            catch when (attempt < retryCount && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(retrySec), ct);
            }
        }
    }

    /// <summary>서버 발행 수신 등록 (해제는 반환된 IDisposable.Dispose()).</summary>
    public IDisposable On<T1>(string method, Action<T1> handler) =>
        _conn.On(method, handler);

    /// <summary>서버 발행 수신 등록 (인자 2개).</summary>
    public IDisposable On<T1, T2>(string method, Action<T1, T2> handler) =>
        _conn.On(method, handler);

    /// <summary>허브 메서드 호출 (반환값 없음).</summary>
    public Task InvokeAsync(string method, params object?[] args) =>
        _conn.InvokeCoreAsync(method, args, default);

    /// <summary>허브 메서드 호출 (반환값 수신).</summary>
    public async Task<TResult> InvokeAsync<TResult>(string method, params object?[] args) =>
        (TResult)(await _conn.InvokeCoreAsync(method, typeof(TResult), args, default))!;

    /// <summary>연결 종료 및 정리.</summary>
    public async ValueTask DisposeAsync()
    {
        try   { await _conn.StopAsync(); }
        catch { /* 이미 끊긴 경우 무시 */ }
        await _conn.DisposeAsync();
    }
}
