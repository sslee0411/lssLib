// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Channels/RequestResponseChannel.cs
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 요청-응답 채널 (RequestResponse 모드).
/// ReadCommands 를 주기적으로 전송하고, Write 는 Read 보다 항상 우선 처리됩니다.
/// </summary>
/// <remarks>
/// <b>우선순위:</b>
/// Critical(0) &gt; Write(1) &gt; Read(2) &gt; Low/Heartbeat(3)
///
/// <b>조립 예시 — Modbus RTU (Serial, SequenceMode=1):</b>
/// <code>
/// var cfg = new SerialDeviceConfig(1, "Modbus-PLC", "COM3", 9600);
/// // cfg.SequenceMode == 1 (Sequential) ← RS-485 기본값, 버스 충돌 방지
///
/// cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]); // 슬레이브 1
/// cfg.AddReadCommand([0x02, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]); // 슬레이브 2
/// // → 슬레이브 1 완료 후 슬레이브 2 순서 보장
///
/// await using var channel = new RequestResponseChannel(
///     cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) => ProcessModbus(id, frame);
/// channel.DeviceStateChanged  += (id, state) =>
///     Dispatcher.InvokeAsync(() => BtnSend.IsEnabled = state == NetState.Connected);
/// channel.DeviceErrorOccurred += (id, ex) =>
///     LogManager.Instance.Error("Modbus-PLC", ex.Message);
///
/// await channel.StartAsync();
///
/// // Write (Read 보다 항상 우선)
/// await channel.WriteAsync(setpointFrame, NetPriority.Write);
///
/// // 단발 요청-응답
/// NetResult r = await channel.RequestAsync(queryFrame, TimeSpan.FromMilliseconds(500));
/// if (r.IsOk)
/// {
///     // lssLib.Binary 파싱 연동
///     // var result = r.Data!.ToParser().Parse(ModbusSchema.Default);
/// }
/// </code>
///
/// <b>조립 예시 — Modbus TCP (SequenceMode=0, 병렬):</b>
/// <code>
/// var cfg = new TcpDeviceConfig(2, "PLC-TCP", "192.168.1.10", 502)
/// {
///     SequenceMode     = NetDeviceConfig.SequenceModes.Parallel,  // 0: 병렬
///     PeriodicInterval = TimeSpan.FromMilliseconds(100)
/// };
/// cfg.AddReadCommand(modbusReadCmd1);
/// cfg.AddReadCommand(modbusReadCmd2);
/// // → 두 ReadCommand 동시 투입 (Task.WhenAll)
/// </code>
///
/// <b>슬라이딩 윈도우 예시 (SequenceMode=3):</b>
/// <code>
/// var cfg = new TcpDeviceConfig(3, "Multi-PLC", "192.168.1.20", 502)
/// {
///     SequenceMode = 3   // 최대 3개 동시, 전체 순서 유지
/// };
/// // ReadCommands 6개 → [1,2,3] 동시 → [4,5,6] 동시
/// </code>
///
/// <b>파생 클래스 패턴 (동적 Read 프레임 생성):</b>
/// <code>
/// public class ModbusChannel : RequestResponseChannel
/// {
///     private byte _slaveAddr = 0x01;
///
///     public ModbusChannel(SerialDeviceConfig cfg)
///         : base(cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true) { }
///
///     public void SetSlave(byte addr) => _slaveAddr = addr;
/// }
/// </code>
/// </remarks>
public class RequestResponseChannel : NetChannelBase
{
    /// <inheritdoc/>
    public override NetMode Mode => NetMode.RequestResponse;

    /// <summary>요청-응답 채널을 초기화합니다.</summary>
    /// <param name="config">장비 설정 (ReadCommands / PeriodicInterval / SequenceMode 포함)</param>
    /// <param name="transport">전송 계층. XxxTransport.FromConfig(cfg) 권장.</param>
    /// <param name="protocol">프로토콜 계층.</param>
    /// <param name="autoRegister">true 이면 NetDeviceRegistry 자동 등록.</param>
    public RequestResponseChannel(
        NetDeviceConfig config,
        INetTransport transport,
        INetProtocol protocol,
        bool autoRegister = false)
        : base(config, transport, protocol, autoRegister) { }
}