// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/DeviceTreeViewModel.cs
//  역할: 장비 트리 ViewModel
//  S-09 rev: 스케일·알람 라이브러리 VM 주입 추가
//  S-10 patch: 계층 규칙 확정
//  S-14: IsTemplateMode + ShowTemplateCommand 추가
//  S-17A: MoveUpCommand / MoveDownCommand 추가 (노드 순서 이동)
//  S-17C: SearchText + FilteredRootNodes + ClearSearchCommand 추가
//  S-20A: CopyNodeCommand / PasteNodeCommand / _DeepCopy 추가
//  S-24: ExpandAllCommand / CollapseAllCommand 추가
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class DeviceTreeViewModel : ObservableObject
{
    // §1 ─ 라이브러리 참조 ────────────────────────────────────

    public ScaleLibraryViewModel ScaleLibrary  { get; }
    public TagTemplateViewModel  TagTemplateVm { get; }
    public AlarmLibraryViewModel AlarmLibrary  { get; }

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public DeviceTreeViewModel(
        ScaleLibraryViewModel scaleLibrary,
        AlarmLibraryViewModel alarmLibrary,
        TagTemplateViewModel  tagTemplateVm)
    {
        ScaleLibrary  = scaleLibrary;
        AlarmLibrary  = alarmLibrary;
        TagTemplateVm = tagTemplateVm;

        // S-17C: RootNodes 변경 시 FilteredRootNodes 재계산
        RootNodes.CollectionChanged +=
            (_, _) => OnPropertyChanged(nameof(FilteredRootNodes));
    }

    // §3 ─ 루트 + 상태 메시지 ────────────────────────────────

    public ObservableCollection<AbstractTreeNode> RootNodes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    private string _statusMessage = string.Empty;

    public bool IsStatusVisible => !string.IsNullOrEmpty(StatusMessage);

    // §3-1 ─ S-17C: 검색 ─────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredRootNodes))]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    private string _searchText = string.Empty;

    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

    public IEnumerable<AbstractTreeNode> FilteredRootNodes =>
        string.IsNullOrWhiteSpace(SearchText)
            ? RootNodes
            : RootNodes.Where(n => _MatchSearch(n, SearchText));

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    private static bool _MatchSearch(AbstractTreeNode node, string q) =>
        node.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
        node.Children.Any(c => _MatchSearch(c, q));

    // §4 ─ 선택 노드 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected))]
    [NotifyPropertyChangedFor(nameof(IsGroupSelected))]
    [NotifyPropertyChangedFor(nameof(IsDeviceSelected))]
    [NotifyPropertyChangedFor(nameof(IsPlcSelected))]
    [NotifyPropertyChangedFor(nameof(IsTagSelected))]
    [NotifyPropertyChangedFor(nameof(GroupEditor))]
    [NotifyPropertyChangedFor(nameof(DeviceEditor))]
    [NotifyPropertyChangedFor(nameof(PlcEditor))]
    [NotifyPropertyChangedFor(nameof(TagEditor))]
    [NotifyPropertyChangedFor(nameof(ActiveEditor))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyNodeCommand))]
    private AbstractTreeNode? _selectedNode;

    // §5 ─ 선택 타입 판별 ─────────────────────────────────────

    public bool IsNoneSelected   => SelectedNode is null && !IsTemplateMode;
    public bool IsGroupSelected  => SelectedNode is GroupTreeNode;
    public bool IsDeviceSelected => SelectedNode is DeviceTreeNode;
    public bool IsPlcSelected    => SelectedNode is PlcTreeNode;
    public bool IsTagSelected    => SelectedNode is TagTreeNode;

    // §6 ─ 편집기 캐스팅 ──────────────────────────────────────

    public GroupTreeNode?  GroupEditor  => SelectedNode as GroupTreeNode;
    public DeviceTreeNode? DeviceEditor => SelectedNode as DeviceTreeNode;
    public PlcTreeNode?    PlcEditor    => SelectedNode as PlcTreeNode;
    public TagTreeNode?    TagEditor    => SelectedNode as TagTreeNode;

    public AbstractTreeNode? ActiveEditor => IsTemplateMode ? null : SelectedNode;

    // §7 ─ 템플릿 모드 (S-14) ─────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected))]
    [NotifyPropertyChangedFor(nameof(ActiveEditor))]
    private bool _isTemplateMode;

    [RelayCommand]
    private void ShowTemplate()
    {
        IsTemplateMode = !IsTemplateMode;
        if (IsTemplateMode) SelectedNode = null;
    }

    // §8 ─ 선택 메서드 ────────────────────────────────────────

    public void SelectNode(object? item)
    {
        SelectedNode   = item as AbstractTreeNode;
        IsTemplateMode = false;
    }

    [RelayCommand]
    private void ClearScale()
    {
        if (SelectedNode is TagTreeNode tag)
            tag.ScaleEntryId = null;
    }

    [RelayCommand]
    private void ClearAlarm()
    {
        if (SelectedNode is TagTreeNode tag)
            tag.AlarmEntryId = null;
    }

    // §9 ─ 추가 커맨드 ────────────────────────────────────────

    [RelayCommand]
    private void AddGroupSibling()
    {
        var node = new GroupTreeNode($"그룹 {_CountAll<GroupTreeNode>() + 1}");
        _AddAsSibling(node, allowedParentTypes: null);
    }

    [RelayCommand]
    private void AddGroupChild()
    {
        if (SelectedNode is TagTreeNode)
        { _ShowWarning("⚠ Tag 하위에는 추가할 수 없습니다."); return; }
        var node = new GroupTreeNode($"그룹 {_CountAll<GroupTreeNode>() + 1}");
        _AddAsChild(node);
    }

    [RelayCommand]
    private void AddDeviceSibling()
    {
        var node = new DeviceTreeNode($"장비 {_CountAll<DeviceTreeNode>() + 1}");
        _AddAsSibling(node, allowedParentTypes: null);
    }

    [RelayCommand]
    private void AddDeviceChild()
    {
        if (SelectedNode is TagTreeNode)
        { _ShowWarning("⚠ Tag 하위에는 장비를 추가할 수 없습니다."); return; }
        var node = new DeviceTreeNode($"장비 {_CountAll<DeviceTreeNode>() + 1}");
        _AddAsChild(node);
    }

    [RelayCommand]
    private void AddPlcSibling()
    {
        var node = new PlcTreeNode($"PLC {_CountAll<PlcTreeNode>() + 1}");
        _AddAsSibling(node, allowedParentTypes: null);
    }

    [RelayCommand]
    private void AddPlcChild()
    {
        if (SelectedNode is TagTreeNode)
        { _ShowWarning("⚠ Tag 하위에는 PLC를 추가할 수 없습니다."); return; }
        var node = new PlcTreeNode($"PLC {_CountAll<PlcTreeNode>() + 1}");
        _AddAsChild(node);
    }

    [RelayCommand]
    private void AddTag()
    {
        var newTag = new TagTreeNode($"Tag {_CountAll<TagTreeNode>() + 1}");

        if (SelectedNode is PlcTreeNode or DeviceTreeNode)
        {
            SelectedNode.Children.Add(newTag);
            SelectedNode = newTag;
            return;
        }

        if (SelectedNode is TagTreeNode)
        {
            var (parentCol, parentNode) = _FindParentCollection(RootNodes, SelectedNode);
            if (parentCol is not null && parentNode is PlcTreeNode or DeviceTreeNode)
            {
                var idx = parentCol.IndexOf(SelectedNode);
                if (idx >= 0) parentCol.Insert(idx + 1, newTag);
                else          parentCol.Add(newTag);
                SelectedNode = newTag;
                return;
            }
        }

        if (SelectedNode is null)
            _ShowWarning("⚠ Tag 추가 전 PLC 또는 장비를 먼저 선택하세요.");
        else
            _ShowWarning("⚠ Tag 는 PLC 또는 장비 하위에만 추가할 수 있습니다.");
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode is null) return;
        if (RootNodes.Remove(SelectedNode)) { SelectedNode = null; return; }
        _RemoveFromChildren(RootNodes, SelectedNode);
        SelectedNode = null;
    }

    // §10 ─ S-17A: 순서 이동 커맨드 ──────────────────────────

    [RelayCommand(CanExecute = nameof(_CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedNode is null) return;
        var list = _GetParentList(SelectedNode);
        if (list is null) return;
        var idx = list.IndexOf(SelectedNode);
        if (idx <= 0) return;
        list.Move(idx, idx - 1);
    }

    private bool _CanMoveUp()
    {
        if (SelectedNode is null) return false;
        var list = _GetParentList(SelectedNode);
        if (list is null) return false;
        return list.IndexOf(SelectedNode) > 0;
    }

    [RelayCommand(CanExecute = nameof(_CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedNode is null) return;
        var list = _GetParentList(SelectedNode);
        if (list is null) return;
        var idx = list.IndexOf(SelectedNode);
        if (idx < 0 || idx >= list.Count - 1) return;
        list.Move(idx, idx + 1);
    }

    private bool _CanMoveDown()
    {
        if (SelectedNode is null) return false;
        var list = _GetParentList(SelectedNode);
        if (list is null) return false;
        var idx = list.IndexOf(SelectedNode);
        return idx >= 0 && idx < list.Count - 1;
    }

    // §11 ─ 헬퍼 ─────────────────────────────────────────────

    private ObservableCollection<AbstractTreeNode>? _GetParentList(AbstractTreeNode node)
    {
        if (RootNodes.Contains(node)) return RootNodes;
        var (col, _) = _FindParentCollection(RootNodes, node);
        return col;
    }

    private void _AddAsSibling(AbstractTreeNode newNode, Type[]? allowedParentTypes)
    {
        if (SelectedNode is null) { _AppendToRoot(newNode); return; }
        var (parentCol, parentNode) = _FindParentCollection(RootNodes, SelectedNode);
        if (parentCol is null) { _AppendToRoot(newNode); return; }
        if (allowedParentTypes is not null && parentNode is not null
            && !allowedParentTypes.Contains(parentNode.GetType()))
        { _ShowWarning("⚠ 해당 위치에는 추가할 수 없습니다."); return; }
        var idx = parentCol.IndexOf(SelectedNode);
        if (idx >= 0) parentCol.Insert(idx + 1, newNode);
        else          parentCol.Add(newNode);
        SelectedNode = newNode;
    }

    private void _AddAsChild(AbstractTreeNode newNode)
    {
        if (SelectedNode is null) { _AppendToRoot(newNode); return; }
        SelectedNode.Children.Add(newNode);
        SelectedNode = newNode;
    }

    private void _AppendToRoot(AbstractTreeNode node)
    {
        RootNodes.Add(node);
        SelectedNode = node;
    }

    private void _ShowWarning(string message)
    {
        StatusMessage = message;
        _ = Task.Delay(3000).ContinueWith(_ =>
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => StatusMessage = string.Empty));
    }

    private (ObservableCollection<AbstractTreeNode>? collection,
             AbstractTreeNode? parent)
        _FindParentCollection(
            ObservableCollection<AbstractTreeNode> nodes,
            AbstractTreeNode target)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Contains(target)) return (node.Children, node);
            var (col, par) = _FindParentCollection(node.Children, target);
            if (col is not null) return (col, par);
        }
        return (null, null);
    }

    private int _CountAll<T>() where T : AbstractTreeNode
    {
        int count = 0;
        _CountRecursive<T>(RootNodes, ref count);
        return count;
    }

    private static void _CountRecursive<T>(
        IEnumerable<AbstractTreeNode> nodes, ref int count)
        where T : AbstractTreeNode
    {
        foreach (var node in nodes)
        {
            if (node is T) count++;
            _CountRecursive<T>(node.Children, ref count);
        }
    }

    private static bool _RemoveFromChildren(
        ObservableCollection<AbstractTreeNode> nodes, AbstractTreeNode target)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Remove(target)) return true;
            if (_RemoveFromChildren(node.Children, target)) return true;
        }
        return false;
    }

    // §12 ─ ★ S-20A: 복사·붙여넣기 커맨드 ───────────────────

    private AbstractTreeNode? _clipboardNode;

    [RelayCommand(CanExecute = nameof(_CanCopy))]
    private void CopyNode()
    {
        if (SelectedNode is null) return;
        _clipboardNode = _DeepCopy(SelectedNode, isCopy: true);
        _ShowWarning($"📋 '{SelectedNode.Name}' 복사됨 — Ctrl+V 로 붙여넣기");

        // PasteNodeCommand CanExecute 재평가
        PasteNodeCommand.NotifyCanExecuteChanged();
    }

    private bool _CanCopy() => SelectedNode is not null;

    [RelayCommand(CanExecute = nameof(_CanPaste))]
    private void PasteNode()
    {
        if (_clipboardNode is null) return;

        // 매번 새 복사본 생성 (여러 번 붙여넣기 지원)
        var newNode = _DeepCopy(_clipboardNode, isCopy: false);

        if (SelectedNode is null)
        {
            RootNodes.Add(newNode);
        }
        else if (SelectedNode is GroupTreeNode)
        {
            SelectedNode.Children.Add(newNode);
        }
        else
        {
            var (parentCol, _) = _FindParentCollection(RootNodes, SelectedNode);
            var list = parentCol ?? RootNodes;
            var idx  = list.IndexOf(SelectedNode);
            if (idx >= 0) list.Insert(idx + 1, newNode);
            else          list.Add(newNode);
        }

        SelectedNode = newNode;
    }

    private bool _CanPaste() => _clipboardNode is not null;

    private static AbstractTreeNode _DeepCopy(AbstractTreeNode src, bool isCopy)
    {
        var suffix = isCopy ? " (복사본)" : string.Empty;

        AbstractTreeNode copy = src switch
        {
            GroupTreeNode g => new GroupTreeNode(g.Name + suffix)
            {
                Description = g.Description
            },
            DeviceTreeNode d => new DeviceTreeNode(d.Name + suffix)
            {
                Description  = d.Description,
                Model        = d.Model,
                Manufacturer = d.Manufacturer,
                Location     = d.Location,
                CommType     = d.CommType,
                Host         = d.Host,
                Port         = d.Port,
                PollMs       = d.PollMs
            },
            PlcTreeNode p => new PlcTreeNode(p.Name + suffix)
            {
                Description = p.Description,
                CommType    = p.CommType,
                Host        = p.Host,
                Port        = p.Port,
                PollMs      = p.PollMs
            },
            TagTreeNode t => new TagTreeNode(t.Name + suffix)
            {
                Description  = t.Description,
                Address      = t.Address,
                DataType     = t.DataType,
                Unit         = t.Unit,
                ScaleEntryId = t.ScaleEntryId,
                AlarmEntryId = t.AlarmEntryId
            },
            _ => new GroupTreeNode(src.Name + suffix)
        };

        foreach (var child in src.Children)
            copy.Children.Add(_DeepCopy(child, isCopy: false));

        return copy;
    }

    // §13 ─ ★ S-24: 전체 펼침·접힘 커맨드 ──────────────────

    /// <summary>전체 트리 펼치기 — 모든 노드 IsExpanded = true</summary>
    [RelayCommand]
    private void ExpandAll() => _SetExpanded(RootNodes, true);

    /// <summary>전체 트리 접기 — 모든 노드 IsExpanded = false</summary>
    [RelayCommand]
    private void CollapseAll() => _SetExpanded(RootNodes, false);

    private static void _SetExpanded(
        IEnumerable<AbstractTreeNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            _SetExpanded(node.Children, expanded);
        }
    }
}
