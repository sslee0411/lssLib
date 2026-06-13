// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Library/CommLibraryViewModel.cs
//  역할: 통신 설정 라이브러리 CRUD + 프로토콜별 조건부 필드 ViewModel
//  Phase 4-3: 라이브러리 뷰 신규
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.Core.DataModel;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels.Library;

/// <summary>
/// 통신 설정 라이브러리 편집 ViewModel.
/// comm-library.json 의 CommConfig 목록을 CRUD 합니다.
/// Protocol 선택에 따라 TCP/Serial/OPC-UA 필드가 조건부 표시됩니다.
/// </summary>
public partial class CommLibraryViewModel : ObservableObject
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "CommLibrary";
    private readonly JsonWriteService _writer;

    // §2 ─ 목록 ───────────────────────────────────────────────
    public ObservableCollection<CommConfigItem> Items { get; } = [];

    // §3 ─ 선택 항목 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private CommConfigItem? _selectedItem;

    public bool HasSelection => SelectedItem is not null;

    // §4 ─ 상태 ───────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = "준비";
    [ObservableProperty] private bool   _hasChanges;
    [ObservableProperty] private string _testResult = "";

    // §5 ─ 드롭다운 소스 ──────────────────────────────────────
    public IEnumerable<CommProtocol> ProtocolList =>
        Enum.GetValues<CommProtocol>().Where(p => p != CommProtocol.Simulation);

    public IReadOnlyList<int> BaudRateList =>
        [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    public IEnumerable<SerialParity> ParityList   => Enum.GetValues<SerialParity>();
    public IEnumerable<SerialStopBits> StopBitsList => Enum.GetValues<SerialStopBits>();

    // §6 ─ 생성자 ─────────────────────────────────────────────
    public CommLibraryViewModel(JsonWriteService writer)
    {
        _writer = writer;
        Items.CollectionChanged += (_, _) => HasChanges = true;
    }

    // §7 ─ 데이터 로드 ────────────────────────────────────────

    public void Load(IEnumerable<CommConfig> configs)
    {
        Items.Clear();
        foreach (var c in configs)
            Items.Add(new CommConfigItem(c));
        HasChanges = false;
        StatusMessage = $"통신 설정 {Items.Count}개 로드 완료";
        LogManager.Instance.Info(LogSrc, $"통신 라이브러리 로드: {Items.Count}개");
    }

    // §8 ─ CRUD 커맨드 ────────────────────────────────────────

    [RelayCommand]
    private void Add()
    {
        var item = new CommConfigItem(new CommConfig
        {
            Name      = "새 통신 설정",
            Protocol  = CommProtocol.ModbusTcp,
            IpAddress = "192.168.1.1",
            Port      = 502,
            SlaveId   = 1,
            Timeout   = 3000,
            RetryCount = 3,
            PollMs    = 1000,
        });
        Items.Add(item);
        SelectedItem = item;
        StatusMessage = "새 통신 설정 추가됨";
        LogManager.Instance.Info(LogSrc, "통신 설정 추가");
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedItem is null) return;
        var name = SelectedItem.Name;
        Items.Remove(SelectedItem);
        SelectedItem = Items.LastOrDefault();
        StatusMessage = $"'{name}' 삭제됨";
        LogManager.Instance.Info(LogSrc, $"통신 설정 삭제: {name}");
    }

    private bool CanDelete() => SelectedItem is not null;

    [RelayCommand]
    private void Save()
    {
        var configs = Items.Select(i => i.ToCommConfig()).ToList();
        _writer.SaveCommLibrary(configs);
        HasChanges = false;
        StatusMessage = $"저장 완료 — {configs.Count}개";
        LogManager.Instance.Info(LogSrc, $"통신 라이브러리 저장: {configs.Count}개");
    }

    /// <summary>
    /// 연결 테스트 (Phase 5에서 실제 연결 구현 예정).
    /// 현재는 설정값 유효성 확인만 수행.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void TestConnection()
    {
        if (SelectedItem is null) return;
        var c = SelectedItem;
        TestResult = c.Protocol switch
        {
            CommProtocol.ModbusTcp or CommProtocol.TcpIp =>
                string.IsNullOrWhiteSpace(c.IpAddress)
                    ? "⚠ IP 주소를 입력하세요"
                    : $"⏳ {c.IpAddress}:{c.Port} 연결 테스트 (Phase 5 구현 예정)",
            CommProtocol.ModbusRtu or CommProtocol.ModbusAscii or CommProtocol.Serial =>
                string.IsNullOrWhiteSpace(c.PortName)
                    ? "⚠ COM 포트를 입력하세요"
                    : $"⏳ {c.PortName} @ {c.BaudRate}bps 테스트 (Phase 5 구현 예정)",
            CommProtocol.OpcUa =>
                string.IsNullOrWhiteSpace(c.OpcEndpointUrl)
                    ? "⚠ 엔드포인트 URL을 입력하세요"
                    : $"⏳ {c.OpcEndpointUrl} 테스트 (Phase 5 구현 예정)",
            _ => "⏳ 연결 테스트 (Phase 5 구현 예정)"
        };
        StatusMessage = TestResult;
        LogManager.Instance.Info(LogSrc, $"연결 테스트 요청: {c.Name}");
    }
}

