// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetScheduler.cs  [v5.1]
//  역할: 주기 Read + Heartbeat 루프 전담 (Pause/Resume 제어)
//
//  ┌─ Scheduler 의 역할 범위 ────────────────────────────────────────┐
//  │                                                                  │
//  │  NetScheduler = "언제 패킷을 Pipeline 에 넣을지"만 담당          │
//  │                                                                  │
//  │  ┌─ PeriodicRead 루프 ───────────────────────────────────────┐  │
//  │  │  RequestResponse 전용                                      │  │
//  │  │  PeriodicInterval 마다 ReadCommands 를 Pipeline 에 투입    │  │
//  │  │  → Pipeline.DispatchAsync → WriteAsync → ReadAsync → 이벤 │  │
//  │  │                                                            │  │
//  │  │  Passive 모드: PeriodicInterval=Zero → 즉시 return        │  │
//  │  └────────────────────────────────────────────────────────────┘  │
//  │                                                                  │
//  │  ┌─ Heartbeat 루프 ─────────────────────────────────────────┐   │
//  │  │  선택적 기능 (HeartbeatInterval=Zero 이면 비활성)          │   │
//  │  │  HeartbeatInterval 마다 BuildHeartbeat() 전송              │   │
//  │  │  → Low(3) 최저 우선순위 → Write/Read 공백에만 전송        │   │
//  │  └────────────────────────────────────────────────────────────┘  │
//  │                                                                  │
//  │  ★ Scheduler 는 ReadAsync 를 직접 호출하지 않습니다.            │
//  │    패킷을 만들어 Pipeline 에 투입하는 것이 전부입니다.            │
//  └──────────────────────────────────────────────────────────────────┘
//
//  ┌─ SequenceMode 분기 (v5.1 신규) ─────────────────────────────────┐
//  │  0 (Parallel)   → Task.WhenAll — 모든 커맨드 동시 투입          │
//  │  1 (Sequential) → foreach     — 1개씩 순서대로 투입             │
//  │  N ≥ 2          → SemaphoreSlim(N) — 슬라이딩 윈도우 N개 동시  │
//  └──────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 주기 Read 루프와 Heartbeat 루프를 전담합니다.
/// </summary>
/// <remarks>
/// <para>
/// <b>Passive 모드:</b> <c>PeriodicInterval=Zero</c> 로 설정하면
/// <see cref="RunPeriodicReadAsync"/> 는 즉시 return 합니다.
/// 수신은 Transport.DataReceived → PushReceived 경로가 담당합니다.
/// </para>
/// <para>
/// <b>RequestResponse 모드:</b> <c>PeriodicInterval</c> 마다
/// ReadCommands 를 <see cref="NetDispatchPipeline"/> 에 투입합니다.
/// </para>
/// <para>
/// <b>SequenceMode 별 동작 (v5.1):</b>
/// </para>
/// <list type="table">
///   <item>
///     <term>0 (Parallel)</term>
///     <description>ReadCommands 전체를 Task.WhenAll 로 동시 투입. TCP/UDP 권장.</description>
///   </item>
///   <item>
///     <term>1 (Sequential)</term>
///     <description>ReadCommands 를 1개씩 순서대로 투입. RS-485/Modbus RTU 필수.</description>
///   </item>
///   <item>
///     <term>N ≥ 2 (Window)</term>
///     <description>SemaphoreSlim(N) 으로 N개씩 동시 허용, 전체 순서 유지.</description>
///   </item>
/// </list>
/// <para>
/// <b>Heartbeat:</b> <c>HeartbeatInterval</c> 마다
/// <see cref="INetProtocol.BuildHeartbeat"/> 결과를 Low(3) 우선순위로 투입합니다.
/// null 이면 해당 주기를 건너뜁니다.
/// </para>
/// </remarks>
internal sealed class NetScheduler
{
    #region §1 ─ 필드

    private readonly NetDeviceConfig _cfg;
    private readonly NetDispatchPipeline _pipeline;
    private readonly INetProtocol _protocol;
    private readonly Func<bool> _isConnected;

    private volatile bool _paused;
    private Task? _runTask;

    #endregion

    #region §2 ─ 생성자

    internal NetScheduler(
        NetDeviceConfig cfg,
        NetDispatchPipeline pipeline,
        INetProtocol protocol,
        Func<bool> isConnected)
    {
        _cfg = cfg;
        _pipeline = pipeline;
        _protocol = protocol;
        _isConnected = isConnected;
    }

    #endregion

    #region §3 ─ 제어

    /// <summary>스케줄러를 시작합니다.</summary>
    internal void Start(CancellationToken ct)
        => _runTask = Task.Run(() => RunAsync(ct), ct);

    /// <summary>
    /// 스케줄러를 일시 정지합니다.
    /// <para>재접속 시작 시 NetChannelBase.OnStateChanged 에서 자동 호출됩니다.</para>
    /// </summary>
    internal void Pause() => _paused = true;

    /// <summary>
    /// 스케줄러를 재개합니다.
    /// <para>재접속 성공 후 NetConnectionManager.Reconnected 이벤트에서 자동 호출됩니다.</para>
    /// </summary>
    internal void Resume() => _paused = false;

    /// <summary>루프 종료를 기다립니다.</summary>
    internal Task WaitAsync() => _runTask ?? Task.CompletedTask;

    #endregion

    #region §4 ─ 루프

