// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/DeviceTree/DeviceTreeViewModel.cs
//  역할: 장비 트리 ViewModel
//        DeviceTreeView 좌측 트리 + 우측 동적 편집기 바인딩
//        RightPanelMode 패턴: 선택된 노드 타입에 따라
//        IsGroupSelected / IsDeviceSelected / IsPlcSelected / IsTagSelected 전환
//  생성: 2026-06-14
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels.DeviceTree;

// ══════════════════════════════════════════════════════════
//  트리 노드 기반 타입
// ══════════════════════════════════════════════════════════

/// <summary>트리 노드 공통 기반 (그룹/장비/PLC/Tag 공통)</summary>
public abstract partial class AbstractTreeNode : ObservableObject
{
    // §1 ─ 공통 필드 ──────────────────────────────────────────
    [ObservableProperty] private string _name        = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool   _isExpanded  = false;

    public abstract string IconGlyph { get; }
    public abstract string Badge     { get; }

    public ObservableCollection<AbstractTreeNode> Children { get; } = [];
}

/// <summary>그룹 노드 (공장·라인·사이트)</summary>
public sealed partial class GroupTreeNode : AbstractTreeNode
{
    public override string IconGlyph => "📁";
    public override string Badge     => $"{Children.Count}개";
    public GroupTreeNode(string name) { Name = name; }
}

/// <summary>장비 노드 (압출기·사출기 등)</summary>
public sealed partial class DeviceTreeNode : AbstractTreeNode
{
    [ObservableProperty] private string _model        = string.Empty;
    [ObservableProperty] private string _manufacturer = string.Empty;
    [ObservableProperty] private string _location     = string.Empty;

    public override string IconGlyph => "🏭";
    public override string Badge     => string.Empty;
    public DeviceTreeNode(string name) { Name = name; }
}

/// <summary>PLC 노드 (Modbus/OPC-UA 통신)</summary>
public sealed partial class PlcTreeNode : AbstractTreeNode
{
    [ObservableProperty] private string _commLibraryId  = string.Empty;
    [ObservableProperty] private int    _defaultPollMs  = 1000;
    [ObservableProperty] private bool   _isEnabled      = true;

    public IReadOnlyList<CommLibraryItem> AvailableCommLibraries { get; set; } = [];
    public override string IconGlyph => "🔧";
    public override string Badge     => $"{Children.Count} Tags";
    public PlcTreeNode(string name) { Name = name; }
}

/// <summary>Tag 노드 (PLC 레지스터 수집 포인트)</summary>
public sealed partial class TagTreeNode : AbstractTreeNode
{
    [ObservableProperty] private string _tagId          = string.Empty;
    [ObservableProperty] private string _address        = string.Empty;
    [ObservableProperty] private string _dataType       = "FloatBE";
    [ObservableProperty] private int    _pollMs         = 0;
    [ObservableProperty] private string _rawUnit        = string.Empty;
    [ObservableProperty] private string _scaleConfigId  = string.Empty;
    [ObservableProperty] private string _alarmConfigId  = string.Empty;
    [ObservableProperty] private bool   _hasScalePreview;
    [ObservableProperty] private string _scalePreviewText = string.Empty;

    public IReadOnlyList<ScaleLibraryItem>  AvailableScales { get; set; } = [];
    public IReadOnlyList<AlarmLibraryItem>  AvailableAlarms { get; set; } = [];
    public IReadOnlyList<string>            DataTypeOptions { get; set; } =
        ["FloatBE", "FloatLE", "Int16", "Int32", "UInt16", "UInt32", "Bool", "BCD"];

    // SaveCommand — 저장 처리 (ViewModel에서 구독)
    public event Action<TagTreeNode>? SaveRequested;

    [RelayCommand]
    private void Save() => SaveRequested?.Invoke(this);

    public override string IconGlyph => "🏷";
    public override string Badge     => string.Empty;
    public TagTreeNode(string name) { Name = name; }
}

/// <summary>참조 목록용 경량 아이템</summary>
public sealed record CommLibraryItem(string Id, string Name);
public sealed record ScaleLibraryItem(string Id, string Name);
public sealed record AlarmLibraryItem(string Id, string Name);

// ══════════════════════════════════════════════════════════
//  DeviceTreeViewModel
// ══════════════════════════════════════════════════════════

