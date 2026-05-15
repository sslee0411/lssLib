// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetDispatchPipeline.cs
//  역할: 우선순위별 Channel[4] 분리 파이프라인 (lock 없음)
//
//  ┌─ 이 파일에 ReadAsync 가 있는 이유 (핵심 개념) ─────────────────┐
//  │                                                                  │
//  │  Pipeline = "Write 전담"이 아니라 "패킷 처리 전담"입니다.       │
//  │                                                                  │
//  │  패킷 종류별 처리:                                               │
//  │                                                                  │
//  │  ① Write 패킷 (WriteAsync 호출 시)                              │
//  │      → Transport.WriteAsync 만 호출                             │
//  │      → 응답 없음 (Fire-and-forget)                              │
//  │                                                                  │
//  │  ② Request 패킷 (RequestAsync 호출 시) ← ReadAsync 있음        │
//  │      → Transport.WriteAsync (요청 전송)                         │
//  │      → Transport.ReadAsync  (응답 수신) ← RequestResponse 전용 │
//  │      → TCS.SetResult → RequestAsync await 완료                 │
//  │                                                                  │
//  │  ③ PeriodicRead 패킷 (NetScheduler 자동 생성) ← ReadAsync 있음 │
//  │      → Transport.WriteAsync (주기 Read 명령 전송)               │
//  │      → Transport.ReadAsync  (응답 수신) ← RequestResponse 전용 │
//  │      → DeviceFrameReceived 이벤트                               │
//  │                                                                  │
//  │  ④ Passive 수신 (서버 Push / Serial DataReceived)               │
//  │      → Pipeline 의 ProcessLoopAsync 와 무관                    │
//  │      → Transport.DataReceived 이벤트 → PushReceived() 경로     │
//  │      → DispatchAsync 에서 ReadAsync 호출 없음                  │
//  │                                                                  │
//  │  ★ ReadAsync 는 RequestResponse 모드 전용입니다.                │
//  └──────────────────────────────────────────────────────────────────┘
//
//  ┌─ v3 vs v5 파이프라인 구조 비교 ─────────────────────────────────┐
//  │  v3: Channel<NetPacket>(ingress) → lock + PriorityQueue → 소비자│
//  │  v5: Channel[0]Critical ─┐                                       │
//  │      Channel[1]Write     ├─► ProcessLoopAsync (우선순위순 TryRead)│
//  │      Channel[2]Read      │                                       │
//  │      Channel[3]Low       ─┘                                       │
//  └──────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using System.Threading.Channels;
using System.Diagnostics;

namespace lssLib.Net;

/// <summary>
/// 우선순위별 <see cref="Channel{T}"/> 4개를 분리하여 운영하는 디스패치 파이프라인.
/// </summary>
/// <remarks>
/// <para>
/// <b>채널 인덱스 = <see cref="NetPriority"/> 값:</b>
/// Critical(0) &gt; Write(1) &gt; Read(2) &gt; Low(3)
/// </para>
/// <para>
/// 소비자 루프는 0번 채널부터 순서대로 비블로킹 TryRead 를 시도합니다.
/// 높은 우선순위 채널에 패킷이 있으면 낮은 채널은 처리하지 않습니다.
/// </para>
/// <para>
/// <b>Passive 수신 경로 (ProcessLoopAsync 와 무관):</b>
/// <c>Transport.DataReceived → PushReceived() → TryDecode → 이벤트</c>
/// </para>
/// </remarks>
internal sealed class NetDispatchPipeline : IAsyncDisposable
{
    #region §1 ─ 필드

    private const int CHANNEL_COUNT = 4;

    /// <summary>
    /// 우선순위별 패킷 채널 배열 (Critical=0, Write=1, Read=2, Low=3).
    /// WriteAsync / RequestAsync / PeriodicRead / Heartbeat 패킷이 투입됩니다.
    /// </summary>
    private readonly Channel<NetPacket>[] _channels = new Channel<NetPacket>[CHANNEL_COUNT];

    /// <summary>
    /// 디코딩 완료 수신 프레임 채널.
    /// ReadAllAsync() 비동기 열거 경로를 지원합니다.
    /// Passive: PushReceived() 에서 투입
    /// RequestResponse: DispatchAsync PeriodicRead 경로에서 투입
    /// </summary>
    private readonly Channel<byte[]> _receiveChannel;

    private readonly INetTransport _transport;
    private readonly INetProtocol _protocol;
    private readonly NetDeviceConfig _cfg;
    private readonly NetStatistics _stats;
    private readonly NetConnectionManager _connMgr;

    private Task? _processTask;
    private volatile bool _disposed;

    #endregion

    #region §2 ─ 생성자

