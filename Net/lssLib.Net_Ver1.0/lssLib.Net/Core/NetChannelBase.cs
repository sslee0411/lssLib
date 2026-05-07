// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · NetChannelBase.cs
//  역할: 통신형태 + 전송계층 + 프로토콜 통합 추상 베이스
//        Channel<T> 생산자-소비자 + PriorityQueue + 재접속 + Heartbeat
// ══════════════════════════════════════════════════════════════════════

using System.Threading.Channels;
// using lssLib.Log;

namespace lssLib.Net;

/// <summary>
/// lssLib.Net 통신 채널 추상 베이스.
/// </summary>
/// <remarks>
/// 내부 데이터 흐름:
/// <code>
/// [외부] WriteAsync / RequestAsync
///         │
///         ▼
/// Channel&lt;NetPacket&gt;  ← 생산자 진입 (블로킹 없음)
///         │
///         ▼
/// ProcessQueueAsync   ← 소비자 단일 루프
///   ├─ PriorityQueue 재정렬  (Critical &gt; Write &gt; Read &gt; Low)
///   └─ DispatchPacketAsync
///         ├─ INetTransport.WriteAsync
///         └─ INetProtocol.Encode / TryDecode
///                 │
///                 ▼
///         FrameReceived 이벤트  or  Tcs.SetResult
///
/// [수신 루프] INetTransport.DataReceived 이벤트
///         │
///         ▼
/// INetProtocol.TryDecode → _receiveChannel (Passive 소비)
///         │
///         ▼
/// FrameReceived 이벤트 → 상위 계층
/// </code>
///
/// 파생 클래스는 다음을 결합하여 구체적인 채널을 만듭니다:
/// <list type="bullet">
///   <item><description><b>통신 형태</b> — <see cref="Mode"/> 오버라이드 (Passive / RequestResponse)</description></item>
///   <item><description><b>전송 계층</b> — 생성자에서 <see cref="INetTransport"/> 주입</description></item>
///   <item><description><b>프로토콜</b>  — 생성자에서 <see cref="INetProtocol"/> 주입</description></item>
/// </list>
/// </remarks>
public abstract class NetChannelBase : IAsyncDisposable
{
    #region §1 ─ 필드

    private readonly INetTransport _transport;
    private readonly INetProtocol _protocol;
    private readonly NetConfig _config;

    // 진입 채널 (외부 Write 블로킹 방지용 언바운드)
    private readonly Channel<NetPacket> _ingressChannel;
    // 우선순위 재정렬 큐 (소비자 내부)
    private readonly PriorityQueue<NetPacket, int> _priorityQueue = new();
    private readonly object _queueLock = new();

    // 수신 채널 (Passive 모드: 상위 계층이 ReadAllAsync 로 소비)
    private readonly Channel<byte[]> _receiveChannel;

    private CancellationTokenSource _cts = new();
    private Task? _processTask;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private Task? _periodicReadTask;
    private Task? _reconnectTask;

    private volatile bool _disposed;

    private const string LOG_SRC = "Net";

    #endregion

    #region §2 ─ 생성자

    /// <summary>
    /// <see cref="NetChannelBase"/> 를 초기화합니다.
    /// </summary>
    /// <param name="transport">전송 계층 구현체 (TCP / UDP / Serial / SharedMemory)</param>
    /// <param name="protocol">프로토콜 계층 구현체 (Raw / Binary / Modbus)</param>
    /// <param name="config">채널 공통 설정</param>
    protected NetChannelBase(INetTransport transport, INetProtocol protocol, NetConfig config)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // 진입 채널: 언바운드 (외부에서의 Write 를 즉시 수용)
        _ingressChannel = System.Threading.Channels.Channel.CreateUnbounded<NetPacket>(
            new UnboundedChannelOptions { SingleReader = true });

        // 수신 채널: 설정 기반

