// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetDispatchPipeline.cs
//  역할: 우선순위별 Channel[4] 분리 파이프라인 (lock 없음) 
//  패킷 관리
//
//  v3 vs v4:
//    v3: Channel<NetPacket>(ingress) → lock + PriorityQueue → 소비자
//    v4: Channel[0]Critical ─┐
//        Channel[1]Write    ─┤→ ProcessLoopAsync (높은 우선순위 먼저 TryRead)
//        Channel[2]Read     ─┤
//        Channel[3]Low      ─┘
//  ┌─ 이 파일에 ReadAsync 가 있는 이유 (핵심 개념) ──────────────────┐
//  │                                                                   │
//  │  Pipeline = "Write 전담" 이 아니라 "패킷 처리 전담" 입니다.      │
//  │                                                                   │
//  │  패킷 종류별 처리:                                                │
//  │                                                                   │
//  │  ① Write 패킷 (WriteAsync 호출 시)                               │
//  │      → Transport.WriteAsync 만 호출                              │
//  │      → 응답 없음 (Fire-and-forget)                               │
//  │                                                                   │
//  │  ② Request 패킷 (RequestAsync 호출 시) ← ReadAsync 있음         │
//  │      → Transport.WriteAsync (요청 전송)                          │
//  │      → Transport.ReadAsync  (응답 수신)  ← RequestResponse 전용 │
//  │      → TCS.SetResult → RequestAsync await 완료                  │
//  │                                                                   │
//  │  ③ PeriodicRead 패킷 (NetScheduler 자동 생성) ← ReadAsync 있음 │
//  │      → Transport.WriteAsync (주기 Read 명령 전송)               │
//  │      → Transport.ReadAsync  (응답 수신)  ← RequestResponse 전용 │
//  │      → DeviceFrameReceived 이벤트                               │
//  │                                                                   │
//  │  ④ Passive 수신 (서버 Push / Serial DataReceived)               │
//  │      → Pipeline 의 ProcessLoopAsync 와 무관                     │
//  │      → Transport.DataReceived 이벤트 → PushReceived() 경로      │
//  │      → DispatchAsync 에서 ReadAsync 호출 없음                   │
//  │                                                                   │
//  │  ★ ReadAsync 는 RequestResponse 모드 전용입니다.                 │
//  │    Passive 모드에서는 PeriodicRead/Request 패킷이 생성되지       │
//  │    않으므로 DispatchAsync 의 ReadAsync 경로는 실행되지 않습니다. │
//  └───────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using System.Threading.Channels;
using System.Diagnostics;

//using lssLib.Log;

namespace lssLib.Net;

/// <summary>
/// 우선순위별 <see cref="Channel{T}"/> 4개를 분리하여 운영하는 디스패치 파이프라인.
/// </summary>
/// <remarks>
/// <b>채널 인덱스 = <see cref="NetPriority"/> 값:</b>
/// Critical(0) > Write(1) > Read(2) > Low(3)
///
/// 소비자 루프는 0번 채널부터 순서대로 비블로킹 TryRead 를 시도합니다.
/// 높은 우선순위 채널에 패킷이 있으면 낮은 채널은 처리하지 않습니다.
///
/// <b>Passive 수신 경로 (ProcessLoopAsync 와 무관):</b>
/// <code>
/// Transport.DataReceived → PushReceived() → TryDecode → 이벤트
/// </code>
/// </remarks>
internal sealed class NetDispatchPipeline : IAsyncDisposable
{
    #region §1 ─ 필드

    private const int CHANNEL_COUNT = 4;

    /// <summary>
    /// 우선순위별 패킷 채널 배열 (Critical, Write, Read, Low).
    /// WriteAsync / RequestAsync / PeriodicRead / Heartbeat 패킷이 투입됩니다.
    /// </summary>
    private readonly Channel<NetPacket>[] _channels = new Channel<NetPacket>[CHANNEL_COUNT];

    /// <summary>
    /// 수신된 프레임이 디코딩되어 소비자에게 전달되기 전 임시 저장소 역할.( 반환 데이터 배열)
    /// Channel고성능 메시지 큐 시스템
    /// 디코딩 완료 수신 프레임 채널.
    /// ReadAllAsync() 비동기 열거 경로를 지원합니다.
    /// Passive: PushReceived() 에서 투입
    /// RequestResponse: DispatchAsync PeriodicRead 경로에서 투입
    /// </summary>
    private readonly Channel<byte[]> _receiveChannel;

    private readonly INetTransport _transport;
    private readonly INetProtocol _protocol;
    private readonly NetDeviceConfigBase _cfg;
    private readonly NetStatistics _stats;
    private readonly NetConnectionManager _connMgr;

