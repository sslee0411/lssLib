// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetScheduler.cs
//  역할: 주기 Read + Heartbeat 루프 전담 (Pause/Resume 제어 가능)
//
//  ┌─ Scheduler 의 역할 범위 ─────────────────────────────────────────┐
//  │                                                                   │
//  │  NetScheduler = "언제 패킷을 Pipeline 에 넣을지" 만 담당         │
//  │                                                                   │
//  │  ┌─ PeriodicRead 루프 ─────────────────────────────────────────┐ │
//  │  │  RequestResponse 전용                                        │ │
//  │  │  PeriodicInterval 마다 ReadCommands 를 Pipeline 에 투입      │ │
//  │  │  → Pipeline.DispatchAsync → WriteAsync → ReadAsync → 이벤트 │ │
//  │  │                                                              │ │
//  │  │  Passive 모드에서는 PeriodicInterval=Zero 로 설정하므로      │ │
//  │  │  이 루프는 즉시 return 하고 실행되지 않음                    │ │
//  │  └──────────────────────────────────────────────────────────────┘ │
//  │                                                                   │
//  │  ┌─ Heartbeat 루프 ────────────────────────────────────────────┐ │
//  │  │  선택적 기능 (HeartbeatInterval=Zero 이면 비활성)            │ │
//  │  │  HeartbeatInterval 마다 INetProtocol.BuildHeartbeat() 전송   │ │
//  │  │  → Low(3) 우선순위로 Pipeline 투입 (통신 공백에만 전송)      │ │
//  │  │                                                              │ │
//  │  │  목적: 연결 유지 확인 (Keep-Alive 역할)                      │ │
//  │  │  TCP: HeartbeatInterval=TimeSpan.FromSeconds(30) 권장        │ │
//  │  │  Serial: HeartbeatInterval=TimeSpan.Zero (기본, 비활성)      │ │
//  │  └──────────────────────────────────────────────────────────────┘ │
//  │                                                                   │
//  │  ★ Scheduler 는 ReadAsync 를 직접 호출하지 않습니다.             │
//  │    패킷을 만들어 Pipeline 에 투입하는 것이 전부입니다.            │
//  │    실제 WriteAsync/ReadAsync 는 Pipeline.DispatchAsync 에서 처리. │
//  └───────────────────────────────────────────────────────────────────┘
//
//  ┌─ Heartbeat 사용 가이드 ──────────────────────────────────────────┐
//  │                                                                   │
//  │  1. Config 설정                                                   │
//  │     cfg.HeartbeatInterval = TimeSpan.FromSeconds(30);            │
//  │                                                                   │
//  │  2. BinaryProtocol.BuildHeartbeat() → 빈 페이로드 프레임 반환    │
//  │     RawProtocol.BuildHeartbeat()   → null 반환 (건너뜀)          │
//  │                                                                   │
//  │  3. 커스텀 Heartbeat 프레임이 필요하면 INetProtocol 구현:        │
//  │     public byte[]? BuildHeartbeat()                               │
//  │         => BuildFrame(new byte[]{ 0x00 }); // 커스텀 Keep-Alive  │
//  │                                                                   │
//  │  4. 동작:                                                         │
//  │     - Write/Read 패킷이 있으면 Heartbeat 는 건너뜀               │
//  │       (Low=3 우선순위 → 통신 공백에만 전송)                      │
//  │     - Paused 상태(재접속 중)에서는 전송 안 함                    │
//  └───────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

// using lssLib.Log;
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
/// <b>Heartbeat:</b> <c>HeartbeatInterval</c> 마다
/// <see cref="INetProtocol.BuildHeartbeat"/> 결과를 Low 우선순위로 투입합니다.
/// null 이면 해당 주기를 건너뜁니다.
/// </para>
/// </remarks>
internal sealed class NetScheduler
{
    #region §1 ─ 필드

    private readonly NetDeviceConfigBase _cfg;
    private readonly NetDispatchPipeline _pipeline;
    private readonly INetProtocol _protocol;
    private readonly Func<bool> _isConnected;

