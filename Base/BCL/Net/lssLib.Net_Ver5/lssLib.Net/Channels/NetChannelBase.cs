// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Channels/NetChannelBase.cs
//  역할: 내부 3개 전문 클래스를 조합하는 얇은 오케스트레이터
//
//  ┌─ 내부 구성 ─────────────────────────────────────────────────────┐
//  │  NetChannelBase (오케스트레이터)                                 │
//  │    ├─ NetConnectionManager  연결·재접속·상태 머신               │
//  │    ├─ NetDispatchPipeline   Channel[4] 우선순위 (lock 없음)      │
//  │    ├─ NetScheduler          주기 Read + Heartbeat (Pause/Resume) │
//  │    └─ NetStatistics         통신 통계 수집                       │
//  └─────────────────────────────────────────────────────────────────┘
//
//  ┌─ 데이터 흐름 ───────────────────────────────────────────────────┐
//  │  WriteAsync / RequestAsync                                       │
//  │    → IsConnected 가드 → Pipeline.EnqueueAsync                   │
//  │    → Channel[Priority] → ProcessLoopAsync → DispatchAsync       │
//  │    → 성공: 결과 전달                                            │
//  │    → 실패: ConnectionManager.HandleErrorAsync                    │
//  │             → Scheduler.Pause → Disconnect → ReconnectAsync     │
//  │             → 성공: Scheduler.Resume → 보존 Write 재투입        │
//  └─────────────────────────────────────────────────────────────────┘
//
//  ┌─ TCP Passive 연결 끊김 감지 흐름 ──────────────────────────────┐
//  │  PassiveReceiveLoopAsync 예외                                    │
//  │      → State = NetState.Error                                    │
//  │          → StateChanged 이벤트                                   │
//  │              → OnStateChanged(Error)                             │
//  │                  → _connMgr.HandleErrorAsync() ← 재접속 트리거  │
//  └─────────────────────────────────────────────────────────────────┘
//
//  v5.1 변경사항:
//    · NetDeviceConfigBase → NetDeviceConfig 로 변경
//    · _connMgr.Reconnected 이중 구독 버그 수정 (단일 구독)
//    · IsSequential(bool) → SequenceMode(int) 대응
// ══════════════════════════════════════════════════════════════════════

using System.IO;

namespace lssLib.Net;

/// <summary>
/// lssLib.Net 통신 채널 추상 베이스 (v5.1).
/// </summary>
/// <remarks>
/// <para>파생 클래스(Channels/)는 <see cref="Mode"/> 만 override 하면 됩니다.</para>
/// <para>전송 계층·프로토콜·설정은 생성자에서 주입합니다.</para>
///
/// <b>조립 예시 — Passive (TCP Push 서버):</b>
/// <code>
/// var cfg = new TcpDeviceConfig(1, "PushSensor", "192.168.1.50", 5000)
/// {
///     MaxRetries        = 0,   // 무제한 재시도
///     HeartbeatInterval = TimeSpan.FromSeconds(30)
///     // SequenceMode = 0 (Parallel) ← 기본값
/// };
///
/// await using var channel = new PassiveNetChannel(
///     cfg,
///     TcpTransport.FromConfig(cfg, enablePassiveReceive: true),  // ← 필수
///     new BinaryProtocol(stx: 0xAA),
///     autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) =>
///     Dispatcher.InvokeAsync(() => UpdateUI(id, frame));   // ⚠ WPF: Dispatcher 필수
///
/// channel.DeviceStateChanged  += (id, state) =>
///     Dispatcher.InvokeAsync(() => LblState.Content = state.ToString());
///
/// channel.DeviceErrorOccurred += (id, ex) =>
///     LogManager.Instance.Error(cfg.DeviceName, ex.Message);
///
/// await channel.StartAsync();
/// </code>
///
/// <b>조립 예시 — RequestResponse (Modbus RTU):</b>
/// <code>
/// var cfg = new SerialDeviceConfig(2, "Modbus-PLC", "COM3", 9600);
/// // cfg.SequenceMode == 1 (Sequential) ← RS-485 기본값
///
/// cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);
///
/// await using var channel = new RequestResponseChannel(
///     cfg, SerialTransport.FromConfig(cfg),
///     new RawProtocol(), autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) => ProcessModbus(id, frame);
/// await channel.StartAsync();
///
/// await channel.WriteAsync(setpointFrame, NetPriority.Write);
/// NetResult r = await channel.RequestAsync(queryFrame, TimeSpan.FromMilliseconds(500));
/// if (r.IsOk) ProcessResponse(r.Data!);
/// </code>
/// </remarks>
public abstract class NetChannelBase : IAsyncDisposable
{
    #region §1 ─ 필드

