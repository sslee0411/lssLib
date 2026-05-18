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
    /// <summary>
    /// 전송 계층 유형. NetTransportType 열거형으로 구분됩니다.
    /// </summary>
    public override NetTransportType TransportType => NetTransportType.Serial;

    /// <summary>
    /// COM 포트 이름. 예: "COM1", "COM2", "COM3" 등. 운영체제에 따라 다를 수 있습니다.
    /// </summary>
    public string PortName { get; set; }

    /// <summary>
    /// 통신 속도(보드레이트). 일반적으로 9600, 19200, 38400, 115200 등 표준 값이 사용됩니다.
    /// </summary>
    public int BaudRate { get; set; }

    /// <summary>
    /// 데이터 비트 수. 일반적으로 7 또는 8이 사용됩니다. 기본값은 8입니다.
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 패리티 비트 설정. None, Odd, Even, Mark, Space 중 하나를 선택할 수 있습니다. 기본값은 None입니다.
    /// </summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>
    /// 정지 비트 설정. One, OnePointFive, Two 중 하나를 선택할 수 있습니다. 기본값은 One입니다.
    /// </summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>
    /// 읽기 타임아웃(밀리초). 시리얼 포트에서 데이터를 읽을 때 대기하는 최대 시간입니다. 기본값은 1000ms입니다.
    /// </summary>
    public int ReadTimeout { get; set; } = 1000;

    /// <summary>
    /// 쓰기 타임아웃(밀리초). 시리얼 포트에 데이터를 쓸 때 대기하는 최대 시간입니다. 기본값은 1000ms입니다.
    /// </summary>
    public int WriteTimeout { get; set; } = 1000;

    /// <summary>
    /// 생성자. 필수 매개변수로 deviceId, deviceName, portName, baudRate를 받습니다.
    /// </summary>
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