    private volatile bool _paused;
    private Task? _runTask;

    #endregion

    #region §2 ─ 생성자

    internal NetScheduler(NetDeviceConfigBase cfg, NetDispatchPipeline pipeline,
        INetProtocol protocol, Func<bool> isConnected)
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
    internal void Pause()
    {
        _paused = true;
        //LogManager.Instance.Debug(_cfg.DeviceName, "[Scheduler] 일시 정지");
    }

    /// <summary>
    /// 스케줄러를 재개합니다.
    /// <para>재접속 성공 후 NetConnectionManager.Reconnected 이벤트에서 자동 호출됩니다.</para>
    /// </summary>
    internal void Resume()
    {
        _paused = false;
        //LogManager.Instance.Debug(_cfg.DeviceName, "[Scheduler] 재개");
    }

    /// <summary>루프 종료를 기다립니다.</summary>
    internal Task WaitAsync() => _runTask ?? Task.CompletedTask;

    #endregion

    #region §4 ─ 루프

    /// <summary>
    /// 주기 Read 루프와 Heartbeat 루프를 병렬로 실행합니다.
    ///
    /// <b>두 루프의 역할:</b>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <c>RunPeriodicReadAsync</c>: ReadCommands 를 Pipeline 에 투입.<br/>
    ///       Scheduler 가 하는 일은 "언제" 투입할지 결정하는 것뿐입니다.<br/>
    ///       실제 WriteAsync/ReadAsync 는 Pipeline.DispatchAsync 에서 처리합니다.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <c>RunHeartbeatAsync</c>: HeartbeatInterval 마다 Keep-Alive 프레임 투입.<br/>
    ///       Low(3) 우선순위 → Write/Read 패킷이 없을 때만 실제 전송됩니다.
    ///     </description>
    ///   </item>
    /// </list>
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
        => await Task.WhenAll(
            RunPeriodicReadAsync(ct),
            RunHeartbeatAsync(ct))
        .ConfigureAwait(false);

    /// <summary>
    /// 주기 Read 루프.
    ///
    /// <b>Passive 모드:</b>
    ///   PeriodicInterval=Zero → 즉시 return. 루프 실행 안 함.
    ///   수신은 TcpTransport.PassiveReceiveLoopAsync 또는 Serial.DataReceived 가 담당.
    ///
    /// <b>RequestResponse 모드:</b> PeriodicInterval 마다 ReadCommands 투입.
    ///
    /// ★ Scheduler 는 "언제 투입할지"만 결정합니다.
    ///   실제 WriteAsync+ReadAsync 처리는 Pipeline.DispatchAsync 에서 수행합니다.
    ///   IsSequential=true  → foreach 순차 투입 (RS-485/Modbus)
    ///   IsSequential=false → Task.WhenAll 병렬 투입 (TCP 다중 요청)
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

                if (_paused || !_isConnected())
                {
                    //LogManager.Instance.Debug(_cfg.DeviceName, $"[Scheduler] 주기 Read 스킵 — Paused={_paused}");
                    continue;
                }

                var cmds = _cfg.ReadCommands;
                if (cmds.Count == 0) continue;