        _receiveChannel = _config.ReceiveChannelCapacity > 0
    ? System.Threading.Channels.Channel.CreateBounded<byte[]>(
        new System.Threading.Channels.BoundedChannelOptions(_config.ReceiveChannelCapacity)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
            SingleReader = true // 상위 계층에서 ReadAllAsync로 혼자 소비하므로 true 권장
        })
    : System.Threading.Channels.Channel.CreateUnbounded<byte[]>(
        new System.Threading.Channels.UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    #endregion

        #region §3 ─ 공개 프로퍼티 / 이벤트

        /// <summary>통신 형태 (Passive 또는 RequestResponse). 파생 클래스에서 선언합니다.</summary>
    public abstract NetMode Mode { get; }

    /// <summary>현재 연결 상태.</summary>
    public NetState State => _transport.State;

    /// <summary>
    /// 프레임 수신 및 디코딩 완료 시 발생.
    /// <para>※ 백그라운드 스레드에서 호출됩니다.</para>
    /// </summary>
    public event Action<byte[]>? FrameReceived;

    /// <summary>연결 상태 변경 시 발생.</summary>
    public event Action<NetState>? StateChanged;

    /// <summary>처리 불가능한 오류 발생 시 발생.</summary>
    public event Action<Exception>? ErrorOccurred;

    #endregion

    #region §4 ─ 공개 메서드

    /// <summary>
    /// 채널을 시작합니다.
    /// 내부 처리 루프, Heartbeat, 주기적 Read, 수신 루프가 모두 구동됩니다.
    /// </summary>
    public virtual async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        await _transport.ConnectAsync(token).ConfigureAwait(false);

        _processTask = Task.Run(() => ProcessQueueAsync(token), token);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(token), token);

        if (_config.HeartbeatInterval > TimeSpan.Zero)
            _heartbeatTask = Task.Run(() => HeartbeatAsync(token), token);

        if (Mode == NetMode.RequestResponse && _config.PeriodicReadInterval > TimeSpan.Zero)
            _periodicReadTask = Task.Run(() => PeriodicReadAsync(token), token);

     //   LogManager.Instance.Info(LOG_SRC, $"[{GetType().Name}] 시작 → Mode={Mode} State={State}");
    }

    /// <summary>
    /// 채널을 정지합니다.
    /// 진입 채널을 닫고 큐에 남은 항목을 모두 처리한 후 종료합니다.
    /// </summary>
    public virtual async Task StopAsync()
    {
        // 진입 채널 마감 → ProcessQueueAsync 가 잔여 항목 소비 후 루프 종료
        _ingressChannel.Writer.TryComplete();

        try
        {
            await Task.WhenAll(
                _processTask ?? Task.CompletedTask,
                _receiveTask ?? Task.CompletedTask,
                _heartbeatTask ?? Task.CompletedTask,
                _periodicReadTask ?? Task.CompletedTask
            ).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* 정상 종료 */ }

        await _transport.DisconnectAsync().ConfigureAwait(false);
        _cts.Cancel();

    //    LogManager.Instance.Info(LOG_SRC, $"[{GetType().Name}] 정지 완료");
    }

    /// <summary>
    /// 데이터를 전송 큐에 넣습니다 (Fire-and-forget).
    /// Write 는 Read 보다 항상 높은 우선순위로 처리됩니다.
    /// </summary>
    /// <param name="data">전송할 원시 바이트 (프로토콜 인코딩 전)</param>
    /// <param name="priority">우선순위. 기본값: <see cref="NetPriority.Write"/>.</param>
    /// <param name="ct">취소 토큰</param>
    public async Task WriteAsync(byte[] data,
        NetPriority priority = NetPriority.Write, CancellationToken ct = default)
    {
        var packet = NetPacket.CreateWrite(_protocol.Encode(data), priority, ct);
        await _ingressChannel.Writer.WriteAsync(packet, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 요청 프레임을 전송하고 응답을 기다립니다 (RequestResponse 모드 전용).
    /// </summary>
    /// <param name="requestData">요청 페이로드 (프로토콜 인코딩 전)</param>
    /// <param name="timeout">응답 대기 타임아웃. null 이면 <see cref="NetConfig.RequestTimeout"/> 사용.</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns><see cref="NetResult"/> — 성공 시 디코딩된 응답 포함.</returns>
    public async Task<NetResult> RequestAsync(byte[] requestData,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<NetResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var packet = NetPacket.CreateRequest(_protocol.Encode(requestData), tcs, ct);

        await _ingressChannel.Writer.WriteAsync(packet, ct).ConfigureAwait(false);

        var limit = timeout ?? _config.RequestTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(limit);

        try
        {
            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return NetResult.Fail(new TimeoutException(
                $"[{GetType().Name}] 요청 타임아웃 ({limit.TotalMilliseconds}ms)"));
        }
    }

    /// <summary>
    /// 수신 채널에서 프레임을 비동기로 열거합니다 (Passive 모드 권장).
    /// </summary>
    public IAsyncEnumerable<byte[]> ReadAllAsync(CancellationToken ct = default)
        => _receiveChannel.Reader.ReadAllAsync(ct);

    #endregion

    #region §5 ─ 가상 메서드 (파생 클래스 재정의 가능)

    /// <summary>
    /// 주기적 Read 요청 프레임을 생성합니다.
    /// <para>RequestResponse 모드인 경우 파생 클래스에서 재정의하여 요청 프레임을 반환합니다.
    /// null 반환 시 해당 주기 전송을 건너뜁니다.</para>
    /// </summary>
    protected virtual Task<byte[]?> BuildReadRequestAsync(CancellationToken ct)
        => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// 수신된 완전한 프레임을 처리합니다.
    /// 기본 구현은 <see cref="FrameReceived"/> 이벤트를 발생시킵니다.
    /// </summary>
    protected virtual Task OnFrameReceivedAsync(byte[] frame, CancellationToken ct)
    {
        FrameReceived?.Invoke(frame);
        return Task.CompletedTask;
    }

    #endregion

    #region §6 ─ 내부 루프

    // 소비자: Ingress Channel → PriorityQueue → DispatchPacketAsync
    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        var reader = _ingressChannel.Reader;

        while (!ct.IsCancellationRequested)
        {
            // 진입 채널의 모든 항목을 PriorityQueue 로 이동
            while (reader.TryRead(out var incoming))
            {
                lock (_queueLock)
                    _priorityQueue.Enqueue(incoming, (int)incoming.Priority);
            }

            // 가장 높은 우선순위 항목 처리
            NetPacket? packet;
            lock (_queueLock)
                _priorityQueue.TryDequeue(out packet, out _);

            if (packet is null)
            {
                // 새 항목 도착 대기 (최대 100ms)
                try
                {
                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    waitCts.CancelAfter(100);
                    await reader.WaitToReadAsync(waitCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested) break;
                }
                continue;
            }

            await DispatchPacketAsync(packet, ct).ConfigureAwait(false);
        }
    }

    // 개별 패킷 전송 처리
    private async Task DispatchPacketAsync(NetPacket packet, CancellationToken ct)
    {
        try
        {
            await _transport.WriteAsync(packet.Data, ct).ConfigureAwait(false);

            // 요청-응답: 응답 수신 후 TCS 에 결과 전달
            if (packet.Mode == PacketMode.Request && packet.Tcs is not null)
            {
                var responseRaw = await _transport.ReadAsync(ct).ConfigureAwait(false);
                if (_protocol.TryDecode(responseRaw, out var decoded))
                    packet.Tcs.TrySetResult(NetResult.Ok(decoded));
                else
                    packet.Tcs.TrySetResult(NetResult.Fail("프로토콜 디코딩 실패"));
            }

            // 주기적 Read 응답 수신 (Passive 채널로 전달)
            if (packet.Mode == PacketMode.PeriodicRead)
            {
                var responseRaw = await _transport.ReadAsync(ct).ConfigureAwait(false);
                if (_protocol.TryDecode(responseRaw, out var decoded))
                {
                    _receiveChannel.Writer.TryWrite(decoded);
                    await OnFrameReceivedAsync(decoded, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
        //    LogManager.Instance.Error(LOG_SRC,
        //        $"[{GetType().Name}] 패킷 처리 오류 (Mode={packet.Mode}): {ex.Message}");

            packet.Tcs?.TrySetResult(NetResult.Fail(ex));
            ErrorOccurred?.Invoke(ex);

            // Write 실패 시 재전송 큐잉
            if (packet.Mode is PacketMode.Write or PacketMode.Retry
                && packet.RetryCount < _config.MaxWriteRetries)
            {
             //   LogManager.Instance.Warn(LOG_SRC,
             //       $"[{GetType().Name}] Write 재전송 예약 ({packet.RetryCount + 1}/{_config.MaxWriteRetries})");

                await Task.Delay(_config.RetryDelay, ct).ConfigureAwait(false);
                var retry = packet.ToRetry();  // Priority → Critical
                await _ingressChannel.Writer.WriteAsync(retry, ct).ConfigureAwait(false);
            }

            // 연결 오류 시 재접속 시도
            if (_config.AutoReconnect && _transport.State != NetState.Connected)
                _ = ScheduleReconnectAsync(ct);
        }
    }

    // 수동 수신 루프 (DataReceived 이벤트 기반 → ReceiveChannel)
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        // DataReceived 는 OnTransportDataReceived 에서 처리하므로
        // 이 루프는 ct 만료까지 유지됩니다.
        try { await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* 정상 종료 */ }
    }

    // Heartbeat 루프
    private async Task HeartbeatAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.HeartbeatInterval, ct).ConfigureAwait(false);

                var hb = _protocol.BuildHeartbeat();
                if (hb is not null)
                {
                    var packet = NetPacket.CreateWrite(hb, NetPriority.Low, ct);
                    await _ingressChannel.Writer.WriteAsync(packet, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
             //   LogManager.Instance.Warn(LOG_SRC,
             //       $"[{GetType().Name}] Heartbeat 오류: {ex.Message}");
            }
        }
    }

    // 주기적 Read 요청 루프 (RequestResponse 모드)
    private async Task PeriodicReadAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.PeriodicReadInterval, ct).ConfigureAwait(false);

                var req = await BuildReadRequestAsync(ct).ConfigureAwait(false);
                if (req is not null)
                {
                    // Read 는 NetPriority.Read — 큐에 Write 가 있으면 Write 먼저 처리됨
                    var packet = NetPacket.CreatePeriodicRead(_protocol.Encode(req), ct);
                    await _ingressChannel.Writer.WriteAsync(packet, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
             //   LogManager.Instance.Warn(LOG_SRC,
             //       $"[{GetType().Name}] 주기 Read 오류: {ex.Message}");
            }
        }
    }

    // 재접속 스케줄러 (이미 재접속 중이면 무시)
    private Task ScheduleReconnectAsync(CancellationToken ct)
    {
        if (_reconnectTask is { IsCompleted: false }) return Task.CompletedTask;
        _reconnectTask = ReconnectAsync(ct);
        return _reconnectTask;
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        int maxAttempts = _config.MaxReconnectAttempts == 0
            ? int.MaxValue : _config.MaxReconnectAttempts;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var delay = _config.ReconnectBackoff
                    ? TimeSpan.FromTicks(_config.ReconnectDelay.Ticks * (long)Math.Pow(2, attempt - 1))
                    : _config.ReconnectDelay;
                // 최대 60초 상한
                if (delay > TimeSpan.FromSeconds(60)) delay = TimeSpan.FromSeconds(60);

             //   LogManager.Instance.Warn(LOG_SRC,
             //       $"[{GetType().Name}] 재접속 대기 {delay.TotalSeconds:F1}초 ({attempt}/{maxAttempts})");

                await Task.Delay(delay, ct).ConfigureAwait(false);
                await _transport.ConnectAsync(ct).ConfigureAwait(false);

             //   LogManager.Instance.Info(LOG_SRC, $"[{GetType().Name}] 재접속 성공");
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
             //   LogManager.Instance.Error(LOG_SRC,
             //       $"[{GetType().Name}] 재접속 실패 ({attempt}/{maxAttempts}): {ex.Message}");
            }
        }

        //LogManager.Instance.Fatal(LOG_SRC, $"[{GetType().Name}] 재접속 한도 초과. 포기.");
        ErrorOccurred?.Invoke(new InvalidOperationException("재접속 한도 초과"));
    }

    #endregion

    #region §7 ─ 전송 계층 이벤트 핸들러

    private void OnTransportStateChanged(NetState state)
    {
        StateChanged?.Invoke(state);
    //    LogManager.Instance.Info(LOG_SRC, $"[{GetType().Name}] 상태 변경 → {state}");
    }

    private void OnTransportDataReceived(byte[] raw)
    {
        // Passive 수신 → 프로토콜 디코딩 → 수신 채널 + FrameReceived 이벤트
        if (!_protocol.TryDecode(raw, out var decoded)) return;

        _receiveChannel.Writer.TryWrite(decoded);
        _ = OnFrameReceivedAsync(decoded, _cts.Token);
    }

    #endregion

    #region §8 ─ IAsyncDisposable

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);

        _transport.StateChanged -= OnTransportStateChanged;
        _transport.DataReceived -= OnTransportDataReceived;

        await _transport.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}