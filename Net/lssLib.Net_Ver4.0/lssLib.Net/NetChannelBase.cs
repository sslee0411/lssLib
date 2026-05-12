// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Abstractions/NetChannelBase.cs
//  역할: 3개 전문 클래스를 조합하는 얇은 오케스트레이터
//
//  ┌─ 내부 구성 ────────────────────────────┐
//  │  NetChannelBase (오케스트레이터)                                    │
//  │    ├─ NetConnectionManager  연결·재접속·상태 머신               │
//  │    ├─ NetDispatchPipeline   Channel[4] 우선순위 (lock 없음)       │
//  │    ├─ NetScheduler          주기 Read + Heartbeat (Pause/Resume)  │
//  │    └─ NetStatistics         통신 통계 수집                        │
//  └───────────────────────────────────┘
//
//  ┌─ 데이터 흐름 ──────────────────────────┐
//  │  WriteAsync / RequestAsync                                        │
//  │    → IsConnected 가드 → Pipeline.EnqueueAsync                   │
//  │    → Channel[Priority] → ProcessLoopAsync → DispatchAsync      │
//  │    → 성공: 결과 전달                                             │
//  │    → 실패: ConnectionManager.HandleErrorAsync                    │
//  │             → Scheduler.Pause → Disconnect → ReconnectAsync    │
//  │             → 성공: Scheduler.Resume → 보존 Write 재투입        │
//  └──────────────────────────────────┘
//  ┌─ 연결 끊김 감지 흐름 (v4 수정) ─────────────────┐
//  │                                                                  │
//  │  [TCP Passive] PassiveReceiveLoopAsync 예외                      │
//  │      → State = NetState.Error                                   │
//  │          → StateChanged 이벤트                                  │
//  │              → OnStateChanged(Error)                            │
//  │                  → _connMgr.HandleErrorAsync()  ← 재접속 트리거│
//  │                                                                  │
//  │  [StopAsync] _cts.Cancel() 먼저 호출                             │
//  │      → _cts.IsCancellationRequested == true                     │
//  │          → OnStateChanged(Disconnected) 에서 재접속 안 함       │
//  └─────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

//using lssLib.Log;

using System.IO;

namespace lssLib.Net;

/// <summary>
/// lssLib.Net 통신 채널 추상 베이스 (v4).
/// </summary>
/// <remarks>
/// <para>파생 클래스(Channels/)는 <see cref="Mode"/> 만 override 하면 됩니다.</para>
/// <para>전송 계층·프로토콜·설정은 생성자에서 주입합니다.</para>
///
/// <b>조립 예시:</b>
/// <code>
/// // Passive (TCP Push 서버) — enablePassiveReceive: true 필수
/// await using var channel = new PassiveNetChannel(
///     cfg,
///     TcpTransport.FromConfig(cfg, enablePassiveReceive: true),
///     new BinaryProtocol(),
///     autoRegister: true);
///
/// // RequestResponse
/// await using var channel = new RequestResponseChannel(
///     cfg,
///     TcpTransport.FromConfig(cfg),   // enablePassiveReceive 기본 false
///     new BinaryProtocol(),
///     autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) => { ... };
/// channel.DeviceStateChanged  += (id, state) => { ... };
/// channel.DeviceErrorOccurred += (id, ex)    => { ... };
///
/// await channel.StartAsync();
/// await channel.WriteAsync(frame, NetPriority.Write);
/// NetResult r = await channel.RequestAsync(queryFrame);
/// </code>
/// </remarks>
public abstract class NetChannelBase : IAsyncDisposable
{
    #region §1 ─ 필드

    private readonly NetDeviceConfigBase _cfg;
    private readonly INetTransport _transport;
    private readonly INetProtocol _protocol;

    private readonly NetStatistics _statistics;
    private readonly NetConnectionManager _connMgr;
    private readonly NetDispatchPipeline _pipeline;
    private readonly NetScheduler _scheduler;

    private CancellationTokenSource _cts = new();
    /// <summary>
    /// volatile : 여러 스레드에서 동시에 접근하는 경우에도 일관된 값을 보장합니다.
    /// 파괴되었음을 즉시 인지 가능
    /// </summary>
    private volatile bool _disposed;
    #endregion

    #region §2 ─ 생성자

    /// <param name="config">장비 설정 (DeviceId/Name/Retry/Commands 포함)</param>
    /// <param name="transport">전송 계층. XxxTransport.FromConfig(cfg) 패턴 권장.</param>
    /// <param name="protocol">프로토콜 계층. RawProtocol 또는 BinaryProtocol.</param>
    /// <param name="autoRegister">true 이면 NetDeviceRegistry 에 자동 등록.</param>
    protected NetChannelBase(
        NetDeviceConfigBase config,
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

        // 이벤트 연결
        _transport.StateChanged += OnStateChanged;
        _transport.DataReceived += OnDataReceived;
        _connMgr.ErrorOccurred += (id, ex) => DeviceErrorOccurred?.Invoke(id, ex);

        // 재접속 성공 → 스케줄러 자동 재개
        _connMgr.Reconnected += () =>
        {
            _scheduler.Resume();
            //Log(LogLevel.Info, "재접속 완료 — 스케줄러 재개");
        };

        // Pipeline 수신 이벤트 → 상위 이벤트 전파
        _pipeline.FrameReceived += (id, frame) => DeviceFrameReceived?.Invoke(id, frame);

        if (autoRegister)
            NetDeviceRegistry.Instance.Register(this);
    }