    private readonly NetDeviceConfig _cfg;
    private readonly INetTransport _transport;
    private readonly INetProtocol _protocol;

    private readonly NetStatistics _statistics;
    private readonly NetConnectionManager _connMgr;
    private readonly NetDispatchPipeline _pipeline;
    private readonly NetScheduler _scheduler;

    private CancellationTokenSource _cts = new();
    /// <summary>
    /// volatile: 여러 스레드에서 동시에 접근할 때 일관된 값을 보장합니다.
    /// Dispose 된 상태를 다른 스레드에서 즉시 인지할 수 있습니다.
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region §2 ─ 생성자

    /// <param name="config">장비 설정 (DeviceId/Name/Retry/Commands 포함)</param>
    /// <param name="transport">
    /// 전송 계층. XxxTransport.FromConfig(cfg) 패턴 권장.
    /// TCP Push 서버 연동 시 TcpTransport.FromConfig(cfg, enablePassiveReceive: true) 사용.
    /// </param>
    /// <param name="protocol">프로토콜 계층. BinaryProtocol 또는 RawProtocol.</param>
    /// <param name="autoRegister">true 이면 NetDeviceRegistry 에 자동 등록.</param>
    protected NetChannelBase(
        NetDeviceConfig config,
        INetTransport transport,
        INetProtocol protocol,
        bool autoRegister = false)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));

        _statistics = new NetStatistics();
        _connMgr = new NetConnectionManager(_transport, _cfg, _statistics);
        _pipeline = new NetDispatchPipeline(_transport, _protocol, _cfg, _statistics, _connMgr);
        _scheduler = new NetScheduler(_cfg, _pipeline, _protocol, () => _connMgr.IsConnected);

        // ── 이벤트 구독 (생성자 내 단일 위치에서 처리) ─────────────────
        _transport.StateChanged += OnStateChanged;
        _transport.DataReceived += OnDataReceived;

        // 오류 발생 → DeviceErrorOccurred 상위 이벤트로 전파
        _connMgr.ErrorOccurred += (id, ex) =>
            DeviceErrorOccurred?.Invoke(id, ex);

        // 재접속 진행 상황 → DeviceErrorOccurred 이벤트로 전달 (UI/로그에 표시)
        _connMgr.ReconnectProgress += (n, max, waitSec) =>
        {
            string strMsg = waitSec > 0
                ? $"재접속 {n}/{max} — {waitSec:F0}초 후 재시도"
                : $"재접속 {n}/{max} — 시도 중...";
            DeviceErrorOccurred?.Invoke(_cfg.DeviceId,
                new InvalidOperationException(strMsg));
        };

        // ★ v5 버그 수정: v4에서 Reconnected 가 이중 구독되던 문제 제거 (단일 구독)
        // 재접속 성공 → 스케줄러 자동 재개
        _connMgr.Reconnected += () => _scheduler.Resume();

        // Pipeline 수신 이벤트 → 상위 DeviceFrameReceived 이벤트로 전파
        _pipeline.FrameReceived += (id, frame) =>
            DeviceFrameReceived?.Invoke(id, frame);

        if (autoRegister)
            NetDeviceRegistry.Instance.Register(this);
    }

    #endregion

    #region §3 ─ 공개 프로퍼티 / 이벤트

    /// <summary>통신 형태. 파생 클래스에서 override 합니다.</summary>
    public abstract NetMode Mode { get; }

    /// <summary>장비 고유 ID.</summary>
    public int DeviceId => _cfg.DeviceId;

    /// <summary>장비 이름 (LogManager Source 자동 적용).</summary>
    public string DeviceName => _cfg.DeviceName;

    /// <summary>현재 연결 상태.</summary>
    public NetState State => _connMgr.State;

    /// <summary>
    /// 연결 정상 여부. 모든 통신 동작의 진입 조건.
    /// <para>IsConnected = false 이면 WriteAsync 는 즉시 return, RequestAsync 는 즉시 Fail 반환.</para>
    /// </summary>
    public bool IsConnected => _connMgr.IsConnected;

    /// <summary>통신 통계 (WPF 바인딩 / 대시보드 활용).</summary>
    public NetStatistics Statistics => _statistics;

    /// <summary>
    /// 프레임 수신·디코딩 완료 시 발생. (DeviceId, frame)
    /// <para>⚠ 백그라운드 스레드 — WPF: <c>Dispatcher.InvokeAsync</c> 필수.</para>
    /// <para><b>Passive 모드:</b> 서버 Push → TcpTransport 수신 루프 → 이 이벤트</para>
    /// <para><b>RequestResponse 모드:</b> PeriodicRead 응답 / RequestAsync 응답 → 이 이벤트</para>
    /// </summary>
    public event Action<int, byte[]>? DeviceFrameReceived;

    /// <summary>
    /// 연결 상태 변경 시 발생. (DeviceId, NetState)
    /// <para>⚠ 백그라운드 스레드 — WPF: <c>Dispatcher.InvokeAsync</c> 필수.</para>
    /// </summary>
    public event Action<int, NetState>? DeviceStateChanged;

    /// <summary>
    /// 오류 발생 / 재접속 진행 상황 통보 시 발생. (DeviceId, Exception)
    /// <para>⚠ 백그라운드 스레드 — WPF: <c>Dispatcher.InvokeAsync</c> 필수.</para>
    /// <para>재접속 시도 메시지도 이 이벤트로 전달됩니다: "재접속 1/∞ — 시도 중..."</para>
    /// </summary>
    public event Action<int, Exception>? DeviceErrorOccurred;

    #endregion

    #region §4 ─ 공개 메서드

    /// <summary>
    /// 채널을 시작합니다. 접속 → 파이프라인 → 스케줄러 순으로 구동.
    /// </summary>
    /// <remarks>
    /// <para>내부 동작 순서:</para>
    /// <list type="number">
    ///   <item><description>ObjectDisposedException.ThrowIf → Dispose 여부 확인</description></item>
    ///   <item><description>CancellationTokenSource.CreateLinkedTokenSource → 외부 ct + 내부 _cts 연결</description></item>
    ///   <item><description>_connMgr.ConnectAsync → 실제 소켓/포트 연결 (재시도 포함)</description></item>
    ///   <item><description>_pipeline.Start → Channel[4] 소비자 루프 가동</description></item>
    ///   <item><description>_scheduler.Start → 주기 Read + Heartbeat 루프 가동</description></item>
    ///   <item><description>ConnectionMonitorAsync → 2초 주기 연결 감시 루프 시작</description></item>
    /// </list>
    /// </remarks>
    public virtual async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // 외부 ct 와 내부 _cts 둘 중 하나라도 취소되면 모든 동작 정지
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        await _connMgr.ConnectAsync(token).ConfigureAwait(false);
        _pipeline.Start(token);   // Channel[4] 소비자 루프 가동
        _scheduler.Start(token);  // 주기 Read + Heartbeat 루프 가동

        // 2초 주기 연결 감시 루프 (fire-and-forget)
        _ = Task.Run(() => ConnectionMonitorAsync(token), token);
    }

    /// <summary>
    /// 채널을 정지합니다. 파이프라인 큐 소진 후 종료.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <c>_cts.Cancel()</c> 을 먼저 호출하여 재접속 루프 즉시 중단 후 정리합니다.</para>
    /// <para>정지 순서: _cts.Cancel → Pipeline.StopAsync → Scheduler.WaitAsync → Transport.DisconnectAsync</para>
    /// </remarks>
    public virtual async Task StopAsync()
    {
        _cts.Cancel();  // 먼저 취소 — 재접속 루프 즉시 중단, OnStateChanged 에서 재진입 방지

        await _pipeline.StopAsync().ConfigureAwait(false);

        try { await _scheduler.WaitAsync().ConfigureAwait(false); }
        catch (OperationCanceledException) { }

        await _transport.DisconnectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 데이터를 전송합니다 (Fire-and-forget).
    /// </summary>
    /// <param name="data">전송 페이로드 (프로토콜 인코딩 전 원시 데이터)</param>
    /// <param name="priority">
    /// 큐 우선순위. 기본값: Write(1).
    /// 비상 정지 명령은 NetPriority.Critical(0) 사용 권장.
    /// </param>
    /// <param name="expectResponse">
    /// true = 전송 후 서버 응답 수신 → DeviceFrameReceived 이벤트 발생.
    /// false = 전송만 수행 (기본값, Fire-and-forget).
    /// </param>
    /// <param name="ct">취소 토큰</param>
    /// <remarks>
    /// <para>
    /// 연결이 없으면 즉시 return (예외 없음).
    /// 실패 시 DeviceErrorOccurred 이벤트로 통보됩니다.
    /// </para>
    /// <para>
    /// <b>expectResponse=true 사용 예 (Modbus FC06 ACK 수신):</b>
    /// <code>
    /// // 서버가 Write 에 ACK 응답을 보낼 때
    /// await channel.WriteAsync(setpointFrame, expectResponse: true);
    /// // → DeviceFrameReceived 이벤트에서 ACK 수신
    /// </code>
    /// </para>
    /// </remarks>
    public async Task WriteAsync(
        byte[] data,
        NetPriority priority = NetPriority.Write,
        bool expectResponse = false,
        CancellationToken ct = default)
    {
        if (!IsConnected) return;  // 연결 없으면 스킵, 예외 없음

        var pkt = NetPacket.CreateWrite(_protocol.Encode(data), priority, ct, expectResponse);
        await _pipeline.EnqueueAsync(pkt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 요청 프레임을 전송하고 응답을 기다립니다 (RequestResponse 전용).
    /// </summary>
    /// <param name="requestData">요청 페이로드 (프로토콜 인코딩 전 원시 데이터)</param>
    /// <param name="timeout">응답 대기 시간. null=cfg.RequestTimeout 사용.</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>
    /// 성공: <see cref="NetResult.IsOk"/> = true, <see cref="NetResult.Data"/> = 응답 페이로드.<br/>
    /// 실패: <see cref="NetResult.IsError"/> = true, <see cref="NetResult.Error"/> = 원인 예외.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 연결이 없으면 즉시 <see cref="NetResult.Fail(string)"/> 반환 (큐 투입 없음).
    /// </para>
    /// <para>
    /// <b>내부 동작 순서:</b>
    /// </para>
    /// <list type="number">
    ///   <item><description>IsConnected 확인 → false 이면 즉시 Fail 반환</description></item>
    ///   <item><description>new TaskCompletionSource&lt;NetResult&gt;() 생성</description></item>
    ///   <item><description>NetPacket.CreateRequest(data, tcs, ct) → Channel[Write=1] 투입</description></item>
    ///   <item><description>tcs.Task.WaitAsync(timeoutCts.Token) 에서 대기</description></item>
    ///   <item><description>DispatchAsync 에서 WriteAsync → ReadAsync → tcs.SetResult()</description></item>
    ///   <item><description>await 깨어나 결과 반환</description></item>
    /// </list>
    /// </remarks>
    public async Task<NetResult> RequestAsync(
        byte[] requestData,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            return NetResult.Fail($"[{DeviceName}] 연결 없음 ({State})");

        // 응답 결과를 호출자에게 전달할 TCS 생성
        // RunContinuationsAsynchronously: 결과 준비 후에도 비동기적으로 후속 작업 실행
        var tcs = new TaskCompletionSource<NetResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var packet = NetPacket.CreateRequest(_protocol.Encode(requestData), tcs, ct);
        await _pipeline.EnqueueAsync(packet, ct).ConfigureAwait(false);

        var limit = timeout ?? _cfg.RequestTimeout;

        // 외부 ct 와 타임아웃 CTS 연결 — 둘 중 하나라도 취소되면 대기 종료
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(limit);

        try
        {
            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return NetResult.Fail(new TimeoutException(
                $"[{DeviceName}#{DeviceId}] 요청 타임아웃 ({limit.TotalMilliseconds:F0}ms)"));
        }
    }

    /// <summary>
    /// 수신 채널에서 프레임을 비동기 열거합니다 (Passive 모드 권장).
    /// </summary>
    /// <example><code>
    /// await foreach (var frame in channel.ReadAllAsync(ct))
    /// {
    ///     ProcessFrame(frame);
    /// }
    /// </code></example>
    public IAsyncEnumerable<byte[]> ReadAllAsync(CancellationToken ct = default)
        => _pipeline.ReadAllAsync(ct);

    #endregion

    #region §5 ─ 이벤트 핸들러 (internal)

    /// <summary>
    /// Transport.StateChanged 이벤트 핸들러.
    /// 재접속 시작 → Scheduler.Pause / 연결 끊김 → HandleErrorAsync 트리거.
    /// </summary>
    private void OnStateChanged(NetState state)
    {
        // 재접속 시작 → 주기 Read/Heartbeat 일시 정지
        if (state is NetState.Connecting or NetState.Reconnecting)
            _scheduler.Pause();

        // [TCP Passive] PassiveReceiveLoopAsync: read==0 또는 IOException
        //   → State = NetState.Error → 여기 진입 → 재접속 트리거
        // [조건] 정상 종료(_cts.Cancelled) 또는 Dispose 중이면 재접속 안 함
        if (state == NetState.Error &&
            !_disposed &&
            !(_cts?.IsCancellationRequested ?? true))
        {
            _ = _connMgr.HandleErrorAsync(
                new IOException($"[{DeviceName}] 예기치 못한 연결 끊김 — 재접속 시작"),
                null, null, _cts!.Token);
        }

        DeviceStateChanged?.Invoke(DeviceId, state);
    }

    /// <summary>
    /// Transport.DataReceived 이벤트 핸들러 (Passive 수신 경로).
    /// Serial DataReceived 또는 TCP PassiveReceiveLoopAsync → 이 핸들러.
    /// </summary>
    private void OnDataReceived(byte[] raw) => _pipeline.PushReceived(raw);

    #endregion

    #region §6 ─ 연결 감시 루프 (재접속 누락 보장)

    /// <summary>
    /// 2초 주기로 연결 상태를 감시합니다.
    /// <para>
    /// 이미 재접속 진행 중이 아닌 상태에서 IsConnected=false 이면
    /// HandleErrorAsync 를 통해 재접속을 트리거합니다.
    /// </para>
    /// <para>
    /// OnStateChanged 의 재접속 트리거가 누락되는 엣지 케이스를 보완합니다.
    /// </para>
    /// </summary>
    private async Task ConnectionMonitorAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(2_000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            if (_disposed || ct.IsCancellationRequested) break;

            if (!IsConnected && !_connMgr.IsReconnecting)
            {
                _ = _connMgr.HandleErrorAsync(
                    new IOException($"[{DeviceName}] 감시 루프: 재접속 시도"),
                    null, null, ct);
            }
        }
    }

    #endregion

    #region §7 ─ IAsyncDisposable

    /// <summary>
    /// 채널을 완전히 폐기합니다. 재접속 시도 없이 즉시 종료.
    /// <para>StopAsync → Pipeline.StopAsync → Scheduler.WaitAsync 순으로 안전하게 종료.</para>
    /// <para><c>await using</c> 패턴 사용 권장.</para>
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);

        // 이벤트 구독 해제
        _transport.StateChanged -= OnStateChanged;
        _transport.DataReceived -= OnDataReceived;

        await _connMgr.DisposeAsync().ConfigureAwait(false);
        await _pipeline.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);

        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}