// ─────────────────────────────────────────────────────────
/// <summary>DataGrid 바인딩용 CommConfig 래퍼.</summary>
public partial class CommConfigItem : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string Id { get; }

    [ObservableProperty] private string       _name;
    [ObservableProperty] private string       _description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsOpcUa))]
    [NotifyPropertyChangedFor(nameof(ProtocolLabel))]
    private CommProtocol _protocol;

    // TCP/IP
    [ObservableProperty] private string _ipAddress;
    [ObservableProperty] private int    _port;
    [ObservableProperty] private int    _slaveId;

    // Serial
    [ObservableProperty] private string       _portName;
    [ObservableProperty] private int          _baudRate;
    [ObservableProperty] private int          _dataBits;
    [ObservableProperty] private SerialParity _parity;
    [ObservableProperty] private SerialStopBits _stopBits;

    // OPC-UA
    [ObservableProperty] private string _opcEndpointUrl;
    [ObservableProperty] private string _opcSecurityPolicy;

    // 공통
    [ObservableProperty] private int _timeout;
    [ObservableProperty] private int _retryCount;
    [ObservableProperty] private int _pollMs;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public CommConfigItem(CommConfig c)
    {
        Id                = c.Id;
        _name             = c.Name;
        _description      = c.Description;
        _protocol         = c.Protocol;
        _ipAddress        = c.IpAddress;
        _port             = c.Port;
        _slaveId          = c.SlaveId;
        _portName         = c.PortName;
        _baudRate         = c.BaudRate;
        _dataBits         = c.DataBits;
        _parity           = c.Parity;
        _stopBits         = c.StopBits;
        _opcEndpointUrl   = c.OpcEndpointUrl;
        _opcSecurityPolicy = c.OpcSecurityPolicy;
        _timeout          = c.Timeout;
        _retryCount       = c.RetryCount;
        _pollMs           = c.PollMs;
    }

    // §3 ─ 조건부 표시 ────────────────────────────────────────
    public bool IsTcp    => Protocol is CommProtocol.ModbusTcp or CommProtocol.TcpIp or CommProtocol.Mqtt;
    public bool IsSerial => Protocol is CommProtocol.ModbusRtu or CommProtocol.ModbusAscii or CommProtocol.Serial;
    public bool IsOpcUa  => Protocol is CommProtocol.OpcUa;

    public string ProtocolLabel => Protocol switch
    {
        CommProtocol.ModbusTcp   => "Modbus TCP",
        CommProtocol.ModbusRtu   => "Modbus RTU",
        CommProtocol.ModbusAscii => "Modbus ASCII",
        CommProtocol.OpcUa       => "OPC-UA",
        CommProtocol.OpcDa       => "OPC-DA",
        CommProtocol.TcpIp       => "TCP/IP",
        CommProtocol.Serial      => "Serial",
        CommProtocol.Mqtt        => "MQTT",
        CommProtocol.Simulation  => "Simulation",
        _ => Protocol.ToString()
    };

    // §4 ─ 역변환 ─────────────────────────────────────────────
    public CommConfig ToCommConfig() => new()
    {
        Id                = Id,
        Name              = Name,
        Description       = Description,
        Protocol          = Protocol,
        IpAddress         = IpAddress,
        Port              = Port,
        SlaveId           = SlaveId,
        PortName          = PortName,
        BaudRate          = BaudRate,
        DataBits          = DataBits,
        Parity            = Parity,
        StopBits          = StopBits,
        OpcEndpointUrl    = OpcEndpointUrl,
        OpcSecurityPolicy = OpcSecurityPolicy,
        Timeout           = Timeout,
        RetryCount        = RetryCount,
        PollMs            = PollMs,
    };
}
