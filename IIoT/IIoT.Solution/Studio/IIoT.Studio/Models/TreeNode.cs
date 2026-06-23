// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/TreeNode.cs
//  역할: 장비 트리 노드 추상 기반 + 4종 구체 노드
//  S-17B: AbstractTreeNode IsEditing, EditBuffer 추가
//  S-21A B-1: PlcTreeNode → PlcVendor 추가
//             TagTreeNode → RegisterType + AddressHint 추가
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IIoT.Studio.Models;

// §1 ─ 추상 기반 노드 ─────────────────────────────────────

public abstract partial class AbstractTreeNode : ObservableObject
{
    [ObservableProperty] private string _name        = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    public abstract string IconGlyph     { get; }
    public abstract string NodeTypeLabel { get; }

    // S-17B: 인라인 편집
    [ObservableProperty] private bool   _isEditing;
    [ObservableProperty] private string _editBuffer = string.Empty;

    public void BeginEdit()  { EditBuffer = Name; IsEditing = true; }
    public void CommitEdit() { if (!string.IsNullOrWhiteSpace(EditBuffer)) Name = EditBuffer.Trim(); IsEditing = false; }
    public void CancelEdit() { EditBuffer = Name; IsEditing = false; }

    public ObservableCollection<AbstractTreeNode> Children { get; } = new();
    public Guid Id { get; } = Guid.NewGuid();
}

// §2 ─ 그룹 노드 ──────────────────────────────────────────

public partial class GroupTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "📁";
    public override string NodeTypeLabel => "그룹";
    public GroupTreeNode(string name = "새 그룹") { Name = name; }
}

// §3 ─ 장비 노드 ──────────────────────────────────────────

public partial class DeviceTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "🏭";
    public override string NodeTypeLabel => "장비";

    [ObservableProperty] private string _model        = string.Empty;
    [ObservableProperty] private string _manufacturer = string.Empty;
    [ObservableProperty] private string _location     = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommEnabled))]
    [NotifyPropertyChangedFor(nameof(IsModbusTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsMqtt))]
    [NotifyPropertyChangedFor(nameof(IsOpcUa))]
    private NodeCommType _commType = NodeCommType.None;

    [ObservableProperty] private string _host   = string.Empty;
    [ObservableProperty] private int    _port   = 502;
    [ObservableProperty] private int    _pollMs = 1000;

    public bool IsCommEnabled => CommType != NodeCommType.None;
    public bool IsModbusTcp   => CommType == NodeCommType.ModbusTcp;
    public bool IsSerial      => CommType == NodeCommType.Serial;
    public bool IsMqtt        => CommType == NodeCommType.Mqtt;
    public bool IsOpcUa       => CommType == NodeCommType.OpcUa;

    public DeviceTreeNode(string name = "새 장비") { Name = name; }
}

// §4 ─ PLC 노드 ───────────────────────────────────────────

public partial class PlcTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "🔧";
    public override string NodeTypeLabel => "PLC";

    // ★ S-21A B-1: PLC 제조사 — 하위 Tag 편집기 RegisterType 필터링에 사용
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VendorLabel))]
    private PlcVendor _plcVendor = PlcVendor.Modbus;

    public string VendorLabel => PlcVendor.ToLabel();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModbusTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsMqtt))]
    [NotifyPropertyChangedFor(nameof(IsOpcUa))]
    private NodeCommType _commType = NodeCommType.ModbusTcp;

    [ObservableProperty] private string _host   = "192.168.0.1";
    [ObservableProperty] private int    _port   = 502;
    [ObservableProperty] private int    _pollMs = 1000;

    public bool IsModbusTcp => CommType == NodeCommType.ModbusTcp;
    public bool IsSerial    => CommType == NodeCommType.Serial;
    public bool IsMqtt      => CommType == NodeCommType.Mqtt;
    public bool IsOpcUa     => CommType == NodeCommType.OpcUa;

    public PlcTreeNode(string name = "새 PLC") { Name = name; }
}

// §5 ─ Tag 노드 ───────────────────────────────────────────

public partial class TagTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "🏷";
    public override string NodeTypeLabel => "Tag";

    /// <summary>레지스터 주소 (문자열 — 형식은 제조사+레지스터종류에 따라 다름)</summary>
    [ObservableProperty]
    private string _address = "40001";

    /// <summary>
    /// ★ S-21A B-1: 레지스터 종류 (제조사 공용).
    /// 변경 시 AddressHint 자동 갱신.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressHint))]
    private RegisterType _registerType = RegisterType.HoldingReg;

    /// <summary>
    /// 현재 레지스터 종류의 입력 예시 힌트.
    /// 부모 PLC의 PlcVendor를 알아야 정확한 힌트 표시 가능.
    /// Tag 편집기에서 RelativeSource로 부모 PLC의 PlcVendor를 전달받음.
    /// 여기서는 기본 힌트만 제공.
    /// </summary>
    public string AddressHint => RegisterType switch
    {
        RegisterType.Word         => "예) D100 / %MW100 / D00100",
        RegisterType.DWord        => "예) D100 / %MD100 / DB1.DBD0",
        RegisterType.BitInput     => "예) X0.0 / %IX0.0",
        RegisterType.BitOutput    => "예) Y0.0 / %QX0.0",
        RegisterType.BitMemory    => "예) M100 / %MX0.0 / DB1.DBX0.0",
        RegisterType.LinkWord     => "예) W100 / %LW100",
        RegisterType.Timer        => "예) TN0 / TIM0000",
        RegisterType.Counter      => "예) CN0 / CNT0000",
        RegisterType.HoldingReg   => "예) 40001 ~ 49999 (Modbus Holding)",
        RegisterType.InputReg     => "예) 30001 ~ 39999 (Modbus Input)",
        RegisterType.Coil         => "예) 1 ~ 9999 (Modbus Coil)",
        RegisterType.DiscreteInput=> "예) 10001 ~ 19999 (Modbus Discrete)",
        _                         => "주소를 직접 입력하세요",
    };

    [ObservableProperty] private string _dataType = "Float";
    [ObservableProperty] private string _unit     = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScale))]
    private Guid? _scaleEntryId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlarm))]
    private Guid? _alarmEntryId;

    public bool HasScale => ScaleEntryId.HasValue;
    public bool HasAlarm => AlarmEntryId.HasValue;

    public TagTreeNode(string name = "새 Tag") { Name = name; }
}
