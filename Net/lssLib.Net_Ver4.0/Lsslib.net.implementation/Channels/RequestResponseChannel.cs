// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Channels/RequestResponseChannel.cs
// ══════════════════════════════════════════════════════════════════════


using lssLib.Net;

namespace lssLib.Net.Implementation;

/// <summary>
/// 요청-응답 채널 (RequestResponse 모드).
/// ReadCommands 를 주기적으로 전송하고, Write 는 Read 보다 항상 우선 처리됩니다.
/// </summary>
/// <remarks>
/// <b>우선순위:</b>
/// Critical(0) &gt; Write(1) &gt; Read(2) &gt; Low/Heartbeat(3)
///
/// <b>조립 예시 (Modbus RTU):</b>
/// <code>
/// var cfg = new SerialDeviceConfig(1, "Modbus-PLC", "COM3", 9600)
/// {
///     IsSequential     = true,
///     PeriodicInterval = TimeSpan.FromMilliseconds(50)
/// };
/// cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);
///
/// await using var channel = new RequestResponseChannel(
///     cfg,
///     SerialTransport.FromConfig(cfg),
///     new RawProtocol(),
///     autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) => ProcessModbus(id, frame);
/// channel.DeviceStateChanged  += (id, state) =>
///     Dispatcher.InvokeAsync(() => BtnSend.IsEnabled = (state == NetState.Connected));
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
///     // lssLib.Binary 파싱
///     // var result = r.Data!.ToParser().Parse(ModbusSchema.Default);
/// }
///
/// // 통계 조회
/// var snap = channel.Statistics.Snapshot();
/// LogManager.Instance.Info("Modbus-PLC",
///     $"평균응답={snap.AvgResponseMs:F1}ms 재접속={snap.TotalReconnects}");
/// </code>
///
/// <b>파생 클래스 패턴 (동적 Read 프레임):</b>
/// <code>
/// public class ModbusChannel : RequestResponseChannel
/// {
///     private byte _slaveAddr = 0x01;
///
///     public ModbusChannel(SerialDeviceConfig cfg)
///         : base(cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true) { }
///
///     public void SetSlave(byte addr) => _slaveAddr = addr;
///
///     // ReadCommands 없을 때 동적 생성
///     protected override Task&lt;byte[]?&gt; BuildExtraReadRequestAsync(CancellationToken ct)
///     {
///         // byte[] req = BufferWriter.Create()
///         //     .WriteUInt8(_slaveAddr).WriteUInt8(0x03)
///         //     .WriteUInt16BE(0x0000).WriteUInt16BE(0x000A)
///         //     .AppendCrc16Modbus().ToArray();
///         // return Task.FromResult&lt;byte[]?&gt;(req);
///         return Task.FromResult&lt;byte[]?&gt;(null);
///     }
/// }
/// </code>
/// </remarks>
public class RequestResponseChannel : NetChannelBase
{
    /// <inheritdoc/>
    public override NetMode Mode => NetMode.RequestResponse;

    /// <summary>요청-응답 채널을 초기화합니다.</summary>
    /// <param name="config">장비 설정 (ReadCommands / PeriodicInterval / IsSequential 포함)</param>
    /// <param name="transport">전송 계층. XxxTransport.FromConfig(cfg) 권장.</param>
    /// <param name="protocol">프로토콜 계층.</param>
    /// <param name="autoRegister">true 이면 NetDeviceRegistry 자동 등록.</param>
    public RequestResponseChannel(
        NetDeviceConfigBase config,
        INetTransport transport,
        INetProtocol protocol,
        bool autoRegister = false)
        : base(config, transport, protocol, autoRegister) { }
}