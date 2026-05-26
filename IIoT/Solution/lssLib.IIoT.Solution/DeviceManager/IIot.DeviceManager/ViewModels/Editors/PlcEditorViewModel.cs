// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Editors/PlcEditorViewModel.cs
//  역할: PLC 노드 통신 설정 편집 ViewModel
//  Phase 3: 편집기 패널
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.DataModel;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using System.Xml.Linq;

namespace IIoT.DeviceManager.ViewModels.Editors;

/// <summary>PLC 노드 통신 설정 편집 ViewModel.</summary>
public partial class PlcEditorViewModel : ObservableObject
{
    private PlcNodeViewModel? _target;

    // §1 ─ PLC 기본 ───────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _slotNo;
    [ObservableProperty] private byte _unitId = 1;

    // §2 ─ 프로토콜 선택 ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsOpcUa))]
    private CommProtocol _protocol = CommProtocol.ModbusTcp;

    public bool IsTcp => Protocol is CommProtocol.ModbusTcp or
                                        CommProtocol.TcpIp;
    public bool IsSerial => Protocol is CommProtocol.ModbusRtu or
                                        CommProtocol.ModbusAscii or
                                        CommProtocol.Serial;
    public bool IsOpcUa => Protocol is CommProtocol.OpcUa;

    // §3 ─ TCP/IP 설정 ────────────────────────────────────────
    [ObservableProperty] private string _ipAddress = "192.168.1.1";
    [ObservableProperty] private int _port = 502;
    [ObservableProperty] private int _slaveId = 1;

    // §4 ─ 시리얼 설정 ────────────────────────────────────────
    [ObservableProperty] private string _portName = "COM1";
    [ObservableProperty] private int _baudRate = 9600;
    [ObservableProperty] private SerialParity _parity = SerialParity.None;

    // §5 ─ OPC-UA 설정 ────────────────────────────────────────
    [ObservableProperty] private string _opcEndpointUrl = "opc.tcp://192.168.1.1:4840";

    // §6 ─ 공통 수집 설정 ─────────────────────────────────────
    [ObservableProperty] private int _timeout = 3000;
    [ObservableProperty] private int _retryCount = 3;
    [ObservableProperty] private int _pollMs = 1000;

    // §7 ─ 상태 ───────────────────────────────────────────────
    [ObservableProperty] private bool _hasChanges;

    public string TargetLabel => _target is not null
        ? $"⚙️  {_target.Name}  (#{_target.SlotNo})"
        : "PLC 선택 없음";

    // §8 ─ 프로토콜 선택 목록 ─────────────────────────────────
    public IEnumerable<CommProtocol> ProtocolList =>
        Enum.GetValues<CommProtocol>().Where(p => p != CommProtocol.Simulation);

    public IEnumerable<SerialParity> ParityList =>
        Enum.GetValues<SerialParity>();

    public IEnumerable<int> BaudRateList =>
        [9600, 19200, 38400, 57600, 115200];

    // §9 ─ Load / Apply ───────────────────────────────────────
    public void Load(PlcNodeViewModel node)
    {
        _target = node;
        Name = node.Name;
        SlotNo = node.SlotNo;
        UnitId = node.UnitId;
        Protocol = Enum.TryParse<CommProtocol>(node.ProtocolType, out var p)
                    ? p : CommProtocol.ModbusTcp;
        HasChanges = false;
        OnPropertyChanged(nameof(TargetLabel));
    }

    [RelayCommand]
    private void Apply()
    {
        if (_target is null) return;
        _target.Name = Name.Trim();
        _target.SlotNo = SlotNo;
        _target.UnitId = UnitId;
        _target.ProtocolType = Protocol.ToString();
        HasChanges = false;
    }

    [RelayCommand]
    private void Reset()
    {
        if (_target is not null) Load(_target);
    }

    // §10 ─ 변경 감지 ─────────────────────────────────────────
    partial void OnNameChanged(string v) => HasChanges = true;
    partial void OnProtocolChanged(CommProtocol v) => HasChanges = true;
    partial void OnIpAddressChanged(string v) => HasChanges = true;
    partial void OnPortChanged(int v) => HasChanges = true;
    partial void OnSlaveIdChanged(int v) => HasChanges = true;
    partial void OnTimeoutChanged(int v) => HasChanges = true;
    partial void OnPollMsChanged(int v) => HasChanges = true;
}