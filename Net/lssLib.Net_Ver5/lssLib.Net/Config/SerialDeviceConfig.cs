// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/SerialDeviceConfig.cs  [v5.1]
//  SequenceMode = 1 (Sequential) — RS-485 버스 충돌 방지 필수
// ══════════════════════════════════════════════════════════════════════

using System.IO.Ports;

namespace lssLib.Net;

/// <summary>
/// 시리얼(COM 포트) 통신 장비 설정.
/// </summary>
/// <example><code>
/// var cfg = new SerialDeviceConfig(2, "Modbus-PLC", "COM3", 9600);
/// // cfg.SequenceMode == 1 (Sequential) ← RS-485 기본값
///
/// cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);
/// cfg.AddReadCommand([0x01, 0x03, 0x00, 0x10, 0x00, 0x05, 0x84, 0x0E]);
/// // → 2개 커맨드를 1번 → 2번 순서로 하나씩 전송
/// </code></example>
public sealed class SerialDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.Serial;

    public string PortName { get; set; }
    public int BaudRate { get; set; }
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public int ReadTimeout { get; set; } = 1000;
    public int WriteTimeout { get; set; } = 1000;

    public SerialDeviceConfig(int deviceId, string deviceName, string portName, int baudRate)
        : base(deviceId, deviceName)
    {
        PortName = portName;
        BaudRate = baudRate;
        RetryDelay = TimeSpan.FromMilliseconds(100);
        ReconnectBackoff = false;
        SequenceMode = NetDeviceConfig.SequenceModes.Sequential;  // 1: 단일 순차 (RS-485 필수)
        RequestTimeout = TimeSpan.FromSeconds(1);
        HeartbeatInterval = TimeSpan.Zero;
    }

    public override string ToString()
        => base.ToString() + $" | {PortName} {BaudRate}bps {Parity}/{DataBits}/{StopBits}";
}