                if (_cfg.IsSequential)
                {
                    // 순차 투입: 이전 패킷이 처리될 때까지 대기하지 않음
                    // (Pipeline 의 SingleReader=false 채널에 순서대로 투입)
                    foreach (var cmd in cmds)
                    {
                        await _pipeline.EnqueueAsync(
                            NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    // 병렬 투입: 모든 ReadCommands 를 동시에 채널에 투입
                    var tasks = cmds.Select(cmd =>
                        _pipeline.EnqueueAsync(
                            NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct).AsTask());
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ex.Message.ToString();
                // LogManager.Instance.Warn(_cfg.DeviceName, $"[Scheduler] 주기 Read 오류: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Heartbeat 루프.
    ///
    /// <b>HeartbeatInterval=Zero (기본값):</b> 즉시 return. 비활성.
    ///
    /// <b>HeartbeatInterval 설정 시:</b>
    ///   INetProtocol.BuildHeartbeat() 결과를 Low(3) 우선순위로 투입.
    ///   null 반환 시 해당 주기 건너뜀.
    ///   실제 전송 시점: Pipeline 의 Write/Read 채널이 모두 비어있을 때.
    ///
    /// <b>Heartbeat 설정 예시:</b>
    /// <code>
    /// // TCP 연결 유지 (30초 주기)
    /// var cfg = new TcpDeviceConfig(1, "PLC", "192.168.1.10", 502)
    /// {
    ///     HeartbeatInterval = TimeSpan.FromSeconds(30)
    ///     // TcpDeviceConfig 기본값이 30초이므로 생략 가능
    /// };
    ///
    /// // Serial 비활성 (기본값)
    /// var cfg = new SerialDeviceConfig(2, "Sensor", "COM3", 9600)
    /// {
    ///     HeartbeatInterval = TimeSpan.Zero  // 기본값 — 생략 가능
    /// };
    ///
    /// // 커스텀 Heartbeat 프레임: BinaryProtocol.BuildHeartbeat() 재정의
    /// // 기본: Encode(Array.Empty<byte>()) → 빈 데이터 프레임
    /// // 커스텀 예시:
    /// // public byte[]? BuildHeartbeat()
    /// //     => BuildFrame(new byte[] { 0x00 });  // Keep-Alive 코드
    /// <b>IsHeartbeatAcknowledged=false (기본):</b>
    ///   _stream.WriteAsync(hb) 만 수행 — Keep-Alive 역할.
    ///
    /// <b>IsHeartbeatAcknowledged=true:</b>
    ///   _stream.WriteAsync(hb) + _stream.ReadAsync() → DeviceFrameReceived 이벤트.
    ///   서버가 Heartbeat 에 ACK 를 보내는 경우 사용합니다.
    ///
    /// <b>설정 예시:</b>
    /// <code>
    /// // Heartbeat 전송만 (Keep-Alive)
    /// cfg.HeartbeatInterval       = TimeSpan.FromSeconds(30);
    /// cfg.IsHeartbeatAcknowledged = false;  // 기본값
    ///
    /// // Heartbeat ACK 수신
    /// cfg.HeartbeatInterval       = TimeSpan.FromSeconds(30);
    /// cfg.IsHeartbeatAcknowledged = true;
    ///
    /// // ACK 처리
    /// channel.DeviceFrameReceived += (id, frame) =>
    /// {
    ///     if (IsHeartbeatAck(frame)) Console.WriteLine("Heartbeat ACK");
    ///     else                       ProcessSensorData(frame);
    /// };
    /// </code>
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

                if (_paused || !_isConnected())
                {
                    // LogManager.Instance.Debug(_cfg.DeviceName, "[Scheduler] Heartbeat 스킵");
                    continue;
                }

                // INetProtocol.BuildHeartbeat(): null 이면 이번 주기 건너뜀
                var hb = _protocol.BuildHeartbeat();
                if (hb is null) continue;

                // IsHeartbeatAcknowledged 에 따라 응답 수신 여부 결정
                var packet = NetPacket.CreateHeartbeat(hb, ct, _cfg.IsHeartbeatAcknowledged);
                await _pipeline.EnqueueAsync(packet, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ex.Message.ToString();
                // LogManager.Instance.Warn(_cfg.DeviceName, $"[Scheduler] Heartbeat 오류: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 주기 Read 명령을 PeriodicRead 패킷으로 만들어 Pipeline 에 투입합니다.
    /// 실제 WriteAsync/ReadAsync 처리는 Pipeline.DispatchAsync 에서 수행합니다.
    /// </summary>
    private async Task EnqueueReadAsync(byte[] cmd, CancellationToken ct)
        => await _pipeline.EnqueueAsync(
                    NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct)
                .ConfigureAwait(false);

    #endregion
}