    private Task? _processTask;
    private volatile bool _disposed;

    #endregion

    #region §2 ─ 생성자

    internal NetDispatchPipeline(INetTransport transport,
                                  INetProtocol protocol,
                                  NetDeviceConfigBase cfg,
                                  NetStatistics stats,
                                  NetConnectionManager connMgr)
    {
        _transport = transport;
        _protocol = protocol;
        _cfg = cfg;
        _stats = stats;
        _connMgr = connMgr;

        for (int i = 0; i < CHANNEL_COUNT; i++)
        {
            /// Write 채널은 Bounded (낮은 우선순위 패킷이 너무 많이 쌓이는 것을 방지), 나머지는 Unbounded
            _channels[i] = Channel.CreateUnbounded<NetPacket>(
            new UnboundedChannelOptions { SingleReader = false });
        }

        _receiveChannel = cfg.ReceiveChannelCapacity > 0 ?
                            Channel.CreateBounded<byte[]>(
                                new BoundedChannelOptions(cfg.ReceiveChannelCapacity)
                                {
                                    FullMode = BoundedChannelFullMode.DropOldest  // 채널 용량 초과 시 오래된 항목 제거
                                })
                        : Channel.CreateUnbounded<byte[]>(); // 수신 채널은 기본적으로 무제한 (양수 용량 설정 시 제한)
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
        if (!_protocol.TryDecode(raw, out var decoded))
        {
            return;
        }
        // 디코딩 성공 → 통계 기록 + 채널 투입 + 이벤트 발생
        _stats.RecordReceived();

        // Receive 채널 투입
        _receiveChannel.Writer.TryWrite(decoded);

        // 이벤트 발생
        FrameReceived?.Invoke(_cfg.DeviceId, decoded);
    }

    /// <summary>소비자 루프를 시작합니다.</summary>
    internal void Start(CancellationToken ct)
        => _processTask = Task.Run(() => ProcessLoopAsync(ct), ct);