    #endregion

    #region §3 ─ 공개 프로퍼티 / 이벤트

    /// <summary>통신 형태. 파생 클래스에서 선언합니다.</summary>
    public abstract NetMode Mode { get; }

    /// <summary>장비 ID.</summary>
    public int DeviceId => _cfg.DeviceId;

    /// <summary>장비 이름 (LogManager Source 자동 적용).</summary>
    public string DeviceName => _cfg.DeviceName;

    /// <summary>현재 연결 상태.</summary>
    public NetState State => _connMgr.State;

    /// <summary>연결 정상 여부. 모든 통신 동작의 진입 조건.</summary>
    public bool IsConnected => _connMgr.IsConnected;

    /// <summary>통신 통계 (WPF 바인딩 / 대시보드 활용).</summary>
    public NetStatistics Statistics => _statistics;

    /// <summary>
    /// 프레임 수신·디코딩 완료 시 발생. (DeviceId, frame)
    /// <para>⚠ 백그라운드 스레드 — WPF: <c>Dispatcher.InvokeAsync</c> 필수.</para>
    /// <para><b>Passive 모드</b>: 서버 Push → TcpTransport 수신 루프 → 이 이벤트</para>
    /// <para><b>RequestResponse 모드</b>: PeriodicRead 응답 / RequestAsync 응답 → 이 이벤트</para>
    /// </summary>
    public event Action<int, byte[]>? DeviceFrameReceived;

    /// <summary>연결 상태 변경 시 발생. (DeviceId, NetState) ⚠ 백그라운드 스레드.</summary>
    public event Action<int, NetState>? DeviceStateChanged;

    /// <summary>오류 발생 시 발생 (재접속 전). (DeviceId, Exception) ⚠ 백그라운드 스레드.</summary>
    public event Action<int, Exception>? DeviceErrorOccurred;

    #endregion

    #region §4 ─ 공개 메서드

    /// <summary>채널을 시작합니다. 접속 → 파이프라인 → 스케줄러 순으로 구동.</summary>
    public virtual async Task StartAsync(CancellationToken ct = default)
    {
        //disposed가 true라면,
        //즉시 ObjectDisposedException을 던져서 이후의
        //ConnectAsync나 Start 로직이 실행되지 않도록 입구에서 컷(Cut)
        ObjectDisposedException.ThrowIf(_disposed, this);

        //하나만 만족해도 취소 상태
        //매개변수로 받은 ct가 취소될 때내
        //가 만든 _cts를 직접 _cts.Cancel() 할 때
        // CreateLinkedTokenSource :
        //              여러 개의 취소 신호(CancellationToken)를
        //              하나의 통합된 취소 신호로 합칠 때 사용하는 기능
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        await _connMgr.ConnectAsync(token) //실제로 오픈하여 원격 장치와 연결을 시도
                       .ConfigureAwait(false);

        _pipeline.Start(token);  // 데이터 흐름(Pipeline)을 활성화
        _scheduler.Start(token); //"1초마다 데이터를 읽어와라", "특정 명령을 우선 전송해라"와 같은 스케줄링 로직을 가동

        /*Log(LogLevel.Info,
            $"[{GetType().Name}] 시작 Mode={Mode} " +
            $"ReadCmd={_cfg.ReadCommands.Count} Sequential={_cfg.IsSequential}");*/
    }

