// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/SerialDeviceConfig.cs
// ══════════════════════════════════════════════════════════════════════

using System.IO.Ports;

using lssLib.Net;

namespace lssLib.Net.Implementation;

/// <summary>
/// 시리얼(COM 포트) 통신 장비 설정.
/// </summary>
/// <example><code>
/// var cfg = new SerialDeviceConfig(2, "Modbus-PLC", "COM3", 9600)
/// {
///     IsSequential     = true,    // RS-485 필수
///     ReconnectBackoff = false,   // 고정 간격
///     PeriodicInterval = TimeSpan.FromMilliseconds(50)
/// };
/// cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);
///
/// await using var channel = new RequestResponseChannel(
///     cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);
/// </code></example>
public sealed class SerialDeviceConfig : NetDeviceConfigBase
{
    /// <inheritdoc/>
    public override NetTransportType TransportType => NetTransportType.Serial;

    /// <summary>포트 이름. 예) "COM3", "/dev/ttyUSB0"</summary>
    public string PortName { get; set; }

    /// <summary>보드레이트. 예) 9600, 115200.</summary>
    public int BaudRate { get; set; }

    /// <summary>데이터 비트. 기본값: 8.</summary>
    public int DataBits { get; set; } = 8;

    /// <summary>패리티. 기본값: None.</summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>스톱 비트. 기본값: One.</summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>읽기 타임아웃(ms). 기본값: 1000.</summary>
    public int ReadTimeout { get; set; } = 1000;

    /// <summary>쓰기 타임아웃(ms). 기본값: 1000.</summary>
    public int WriteTimeout { get; set; } = 1000;

    public SerialDeviceConfig(int deviceId, string deviceName, string portName, int baudRate)
        : base(deviceId, deviceName)
    {
        PortName = portName;
        BaudRate = baudRate;
        // Serial 환경 기본값
        RetryDelay = TimeSpan.FromMilliseconds(100);
        ReconnectBackoff = false;
        IsSequential = true;
        RequestTimeout = TimeSpan.FromSeconds(1);
        HeartbeatInterval = TimeSpan.Zero;
    }

    /// <inheritdoc/>
    public override string ToString()
        => base.ToString() + $" | {PortName} {BaudRate}bps {Parity}/{DataBits}/{StopBits}";
}