    /// <summary>주기 Read 루프와 Heartbeat 루프를 병렬로 실행합니다.</summary>
    private Task RunAsync(CancellationToken ct)
        => Task.WhenAll(RunPeriodicReadAsync(ct), RunHeartbeatAsync(ct));

    /// <summary>
    /// 주기 Read 루프. SequenceMode 에 따라 투입 방식 결정.
    /// <para>
    /// <b>Passive 모드 (PeriodicInterval=Zero):</b> 즉시 return. 루프 실행 안 함.<br/>
    /// 수신은 TcpTransport.PassiveReceiveLoopAsync 또는 Serial.DataReceived 가 담당.
    /// </para>
    /// <para>
    /// <b>RequestResponse 모드:</b> PeriodicInterval 마다 ReadCommands 투입.<br/>
    /// ★ Scheduler 는 "언제 투입할지"만 결정합니다.
    ///    실제 WriteAsync+ReadAsync 처리는 Pipeline.DispatchAsync 에서 수행합니다.
    /// </para>
    /// </summary>
    private async Task RunPeriodicReadAsync(CancellationToken ct)
    {
        // Passive 모드 또는 PeriodicInterval=Zero → 즉시 종료
        if (_cfg.PeriodicInterval == TimeSpan.Zero) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cfg.PeriodicInterval, ct).ConfigureAwait(false);

                if (_paused || !_isConnected()) continue;

                var cmds = _cfg.ReadCommands;
                if (cmds.Count == 0) continue;

                // ── SequenceMode 분기 ──────────────────────────────────
                switch (_cfg.SequenceMode)
                {
                    // ① Parallel (0) — 모든 커맨드 동시 투입
                    case NetDeviceConfig.SequenceModes.Parallel:
                        await Task.WhenAll(cmds.Select(cmd =>
                            _pipeline.EnqueueAsync(
                                NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct)
                                .AsTask()))
                            .ConfigureAwait(false);
                        break;

                    // ② Sequential (1) — 1개씩 순서대로 투입 (RS-485 버스 충돌 방지)
                    case NetDeviceConfig.SequenceModes.Sequential:
                        foreach (var cmd in cmds)
                            await _pipeline.EnqueueAsync(
                                NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct)
                                .ConfigureAwait(false);
                        break;

                    // ③ Window(N≥2) — SemaphoreSlim 슬라이딩 윈도우
                    //    N개씩 동시 허용, 전체 순서 유지
                    default:
                        int windowSize = _cfg.SequenceMode;
                        var sem = new SemaphoreSlim(windowSize, windowSize);
                        var tasks = cmds.Select(async cmd =>
                        {
                            await sem.WaitAsync(ct).ConfigureAwait(false);
                            try
                            {
                                await _pipeline.EnqueueAsync(
                                    NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct)
                                    .ConfigureAwait(false);
                            }
                            finally { sem.Release(); }
                        });
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                        break;
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* 오류 무시 — 다음 주기 재시도 */ }
        }
    }

    /// <summary>
    /// Heartbeat 루프.
    /// <para><b>HeartbeatInterval=Zero (기본값):</b> 즉시 return. 비활성.</para>
    /// <para>
    /// <b>HeartbeatInterval 설정 시:</b>
    /// INetProtocol.BuildHeartbeat() 결과를 Low(3) 우선순위로 투입.
    /// null 반환 시 해당 주기 건너뜀.
    /// 실제 전송 시점: Pipeline 의 Write/Read 채널이 모두 비어있을 때.
    /// </para>
    /// <para>
    /// <b>IsHeartbeatAcknowledged=false (기본):</b> 전송만 수행 (Keep-Alive 역할).<br/>
    /// <b>IsHeartbeatAcknowledged=true:</b> WriteAsync + ReadAsync → DeviceFrameReceived 이벤트.
    /// </para>
    /// <example><code>
    /// // TCP 연결 유지 (30초 주기, ACK 없음)
    /// cfg.HeartbeatInterval       = TimeSpan.FromSeconds(30);
    /// cfg.IsHeartbeatAcknowledged = false;   // 기본값
    ///
    /// // Heartbeat ACK 수신이 필요한 경우
    /// cfg.HeartbeatInterval       = TimeSpan.FromSeconds(30);
    /// cfg.IsHeartbeatAcknowledged = true;
    ///
    /// channel.DeviceFrameReceived += (id, frame) =>
    /// {
    ///     if (IsHeartbeatAck(frame)) Console.WriteLine("Heartbeat ACK 수신");
    ///     else                       ProcessSensorData(frame);
    /// };
    /// </code></example>
    /// </summary>
    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        // HeartbeatInterval=Zero → 비활성
        if (_cfg.HeartbeatInterval == TimeSpan.Zero) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cfg.HeartbeatInterval, ct).ConfigureAwait(false);

                if (_paused || !_isConnected()) continue;

                // BuildHeartbeat() = null 이면 이번 주기 건너뜀 (RawProtocol 기본)
                var hb = _protocol.BuildHeartbeat();
                if (hb is null) continue;

                // IsHeartbeatAcknowledged 에 따라 응답 수신 여부 결정
                await _pipeline.EnqueueAsync(
                    NetPacket.CreateHeartbeat(hb, ct, _cfg.IsHeartbeatAcknowledged), ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* 오류 무시 */ }
        }
    }

    #endregion
}