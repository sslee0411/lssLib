// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/CommConfig.cs
//  역할: 통신 설정 라이브러리 레코드
//        Device.CommConfigId 로 참조 (여러 Device 공유 가능)
//        comm-library.json 에 저장
//  Phase 1 Update: 신규 추가
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// 통신 프로토콜 종류
/// </summary>
public enum CommProtocol
{
    ModbusTcp = 0,   // Modbus TCP/IP
    ModbusRtu = 1,   // Modbus RTU (시리얼)
    ModbusAscii = 2,   // Modbus ASCII (시리얼)
    OpcUa = 3,   // OPC-UA
    OpcDa = 4,   // OPC-DA (COM/DCOM)
    TcpIp = 5,   // 커스텀 TCP/IP 소켓
    Serial = 6,   // 순수 시리얼 (RS232/RS485)
    Mqtt = 7,   // MQTT
    Simulation = 99,  // 시뮬레이션 (테스트용)
}

/// <summary>
/// 시리얼 패리티
/// </summary>
public enum SerialParity { None, Odd, Even, Mark, Space }

/// <summary>
/// 시리얼 스톱비트
/// </summary>
public enum SerialStopBits { One, OnePointFive, Two }

/// <summary>
/// 통신 설정 레코드.
/// comm-library.json 에 등록되며, Device 가 CommConfigId 로 참조합니다.
/// 같은 PLC 에 여러 Device 가 연결된 경우 하나의 CommConfig 를 공유합니다.
/// </summary>
/// <example><code>
/// var cc = new CommConfig
/// {
///     Id = "cc-001", Name = "1공장 PLC라인",
///     Protocol = CommProtocol.ModbusTcp,
///     IpAddress = "192.168.1.10", Port = 502,
///     SlaveId = 1, Timeout = 3000, RetryCount = 3, PollMs = 1000
/// };
/// </code></example>
public record CommConfig
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    // §2 ─ 프로토콜 공통 ──────────────────────────────────────
    public CommProtocol Protocol { get; init; } = CommProtocol.ModbusTcp;
    public int Timeout { get; init; } = 3000;   // ms
    public int RetryCount { get; init; } = 3;
    public int PollMs { get; init; } = 1000;   // 기본 폴링 주기

    // §3 ─ TCP/IP 계열 (Modbus TCP, OPC-UA, TcpIp, MQTT) ─────
    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; } = 502;

    // §4 ─ Modbus 전용 ────────────────────────────────────────
    /// <summary>Modbus Slave ID / Unit ID (1~247)</summary>
    public int SlaveId { get; init; } = 1;

    // §5 ─ 시리얼 전용 (ModbusRtu, ModbusAscii, Serial) ───────
    public string PortName { get; init; } = "COM1";
    public int BaudRate { get; init; } = 9600;
    public int DataBits { get; init; } = 8;
    public SerialParity Parity { get; init; } = SerialParity.None;
    public SerialStopBits StopBits { get; init; } = SerialStopBits.One;

    // §6 ─ OPC-UA 전용 ────────────────────────────────────────
    /// <summary>OPC-UA 엔드포인트 URL (예: "opc.tcp://192.168.1.10:4840")</summary>
    public string OpcEndpointUrl { get; init; } = string.Empty;

    /// <summary>OPC-UA 보안 정책 (None / Basic256Sha256 등)</summary>
    public string OpcSecurityPolicy { get; init; } = "None";

    // §7 ─ MQTT 전용 ──────────────────────────────────────────
    public string MqttClientId { get; init; } = string.Empty;
    public string MqttUsername { get; init; } = string.Empty;

    // §8 ─ 헬퍼 ──────────────────────────────────────────────
    public bool IsSerial =>
        Protocol is CommProtocol.ModbusRtu
                 or CommProtocol.ModbusAscii
                 or CommProtocol.Serial;

    public bool IsTcpBased =>
        Protocol is CommProtocol.ModbusTcp
                 or CommProtocol.OpcUa
                 or CommProtocol.TcpIp
                 or CommProtocol.Mqtt;

    // §9 ─ ConfigManager 직렬화 헬퍼 ──────────────────────────
    public string SectionKey => $"CommLibrary:{Id}";

    public Dictionary<string, string> ToConfigEntries() => new()
    {
        ["name"] = Name,
        ["description"] = Description,
        ["protocol"] = ((int)Protocol).ToString(),
        ["timeout"] = Timeout.ToString(),
        ["retryCount"] = RetryCount.ToString(),
        ["pollMs"] = PollMs.ToString(),
        // TCP/IP
        ["ipAddress"] = IpAddress,
        ["port"] = Port.ToString(),
        // Modbus
        ["slaveId"] = SlaveId.ToString(),
        // Serial
        ["portName"] = PortName,
        ["baudRate"] = BaudRate.ToString(),
        ["dataBits"] = DataBits.ToString(),
        ["parity"] = ((int)Parity).ToString(),
        ["stopBits"] = ((int)StopBits).ToString(),
        // OPC-UA
        ["opcEndpointUrl"] = OpcEndpointUrl,
        ["opcSecurityPolicy"] = OpcSecurityPolicy,
        // MQTT
        ["mqttClientId"] = MqttClientId,
        ["mqttUsername"] = MqttUsername,
    };

    public static CommConfig FromConfigEntries(string id,
                                               IReadOnlyDictionary<string, string> e)
    {
        static int I(string? s, int d) => int.TryParse(s, out var v) ? v : d;
        static T E<T>(string? s, T d) where T : struct, Enum
            => Enum.TryParse<T>(s, out var v) ? v : d;

        return new CommConfig
        {
            Id = id,
            Name = e.GetValueOrDefault("name", string.Empty),
            Description = e.GetValueOrDefault("description", string.Empty),
            Protocol = E(e.GetValueOrDefault("protocol"), CommProtocol.ModbusTcp),
            Timeout = I(e.GetValueOrDefault("timeout"), 3000),
            RetryCount = I(e.GetValueOrDefault("retryCount"), 3),
            PollMs = I(e.GetValueOrDefault("pollMs"), 1000),
            IpAddress = e.GetValueOrDefault("ipAddress", string.Empty),
            Port = I(e.GetValueOrDefault("port"), 502),
            SlaveId = I(e.GetValueOrDefault("slaveId"), 1),
            PortName = e.GetValueOrDefault("portName", "COM1"),
            BaudRate = I(e.GetValueOrDefault("baudRate"), 9600),
            DataBits = I(e.GetValueOrDefault("dataBits"), 8),
            Parity = E(e.GetValueOrDefault("parity"), SerialParity.None),
            StopBits = E(e.GetValueOrDefault("stopBits"), SerialStopBits.One),
            OpcEndpointUrl = e.GetValueOrDefault("opcEndpointUrl", string.Empty),
            OpcSecurityPolicy = e.GetValueOrDefault("opcSecurityPolicy", "None"),
            MqttClientId = e.GetValueOrDefault("mqttClientId", string.Empty),
            MqttUsername = e.GetValueOrDefault("mqttUsername", string.Empty),
        };
    }
}