// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/TreeNode.cs
//  역할: 장비 트리 노드 추상 기반 + 4종 구체 노드
//  S-17B: AbstractTreeNode IsEditing, EditBuffer 추가
//  S-21A B-1: PlcTreeNode → PlcVendor / TagTreeNode → RegisterType
//  S-23: TagTreeNode → Memo
//  S-24: AbstractTreeNode → IsExpanded
//  S-25: TagTreeNode → IsEnabled
//  S-28: PlcTreeNode → CommEntryId
//  Studio-P02: PlcTreeNode → DriverId / IsPluginDriver / DriverParams
//  Studio-P03b: DeviceTreeNode → DriverId / IsPluginDriver / DriverParams / CommEntryId
//               (단독 통신 장비 — IoT 게이트웨이, 센서 등 PLC 없이 직접 통신)
//  S-Virtual01: TagTreeNode → IsVirtual / Expression (가상/계산 Tag —
//               Collector VirtualTagEngine(C-18)이 이미 소비하는 필드를 Studio에서
//               편집 가능하게 함. true 면 Address/RegisterType/DataType 은 사용 안 함)
//  S-Virtual02: TagTreeNode → UseRoslynScript / ScriptCode (Function 노드 —
//               가상 Tag 계산을 NCalc 대신 Roslyn C# 스크립트로 수행하는 고급 모드)
//  S-프로토콜01: DeviceTreeNode/PlcTreeNode → ProtocolEntryId (프로토콜
//               라이브러리 참조, null = 미사용 — CommEntryId 와 동일 패턴)
//  생성: 2026-06-15 / 수정: 2026-07-20
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

    [ObservableProperty] private bool   _isEditing;
    [ObservableProperty] private string _editBuffer = string.Empty;

    public void BeginEdit()  { EditBuffer = Name; IsEditing = true; }
    public void CommitEdit() { if (!string.IsNullOrWhiteSpace(EditBuffer)) Name = EditBuffer.Trim(); IsEditing = false; }
    public void CancelEdit() { EditBuffer = Name; IsEditing = false; }

    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<AbstractTreeNode> Children { get; } = new();
    public Guid Id { get; } = Guid.NewGuid();
}

// §2 ─ 그룹 노드 ──────────────────────────────────────────

public partial class GroupTreeNode : AbstractTreeNode
{
    public override string IconGlyph     => "📁";
    public override string NodeTypeLabel => "그룹";
    public GroupTreeNode(string name = "새 그룹") { Name = name; }
}

// §3 ─ 장비 노드 ──────────────────────────────────────────

public partial class DeviceTreeNode : AbstractTreeNode
{
    public override string IconGlyph     => "🏭";
    public override string NodeTypeLabel => "장비";

    [ObservableProperty] private string _model        = string.Empty;
    [ObservableProperty] private string _manufacturer = string.Empty;
    [ObservableProperty] private string _location     = string.Empty;