    /// <summary>
    /// 채널을 정지합니다. 파이프라인 큐 소진 후 종료.
    /// ConfigureAwait : 컨텍스트 캡처 방지. UI 스레드에서 호출해도 안전하게 백그라운드에서 계속 실행됩니다.
    /// <para>⚠ <c>_cts.Cancel()</c> 을 먼저 호출하여 OnStateChanged 에서 재접속 루프 진입을 방지합니다.</para>
    /// </summary>
    public virtual async Task StopAsync()
    {
        // ★ Cancel 먼저 → OnStateChanged(Error/Disconnected) 에서 재접속 안 함
        _cts.Cancel();

        await _pipeline.StopAsync().ConfigureAwait(false);
        try { await _scheduler.WaitAsync().ConfigureAwait(false); }
        catch (OperationCanceledException) { }

        await _transport.DisconnectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 데이터를 전송 큐에 넣습니다 (Fire-and-forget).
    /// <para>연결 없으면 스킵. 실패는 <see cref="DeviceErrorOccurred"/> 이벤트로 통보.</para>
    /// </summary>
    public async Task WriteAsync(byte[] data,
                                 NetPriority priority = NetPriority.Write,
                                 CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            // Log(LogLevel.Warn, $"Write 스킵 — {State}"); 
            return;
        }

        var pkt = NetPacket.CreateWrite(_protocol.Encode(data), priority, ct);

        await _pipeline.EnqueueAsync(pkt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 요청 프레임을 전송하고 응답을 기다립니다 (RequestResponse 전용).
    /// <para>연결 없으면 즉시 <see cref="NetResult.Fail(string)"/> 반환.</para>
    /// </summary>
    public async Task<NetResult> RequestAsync(byte[] requestData,
                                              TimeSpan? timeout = null,
                                              CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            return NetResult.Fail($"[{DeviceName}] 연결 없음 ({State})");
        }

        // 결과를 기다리는 TaskCompletionSource 생성.RunContinuationsAsynchronously 옵션으로,
        // 결과가 준비된 후에도 비동기적으로 후속 작업이 실행되도록 보장합니다.
        var tcs = new TaskCompletionSource<NetResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 요청 데이터를 프로토콜로 인코딩하여 패킷 생성 후 파이프라인에 EnqueueAsync 합니다.
        var packet = NetPacket.CreateRequest(_protocol.Encode(requestData), tcs, ct);

        // 요청데이터 입력과 함께 패킷을 생성하여 파이프라인에 EnqueueAsync 합니다.
        // 이때, 요청 패킷은 NetDispatchPipeline의 처리 대기열에 추가됩니다.
        await _pipeline.EnqueueAsync(packet, ct).ConfigureAwait(false);

        // 요청 타임아웃 설정. 지정된 시간 후에 timeoutCts.Token이 취소되어 대기 중인
        var limit = timeout ?? _cfg.RequestTimeout;

        // CancellationTokenSource.CreateLinkedTokenSource(ct) :
        //          외부에서 전달된 ct와 별도의 timeoutCts를 생성하여,
        //          둘 중 하나라도 취소되면 timeoutCts.Token이 취소됩니다.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // 요청 타임아웃 설정. 지정된 시간 후에 timeoutCts.Token이 취소되어 대기 중인
        // Task가 OperationCanceledException을 throw 하도록 합니다.
        timeoutCts.CancelAfter(limit);

        try
        {
            // 요청 패킷이 처리되어 응답이 도착하면,
            // NetDispatchPipeline에서 tcs.SetResult()가 호출되어 대기 중인 Task가 완료됩니다.
            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return NetResult.Fail(new TimeoutException(
                $"[{DeviceName}#{DeviceId}] 요청 타임아웃 ({limit.TotalMilliseconds:F0}ms)"));
        }
    }

    /// <summary>수신 채널에서 프레임을 비동기 열거합니다 (Passive 모드 권장).</summary>
    public System.Collections.Generic.IAsyncEnumerable<byte[]> ReadAllAsync(CancellationToken ct = default)
        => _pipeline.ReadAllAsync(ct);

    #endregion

    #region §5 ─ 가상 메서드 (파생 클래스 선택 재정의)

    /// <summary>ReadCommands 없을 때 동적 Read 프레임 생성. null 반환 시 건너뜀.</summary>
    protected virtual Task<byte[]?> BuildExtraReadRequestAsync(CancellationToken ct)
        => Task.FromResult<byte[]?>(null);

    #endregion

    #region §6 ─ 이벤트 핸들러

    private void OnStateChanged(NetState state)
    {
        // 재접속 시작 → 스케줄러 일시 정지
        if (state is NetState.Connecting or
                     NetState.Reconnecting)
        {
            _scheduler.Pause();

            // ── 연결 끊김 감지 → 재접속 트리거 ──────────────────────────────
            // [TCP Passive] PassiveReceiveLoopAsync: read==0 또는 IOException
            //              → State = NetState.Error → 여기 진입
            // [조건] 정상 종료(_cts.Cancelled) 또는 Dispose 중이면 재접속 안 함
            if (state == NetState.Error &&
                !_disposed &&
                !(_cts?.IsCancellationRequested ?? true))
            {
                _ = _connMgr.HandleErrorAsync(
                    new IOException($"[{DeviceName}] 예기치 못한 연결 끊김 — 재접속 시작"),
                    null, null, _cts!.Token);
            }

        }
        DeviceStateChanged?.Invoke(DeviceId, state);
        //Log(LogLevel.Info, $"상태 변경 → {state}");
    }

    private void OnDataReceived(byte[] raw) => _pipeline.PushReceived(raw);

    #endregion

    #region §7 ─ 로그 헬퍼

    // private void Log(LogLevel level, string message)
    //     => LogManager.Instance.AddLog(level, DeviceName, message);

    #endregion

    #region §8 ─ IAsyncDisposable

    /// <summary>
    /// 채널을 완전히 폐기합니다. 재접속 시도 없이 즉시 종료합니다.
    /// StopAsync → Pipeline.StopAsync → Scheduler.WaitAsync 순으로 안전하게 종료 대기 후,
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        await StopAsync().ConfigureAwait(false);

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