    internal NetDispatchPipeline(
        INetTransport transport,
        INetProtocol protocol,
        NetDeviceConfig cfg,
        NetStatistics stats,
        NetConnectionManager connMgr)
    {
        _transport = transport;
        _protocol = protocol;
        _cfg = cfg;
        _stats = stats;
        _connMgr = connMgr;

        for (int i = 0; i < CHANNEL_COUNT; i++)
            _channels[i] = Channel.CreateUnbounded<NetPacket>(
                new UnboundedChannelOptions { SingleReader = false });

        _receiveChannel = cfg.ReceiveChannelCapacity > 0
            ? Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(cfg.ReceiveChannelCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest   // 용량 초과 시 오래된 항목 제거
                })
            : Channel.CreateUnbounded<byte[]>();
    }

    #endregion

    #region §3 ─ 이벤트

    /// <summary>
    /// 프레임 수신·디코딩 완료 시 발생.
    /// <para>Passive: PushReceived() 경로 (Transport.DataReceived → 이 이벤트)</para>
    /// <para>RequestResponse: PeriodicRead DispatchAsync 경로 (ReadAsync → 이 이벤트)</para>
    /// </summary>
    public event Action<int, byte[]>? FrameReceived;

    #endregion

    #region §4 ─ 공개 API

    /// <summary>패킷을 우선순위 채널에 투입합니다.</summary>
    internal ValueTask EnqueueAsync(NetPacket packet, CancellationToken ct)
        => _channels[(int)packet.Priority].Writer.WriteAsync(packet, ct);

    /// <summary>수신 채널에서 디코딩된 프레임을 비동기 열거합니다.</summary>
    internal IAsyncEnumerable<byte[]> ReadAllAsync(CancellationToken ct)
        => _receiveChannel.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Transport.DataReceived 핸들러에서 호출됩니다.
    /// <para>
    /// <b>이 경로는 Passive 모드 전용입니다:</b><br/>
    /// Serial DataReceived 이벤트 또는 TCP PassiveReceiveLoopAsync 에서 호출됩니다.
    /// RequestResponse 모드에서는 DispatchAsync 의 ReadAsync 경로를 사용합니다.
    /// </para>
    /// </summary>
    internal void PushReceived(byte[] raw)
    {
        if (!_protocol.TryDecode(raw, out var decoded)) return;

        // 디코딩 성공 → 통계 기록 + 수신 채널 투입 + 이벤트 발생
        _stats.RecordReceived();
        _receiveChannel.Writer.TryWrite(decoded);
        FrameReceived?.Invoke(_cfg.DeviceId, decoded);
    }

    /// <summary>소비자 루프를 시작합니다.</summary>
    internal void Start(CancellationToken ct)
        => _processTask = Task.Run(() => ProcessLoopAsync(ct), ct);

    /// <summary>모든 채널을 닫고 소비자 루프 종료를 기다립니다.</summary>
    internal async Task StopAsync()
    {
        foreach (var ch in _channels)
            ch.Writer.TryComplete();

        if (_processTask is not null)
        {
            try { await _processTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    #endregion

    #region §5 ─ 소비자 루프

    /// <summary>
    /// 우선순위별 채널에서 패킷을 비블로킹으로 TryRead → DispatchAsync 호출.
    /// <para>
    /// Critical(0) → Write(1) → Read(2) → Low(3) 순서로 비블로킹 TryRead 시도.
    /// 모든 채널이 비어있으면 최대 50ms 대기 (채널 중 하나라도 데이터가 들어오면 즉시 깨어남).
    /// </para>
    /// </summary>
    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Critical(0) → Write(1) → Read(2) → Low(3) 비블로킹 순차 확인
            NetPacket? packet = null;
            for (int i = 0; i < CHANNEL_COUNT; i++)
            {
                // 높은 우선순위 채널에 패킷이 있으면 낮은 채널은 처리하지 않음
                if (_channels[i].Reader.TryRead(out packet)) break;
            }

            if (packet is null)
            {
                // 모든 채널 비어있음 → 최대 50ms WhenAny 대기
                try
                {
                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    waitCts.CancelAfter(50);

                    await Task.WhenAny(
                        _channels[0].Reader.WaitToReadAsync(waitCts.Token).AsTask(),
                        _channels[1].Reader.WaitToReadAsync(waitCts.Token).AsTask(),
                        _channels[2].Reader.WaitToReadAsync(waitCts.Token).AsTask(),
                        _channels[3].Reader.WaitToReadAsync(waitCts.Token).AsTask()
                    ).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested) break;
                }
                continue;
            }

            await DispatchAsync(packet, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 패킷 종류별 처리 핵심 로직.
    ///
    /// <list type="table">
    ///   <item><term>① Write (ExpectResponse=false)</term>
    ///         <description>WriteAsync 만 → 응답 없음 (Fire-and-forget)</description></item>
    ///   <item><term>② Write (ExpectResponse=true)</term>
    ///         <description>WriteAsync + ReadAsync → FrameReceived 이벤트</description></item>
    ///   <item><term>③ Request</term>
    ///         <description>WriteAsync + ReadAsync → TCS.SetResult → RequestAsync 완료</description></item>
    ///   <item><term>④ PeriodicRead</term>
    ///         <description>WriteAsync + ReadAsync → FrameReceived 이벤트 (TCS 없음)</description></item>
    /// </list>
    ///
    /// <para>★ ReadAsync 에는 RequestTimeout 이 적용됩니다.</para>
    /// </summary>
    private async Task DispatchAsync(NetPacket packet, CancellationToken ct)
    {
        if (!_connMgr.IsConnected)
        {
            packet.Tcs?.TrySetResult(NetResult.Fail($"[{_cfg.DeviceName}] 연결 없음"));
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // 모든 패킷 공통: 프로토콜 인코딩 완료 데이터 전송
            await _transport.WriteAsync(packet.Data, ct).ConfigureAwait(false);
            _stats.RecordSent();

            // ──────────────────────────────────────────────────────────
            // ① Write + 응답 수신 (ExpectResponse=true)
            //    WriteAsync 후 서버 응답을 FrameReceived 이벤트로 전달
            //    예: Modbus FC06 Write → 서버 ACK / Heartbeat ACK
            // ──────────────────────────────────────────────────────────
            if (packet.Mode == PacketMode.Write && packet.ExpectResponse)
            {
                var raw = await ReadWithTimeoutAsync(ct).ConfigureAwait(false);
                if (raw is null) return;  // 타임아웃 — 다음 패킷 처리
                _stats.RecordReceived();
                if (_protocol.TryDecode(raw, out var d0))
                {
                    _receiveChannel.Writer.TryWrite(d0);
                    FrameReceived?.Invoke(_cfg.DeviceId, d0);
                }
                return;
            }

            // ──────────────────────────────────────────────────────────
            // ② Request — RequestAsync 단발 요청-응답
            //    WriteAsync → ReadAsync → TCS.SetResult → await 완료
            // ──────────────────────────────────────────────────────────
            if (packet.Mode == PacketMode.Request && packet.Tcs is not null)
            {
                var raw = await ReadWithTimeoutAsync(ct).ConfigureAwait(false);
                if (raw is null)
                {
                    packet.Tcs.TrySetResult(NetResult.Fail(
                        new TimeoutException($"[{_cfg.DeviceName}] Request 응답 타임아웃")));
                    return;
                }
                sw.Stop();
                _stats.RecordResponse(sw.ElapsedMilliseconds);
                _stats.RecordReceived();
                packet.Tcs.TrySetResult(
                    _protocol.TryDecode(raw, out var d1)
                        ? NetResult.Ok(d1)
                        : NetResult.Fail("프로토콜 디코딩 실패"));
                return;
            }

            // ──────────────────────────────────────────────────────────
            // ③ PeriodicRead — READ_CMD 주기 요청-응답
            //    WriteAsync → ReadAsync → FrameReceived 이벤트
            //    ★ Passive 모드에서는 이 분기 실행 안 됨
            // ──────────────────────────────────────────────────────────
            if (packet.Mode == PacketMode.PeriodicRead)
            {
                var raw = await ReadWithTimeoutAsync(ct).ConfigureAwait(false);
                if (raw is null) return;  // 타임아웃 — 다음 주기 재시도
                _stats.RecordReceived();
                if (_protocol.TryDecode(raw, out var d2))
                {
                    _receiveChannel.Writer.TryWrite(d2);
                    FrameReceived?.Invoke(_cfg.DeviceId, d2);
                }
                return;
            }

            // ④ Write (ExpectResponse=false) / Retry: 전송만 완료 (위에서 이미 처리)
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            packet.Tcs?.TrySetResult(NetResult.Fail(ex));

            // Write 실패 시 재시도 처리
            byte[]? retryPayload = null;
            if (_cfg.IsRetryEnabled &&
                _cfg.RetryTarget.HasFlag(RetryTarget.Write) &&
                packet.Mode is PacketMode.Write or PacketMode.Retry &&
                packet.RetryCount < _cfg.MaxRetries)
            {
                _stats.RecordWriteRetry();
                retryPayload = packet.ToRetry().Data;
            }

            // fire-and-forget: HandleErrorAsync 실행 중에도 ProcessLoopAsync 계속 동작
            _ = _connMgr.HandleErrorAsync(ex, retryPayload,
                async (data, t) => await EnqueueAsync(
                    NetPacket.CreateWrite(data, NetPriority.Critical, t), t)
                    .ConfigureAwait(false),
                ct);
        }
    }

    /// <summary>
    /// RequestTimeout 을 적용한 ReadAsync 헬퍼.
    /// <para>타임아웃 초과 시 null 반환 (예외 없음).</para>
    /// </summary>
    private async Task<byte[]?> ReadWithTimeoutAsync(CancellationToken ct)
    {
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        readCts.CancelAfter(_cfg.RequestTimeout);
        try
        {
            return await _transport.ReadAsync(readCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;  // 타임아웃 (외부 취소가 아닌 경우)
        }
    }

    #endregion

    #region §6 ─ IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }

    #endregion
}