    // ★ Studio-P03b: 플러그인 드라이버 ID
    //   단독 통신 장비(IoT 게이트웨이, 엣지 디바이스 등) 용
    //   "" = 레거시 CommType 방식 / 값 있음 = 플러그인 드라이버 사용
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPluginDriver))]
    private string _driverId = string.Empty;

    /// <summary>플러그인 드라이버 사용 여부.</summary>
    public bool IsPluginDriver => !string.IsNullOrEmpty(DriverId);

    /// <summary>드라이버별 추가 파라미터 (Key = ParameterDefinition.Key).</summary>
    public Dictionary<string, string> DriverParams { get; } = new();

    // ★ Studio-P03b: 통신 라이브러리 공유 참조 (PLC와 동일 방식)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommReferenced))]
    [NotifyPropertyChangedFor(nameof(IsDirectInput))]
    private Guid? _commEntryId;

    public bool IsCommReferenced => CommEntryId.HasValue;
    public bool IsDirectInput    => !CommEntryId.HasValue;

    // ★ S-프로토콜01: 프로토콜 라이브러리 참조 (null = 미사용)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProtocolReferenced))]
    private Guid? _protocolEntryId;

    public bool IsProtocolReferenced => ProtocolEntryId.HasValue;

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
    public override string IconGlyph     => "🔧";
    public override string NodeTypeLabel => "PLC";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VendorLabel))]
    private PlcVendor _plcVendor = PlcVendor.Modbus;

    public string VendorLabel => PlcVendor.ToLabel();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommReferenced))]
    [NotifyPropertyChangedFor(nameof(IsDirectInput))]
    private Guid? _commEntryId;

    public bool IsCommReferenced => CommEntryId.HasValue;
    public bool IsDirectInput    => !CommEntryId.HasValue;

    // ★ S-프로토콜01: 프로토콜 라이브러리 참조 (null = 미사용)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProtocolReferenced))]
    private Guid? _protocolEntryId;

    public bool IsProtocolReferenced => ProtocolEntryId.HasValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPluginDriver))]
    private string _driverId = string.Empty;

    public bool IsPluginDriver => !string.IsNullOrEmpty(DriverId);
    public Dictionary<string, string> DriverParams { get; } = new();

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
    public override string IconGlyph     => "🏷";
    public override string NodeTypeLabel => "Tag";

    [ObservableProperty] private string _address = "40001";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressHint))]
    private RegisterType _registerType = RegisterType.HoldingReg;

    public string AddressHint => RegisterType switch
    {
        RegisterType.Word          => "예) D100 / %MW100",
        RegisterType.DWord         => "예) D100 / %MD100",
        RegisterType.BitInput      => "예) X0.0 / %IX0.0",
        RegisterType.BitOutput     => "예) Y0.0 / %QX0.0",
        RegisterType.BitMemory     => "예) M100 / %MX0.0",
        RegisterType.LinkWord      => "예) W100 / %LW100",
        RegisterType.Timer         => "예) TN0 / TIM0000",
        RegisterType.Counter       => "예) CN0 / CNT0000",
        RegisterType.HoldingReg    => "예) 40001 ~ 49999",
        RegisterType.InputReg      => "예) 30001 ~ 39999",
        RegisterType.Coil          => "예) 1 ~ 9999",
        RegisterType.DiscreteInput => "예) 10001 ~ 19999",
        _                          => "주소를 직접 입력하세요"
    };

    [ObservableProperty] private string _dataType = "UInt16";
    [ObservableProperty] private string _unit     = string.Empty;
    [ObservableProperty] private Guid?  _scaleEntryId;
    [ObservableProperty] private Guid?  _alarmEntryId;
    [ObservableProperty] private string _memo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisabled))]
    private bool _isEnabled = true;

    public bool IsDisabled => !IsEnabled;

    // ★ S-Virtual01: 가상(계산) Tag
    //   true 면 Collector VirtualTagEngine(C-18)이 Address 폴링 대신 Expression 을
    //   주기 평가해 값을 계산·발행한다 (레지스터 종류/주소/데이터 타입 미사용).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotVirtual))]
    private bool _isVirtual;

    public bool IsNotVirtual => !IsVirtual;

    /// <summary>계산식 (NCalc). 다른 Tag 값은 [TagId] 형태로 참조.
    /// 예: "[T001] + [T002] * 0.5". IsVirtual=false 이면 미사용.</summary>
    [ObservableProperty] private string _expression = string.Empty;

    // ★ S-Virtual02: Function 노드 — Roslyn C# 고급 스크립트 모드
    //   NCalc(Expression)와 Roslyn(ScriptCode)는 서로 독립 보관 — 모드 전환 시
    //   값이 사라지지 않고 그대로 유지된다(둘 다 IsVirtual=true 일 때만 사용).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseNCalcMode))]
    private bool _useRoslynScript;

    /// <summary>UseRoslynScript 의 반대값 — RadioButton 양방향 바인딩용
    /// (ScaleEntry.IsLinear/IsExpression 과 동일한 확정 패턴).</summary>
    public bool UseNCalcMode
    {
        get => !UseRoslynScript;
        set { if (value) UseRoslynScript = false; }
    }

    /// <summary>Roslyn C# 스크립트 코드. UseRoslynScript=true 일 때만 사용.
    /// 스크립트 안에서 한정자 없이 Values(Dictionary&lt;string,double&gt;)/
    /// Result(double)/Suppress(bool) 를 직접 참조할 수 있다.
    /// 예: "Result = Values[\"T001\"] + Values[\"T002\"] * 0.5;"</summary>
    [ObservableProperty] private string _scriptCode = string.Empty;

    public TagTreeNode(string name = "새 Tag") { Name = name; }
}
