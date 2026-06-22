// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/TreeNode.cs
//  역할: 장비 트리 노드 추상 기반 + 4종 구체 노드
//        Group / Device / Plc / Tag
//  Enum 은 Models/Enums.cs 로 분리됨 (NodeCommType 등)
//  S-17B: AbstractTreeNode에 IsEditing, EditBuffer 추가
//         더블클릭 인라인 편집 지원
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IIoT.Studio.Models;

// §1 ─ 추상 기반 노드 ─────────────────────────────────────

public abstract partial class AbstractTreeNode : ObservableObject
{
    // §1-1 ─ 공통 프로퍼티 ───────────────────────────────────

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    public abstract string IconGlyph     { get; }
    public abstract string NodeTypeLabel { get; }

    // §1-2 ─ ★ S-17B: 인라인 편집 상태 ──────────────────────

    /// <summary>현재 이름 인라인 편집 중 여부</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>편집 중 임시 이름 버퍼 (Enter/Esc로 확정·취소)</summary>
    [ObservableProperty]
    private string _editBuffer = string.Empty;

    /// <summary>인라인 편집 시작 — EditBuffer에 현재 Name 복사</summary>
    public void BeginEdit()
    {
        EditBuffer = Name;
        IsEditing  = true;
    }

    /// <summary>인라인 편집 확정 — EditBuffer가 비어있지 않으면 Name에 반영</summary>
    public void CommitEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditBuffer))
            Name = EditBuffer.Trim();
        IsEditing = false;
    }

    /// <summary>인라인 편집 취소 — Name 변경 없이 TextBox 닫기</summary>
    public void CancelEdit()
    {
        EditBuffer = Name;   // 버퍼 원복
        IsEditing  = false;
    }

    // §1-3 ─ 자식 노드 ───────────────────────────────────────

    public ObservableCollection<AbstractTreeNode> Children { get; } = new();

    // §1-4 ─ 헬퍼 ───────────────────────────────────────────

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

    [ObservableProperty] private string _address  = "40001";
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