public sealed partial class DeviceTreeViewModel : ObservableObject
{
    // §1 ─ 트리 루트 ──────────────────────────────────────────
    public ObservableCollection<AbstractTreeNode> RootNodes { get; } = [];

    // §2 ─ 검색 ───────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;

    // §3 ─ 노드 카운트 (StudioMainViewModel에서 구독) ─────────
    [ObservableProperty] private int _totalNodeCount;

    // §4 ─ 선택 상태 (RightPanelMode) ────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected))]
    [NotifyPropertyChangedFor(nameof(IsGroupSelected))]
    [NotifyPropertyChangedFor(nameof(IsDeviceSelected))]
    [NotifyPropertyChangedFor(nameof(IsPlcSelected))]
    [NotifyPropertyChangedFor(nameof(IsTagSelected))]
    private AbstractTreeNode? _selectedNode;

    // §5 ─ 패널 모드 프로퍼티 ─────────────────────────────────
    public bool IsNoneSelected   => SelectedNode is null;
    public bool IsGroupSelected  => SelectedNode is GroupTreeNode;
    public bool IsDeviceSelected => SelectedNode is DeviceTreeNode;
    public bool IsPlcSelected    => SelectedNode is PlcTreeNode;
    public bool IsTagSelected    => SelectedNode is TagTreeNode;

    // §6 ─ 편집기 DataContext ─────────────────────────────────
    public GroupTreeNode?  GroupEditor  => SelectedNode as GroupTreeNode;
    public DeviceTreeNode? DeviceEditor => SelectedNode as DeviceTreeNode;
    public PlcTreeNode?    PlcEditor    => SelectedNode as PlcTreeNode;
    public TagTreeNode?    TagEditor    => SelectedNode as TagTreeNode;

    // §7 ─ 생성자 ─────────────────────────────────────────────
    public DeviceTreeViewModel()
    {
        // 샘플 초기 데이터 (빈 트리)
        _InitSampleTree();
    }

    // §8 ─ 선택 처리 (코드비하인드에서 호출) ──────────────────
    /// <summary>TreeView SelectedItemChanged 이벤트에서 호출</summary>
    public void SelectNode(object? item)
    {
        SelectedNode = item as AbstractTreeNode;
    }

    // §9 ─ 커맨드 ─────────────────────────────────────────────
    [RelayCommand]
    private void AddGroup()
    {
        var node = new GroupTreeNode($"그룹 {RootNodes.Count + 1}");
        RootNodes.Add(node);
        _UpdateCount();
    }

    [RelayCommand]
    private void AddDevice()
    {
        if (SelectedNode is GroupTreeNode group)
        {
            var node = new DeviceTreeNode($"장비 {group.Children.Count + 1}");
            group.Children.Add(node);
            group.IsExpanded = true;
            SelectedNode = node;
            _UpdateCount();
        }
        else
        {
            // 루트에 그룹 없으면 먼저 그룹 생성 후 장비 추가
            AddGroup();
            if (RootNodes.LastOrDefault() is GroupTreeNode newGroup)
            {
                var node = new DeviceTreeNode("장비 1");
                newGroup.Children.Add(node);
                newGroup.IsExpanded = true;
                SelectedNode = node;
                _UpdateCount();
            }
        }
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode is null) return;
        _RemoveFromTree(RootNodes, SelectedNode);
        SelectedNode = null;
        _UpdateCount();
    }

    // §10 ─ 내부 헬퍼 ─────────────────────────────────────────
    private void _UpdateCount()
    {
        TotalNodeCount = _CountAll(RootNodes);
    }

    private static int _CountAll(IEnumerable<AbstractTreeNode> nodes)
    {
        int c = 0;
        foreach (var n in nodes)
        {
            c++;
            c += _CountAll(n.Children);
        }
        return c;
    }

    private static bool _RemoveFromTree(
        ObservableCollection<AbstractTreeNode> collection,
        AbstractTreeNode target)
    {
        if (collection.Remove(target)) return true;
        foreach (var node in collection)
            if (_RemoveFromTree(node.Children, target)) return true;
        return false;
    }

    private void _InitSampleTree()
    {
        // 빈 트리로 시작 (실제 데이터는 device.json에서 로드)
        // 필요 시 AddGroup 커맨드로 추가
    }
}
