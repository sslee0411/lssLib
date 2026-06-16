// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/CommLibrary.cs
//  역할: 통신 라이브러리 항목 모델
//        Modbus TCP / Serial / MQTT / OPC-UA 통신 프로파일
//        장비·PLC 에서 Id 로 참조
//  S-08: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Models;

/// <summary>통신 방식 열거형</summary>
public enum CommType
{
    ModbusTcp,
    Serial,
    Mqtt,
    OpcUa
}

/// <summary>
/// 통신 라이브러리 항목.
/// 재사용 가능한 통신 프로파일 1건.
/// 장비·PLC 에서 동일 프로파일을 공유 참조 가능.
/// </summary>
public partial class CommEntry : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────

    public Guid Id { get; } = Guid.NewGuid();

    // §2 ─ 기본 정보 ──────────────────────────────────────────

    [ObservableProperty]
    private string _name = "새 통신";

    [ObservableProperty]
    private string _description = string.Empty;

    // §3 ─ 통신 방식 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModbusTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsMqtt))]
    [NotifyPropertyChangedFor(nameof(IsOpcUa))]
    [NotifyPropertyChangedFor(nameof(CommTypeLabel))]
    private CommType _type = CommType.ModbusTcp;

    public bool IsModbusTcp
    {
        get => Type == CommType.ModbusTcp;
        set { if (value) Type = CommType.ModbusTcp; }
    }
    public bool IsSerial
    {
        get => Type == CommType.Serial;
        set { if (value) Type = CommType.Serial; }
    }
    public bool IsMqtt
    {
        get => Type == CommType.Mqtt;
        set { if (value) Type = CommType.Mqtt; }
    }
    public bool IsOpcUa
    {
        get => Type == CommType.OpcUa;
        set { if (value) Type = CommType.OpcUa; }
    }

    public string CommTypeLabel => Type switch
    {
        CommType.ModbusTcp => "Modbus TCP",
        CommType.Serial    => "Serial",
        CommType.Mqtt      => "MQTT",
        CommType.OpcUa     => "OPC-UA",
        _                  => string.Empty
    };

    // §4 ─ Modbus TCP 설정 ────────────────────────────────────

    [ObservableProperty]
    private string _host = "192.168.0.1";

    [ObservableProperty]
    private int _port = 502;

    /// <summary>Modbus 슬레이브 ID</summary>
    [ObservableProperty]
    private int _slaveId = 1;

    // §5 ─ Serial 설정 ────────────────────────────────────────

    [ObservableProperty]
    private string _comPort = "COM1";

    [ObservableProperty]
    private int _baudRate = 9600;

    [ObservableProperty]
    private string _parity = "None";

    [ObservableProperty]
    private int _dataBits = 8;

    [ObservableProperty]
    private string _stopBits = "One";

    // §6 ─ MQTT 설정 ──────────────────────────────────────────

    [ObservableProperty]
    private string _brokerHost = "localhost";

    [ObservableProperty]
    private int _brokerPort = 1883;

    [ObservableProperty]
    private string _clientId = string.Empty;

    [ObservableProperty]
    private string _topic = "iiot/#";

    [ObservableProperty]
    private bool _useTls;

    [ObservableProperty]
    private string _mqttUser = string.Empty;

    [ObservableProperty]
    private string _mqttPassword = string.Empty;

    // §7 ─ OPC-UA 설정 ────────────────────────────────────────

    [ObservableProperty]
    private string _endpointUrl = "opc.tcp://localhost:4840";

    [ObservableProperty]
    private string _opcUser = string.Empty;

    [ObservableProperty]
    private string _opcPassword = string.Empty;

    // §8 ─ 공통 설정 ──────────────────────────────────────────

    /// <summary>폴링 주기 (ms) — 데이터 수집 간격</summary>
    [ObservableProperty]
    private int _pollMs = 1000;

    /// <summary>연결 타임아웃 (ms)</summary>
    [ObservableProperty]
    private int _timeoutMs = 3000;

    /// <summary>재연결 시도 간격 (ms)</summary>
    [ObservableProperty]
    private int _retryIntervalMs = 5000;

    // §9 ─ 미리보기 ───────────────────────────────────────────

    public string PreviewSummary => Type switch
    {
        CommType.ModbusTcp => $"{Host}:{Port}  슬레이브ID={SlaveId}",
        CommType.Serial    => $"{ComPort}  {BaudRate}bps",
        CommType.Mqtt      => $"{BrokerHost}:{BrokerPort}  {Topic}",
        CommType.OpcUa     => EndpointUrl,
        _                  => string.Empty
    };
}
