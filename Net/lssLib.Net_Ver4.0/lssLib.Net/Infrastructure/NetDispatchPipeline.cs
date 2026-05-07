// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetDispatchPipeline.cs
//  역할: 우선순위별 Channel[4] 분리 파이프라인 (lock 없음)
//
//  v3 vs v4:
//    v3: Channel<NetPacket>(ingress) → lock + PriorityQueue → 소비자
//    v4: Channel[0]Critical ─┐
//        Channel[1]Write    ─┤→ ProcessLoopAsync (높은 우선순위 먼저 TryRead)
//        Channel[2]Read     ─┤
//        Channel[3]Low      ─┘
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
/// </remarks>
internal sealed class NetDispatchPipeline : IAsyncDisposable
{
    #region §1 ─ 필드

    private const int CHANNEL_COUNT = 4;

    /// <summary>
    /// 우선순위별 패킷 채널 배열 (Critical, Write, Read, Low).
    /// Write 영역
    /// </summary>
    private readonly Channel<NetPacket>[] _channels = new Channel<NetPacket>[CHANNEL_COUNT];
    
    /// <summary>
    /// 수신된 프레임이 디코딩되어 소비자에게 전달되기 전 임시 저장소 역할.( 반환 데이터 배열)
    /// Channel고성능 메시지 큐 시스템
    /// Receive영역
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

        for (int i = 0; i < CHANNEL_COUNT; i++) {
            /// Write 채널은 Bounded (낮은 우선순위 패킷이 너무 많이 쌓이는 것을 방지), 나머지는 Unbounded
            _channels[i] = Channel.CreateUnbounded<NetPacket>(new UnboundedChannelOptions { SingleReader = false });
        }

        _receiveChannel = cfg.ReceiveChannelCapacity > 0 ? 
                            Channel.CreateBounded<byte[]>(
                                new BoundedChannelOptions(cfg.ReceiveChannelCapacity){ 
                                    FullMode = BoundedChannelFullMode.DropOldest  // 채널 용량 초과 시 오래된 항목 제거
                                })
                        : Channel.CreateUnbounded<byte[]>(); // 수신 채널은 기본적으로 무제한 (양수 용량 설정 시 제한)
    }

    #endregion

    #region §3 ─ 이벤트

    /// <summary>프레임 수신·디코딩 완료 시 발생. (DeviceId, decodedFrame)</summary>
    public event Action<int, byte[]>? FrameReceived;

    #endregion

    #region §4 ─ 공개 API

    /// <summary>패킷을 우선순위 채널에 투입합니다.</summary>
    internal ValueTask EnqueueAsync(NetPacket packet, CancellationToken ct)
        => _channels[(int)packet.Priority].Writer.WriteAsync(packet, ct);

    /// <summary>수신 채널에서 디코딩된 프레임을 비동기 열거합니다.</summary>
    internal IAsyncEnumerable<byte[]> ReadAllAsync(CancellationToken ct)
        => _receiveChannel.Reader.ReadAllAsync(ct);

    /// <summary>Transport.DataReceived 핸들러에서 호출. 프로토콜 디코딩 → 채널 + 이벤트.</summary>
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
            try { 
                await _processTask.ConfigureAwait(false); 
            }
            catch (OperationCanceledException) { 
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
    /// Write Channel 패킷을 처리하는 핵심 로직.
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
            await _transport.WriteAsync(packet.Data, ct).ConfigureAwait(false);
            _stats.RecordSent();

            // Request 패킷이면서 Tcs가 있는 경우 → 응답 대기 + 프로토콜 디코딩 + Tcs 결과 설정
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
            }

            // PeriodicRead 패킷인 경우 → 응답 대기 + 프로토콜 디코딩 + Receive 채널 투입 + 이벤트 발생 (Tcs 없음)
            if (packet.Mode == PacketMode.PeriodicRead)
            {
                var raw = await _transport.ReadAsync(ct).ConfigureAwait(false);

                _stats.RecordReceived();
                if (_protocol.TryDecode(raw, out var decoded))
                {
                    _receiveChannel.Writer.TryWrite(decoded);
                    FrameReceived?.Invoke(_cfg.DeviceId, decoded);
                }
            }
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

            _ = _connMgr.HandleErrorAsync(  ex, retryPayload,
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