    /// <summary>모든 채널을 닫고 소비자 루프 종료를 기다립니다.</summary>
    internal async Task StopAsync()
    {
        foreach (var ch in _channels)
        {
            ch.Writer.TryComplete();
        }

        if (_processTask is not null)
        {
            try
            {
                await _processTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    #endregion

    #region §5 ─ 소비자 루프

    /// <summary>
    /// 우선순위별 채널에서 패킷을 비블로킹으로 TryRead → DispatchAsync 호출.
    /// 모든 채널이 비어있으면 최대 50ms 대기 (채널 중 하나라도 데이터가 들어오면 즉시 깨어남).
    /// </summary>
    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Critical(0) → Write(1) → Read(2) → Low(3) 비블로킹 순차 확인
            NetPacket? packet = null;
            for (int i = 0; i < CHANNEL_COUNT; i++)
            {
                /// 높은 우선순위 채널에 패킷이 있으면 낮은 채널은 처리하지 않음
                /// 패킷을 읽으면 즉시 탈출하여 다음 단계로 이동
                if (_channels[i].Reader.TryRead(out packet))
                {
                    break;
                }
            }

            if (packet is null)
            {
                // 모든 채널 비어있음 → 최대 50ms 대기
                try
                {
                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    waitCts.CancelAfter(50);

                    await Task.WhenAny(
                        _channels[0].Reader.WaitToReadAsync(waitCts.Token).AsTask(), // WaitToReadAsync : 데이터가 들어오면 처리
                        _channels[1].Reader.WaitToReadAsync(waitCts.Token).AsTask(), // 채널 중 하나라도 데이터가 들어오면 즉시 깨어남
                        _channels[2].Reader.WaitToReadAsync(waitCts.Token).AsTask(), // 최대 50ms 대기
                        _channels[3].Reader.WaitToReadAsync(waitCts.Token).AsTask()
                    ).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested) break;
                }
                continue;
            }

            // 패킷 처리 (DispatchAsync) - 통신 오류 발생 시 예외 처리 포함
            await DispatchAsync(packet, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 패킷 종류별 처리 핵심 로직.
    ///
    /// <b>패킷별 처리 분기:</b>
    /// <list type="table">
    ///   <item>
    ///     <term>Write / Retry</term>
    ///     <description>Transport.WriteAsync 만 호출. 응답 없음.</description>
    ///   </item>
    ///   <item>
    ///     <term>Request (RequestAsync 호출 시)</term>
    ///     <description>
    ///       WriteAsync → <b>ReadAsync</b> → TryDecode → TCS.SetResult.
    ///       ReadAsync 는 요청을 보내고 응답을 기다리기 위해 필요합니다.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>PeriodicRead (NetScheduler 자동 생성)</term>
    ///     <description>
    ///       WriteAsync (Read 명령 전송) → <b>ReadAsync</b> (응답 수신) → DeviceFrameReceived.
    ///       ReadAsync 는 ReadCommand 전송 후 응답을 수신하기 위해 필요합니다.
    ///       <b>Passive 모드에서는 PeriodicRead 패킷이 생성되지 않으므로 이 경로는 실행되지 않습니다.</b>
    ///     </description>
    ///   </item>
    /// </list>
    /// </summary>
    private async Task DispatchAsync(NetPacket packet, CancellationToken ct)
    {
        // 연결 상태 재확인 (큐 대기 중 끊어졌을 수 있음)
        if (!_connMgr.IsConnected)
        {
            // Log(LogLevel.Warn, $"Dispatch 스킵 (Mode={packet.Mode}) — {_connMgr.State}");
            packet.Tcs?.TrySetResult(NetResult.Fail($"[{_cfg.DeviceName}] 연결 없음"));
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // ── ① 모든 패킷 공통: 데이터 전송 ──────────────────────────
            await _transport.WriteAsync(packet.Data, ct).ConfigureAwait(false);
            _stats.RecordSent();


            // ── ② Request 패킷 전용: 응답 수신 + TCS 완료 ─────────────
            // RequestAsync 호출 시 생성된 패킷
            // ReadAsync 가 필요한 이유:
            //   요청 프레임을 전송한 후 서버 응답이 올 때까지 기다려야 함
            //   응답이 도착해야 RequestAsync await 가 완료됨
            if (packet.Mode == PacketMode.Request &&
                packet.Tcs is not null)
            {
                var raw = await _transport.ReadAsync(ct).ConfigureAwait(false);

                sw.Stop();
                _stats.RecordResponse(sw.ElapsedMilliseconds);
                _stats.RecordReceived();

                if (_protocol.TryDecode(raw, out var decoded))
                    packet.Tcs.TrySetResult(NetResult.Ok(decoded));
                else
                    packet.Tcs.TrySetResult(NetResult.Fail("프로토콜 디코딩 실패"));

                return;
            }

            // ── ③ PeriodicRead 패킷 전용: 응답 수신 + 이벤트 발생 ─────
            // NetScheduler 가 ReadCommands 를 주기적으로 전송할 때 생성
            // ReadAsync 가 필요한 이유:
            //   모드버스 등 RequestResponse 장비는 질의 후 응답이 옴
            //   응답을 수신해야 DeviceFrameReceived 이벤트로 전달 가능
            // ★ Passive 모드에서는 이 분기에 진입하지 않음
            //   (NetScheduler.PeriodicInterval=Zero → PeriodicRead 패킷 미생성)
            if (packet.Mode == PacketMode.PeriodicRead)
            {
                var raw = await _transport.ReadAsync(ct).ConfigureAwait(false);

                _stats.RecordReceived();
                if (_protocol.TryDecode(raw, out var decoded))
                {
                    _receiveChannel.Writer.TryWrite(decoded);
                    FrameReceived?.Invoke(_cfg.DeviceId, decoded);
                }

                return;
            }

            // ── ④ Write / Retry 패킷: 전송 완료 (위에서 이미 처리됨) ──
            // WriteAsync 만 호출하고 종료 (응답 대기 없음)
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            //Log(LogLevel.Error, $"통신 오류 (Mode={packet.Mode}): {ex.Message}");

            /// 통신 오류 발생 → 통계 기록 + 이벤트 발생 + Tcs 실패 설정 + 재접속 시도 (Write 패킷인 경우 재전송 페이로드 보존)
            packet.Tcs?.TrySetResult(NetResult.Fail(ex));

            // Write 재전송 페이로드 보존
            byte[]? retryPayload = null;

            if (_cfg.IsRetryEnabled &&
                _cfg.RetryTarget.HasFlag(RetryTarget.Write) &&
                packet.Mode is PacketMode.Write or PacketMode.Retry &&
                packet.RetryCount < _cfg.MaxRetries)
            {
                _stats.RecordWriteRetry();
                retryPayload = packet.ToRetry().Data;
            }

            _ = _connMgr.HandleErrorAsync(ex, retryPayload,
                                            async (data, t) =>
                                                await EnqueueAsync(
                                                        NetPacket.CreateWrite(data, NetPriority.Critical, t), t)
                                            .ConfigureAwait(false),
                                            ct);
        }
    }

    /*
    private void Log(LogLevel lv, string msg)
        => LogManager.Instance.AddLog(lv, _cfg.DeviceName, msg);
    */
    #endregion

    #region §6 ─ IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
    #endregion
}