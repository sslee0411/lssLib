// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/DeviceTreeViewModel.cs
//  역할: 장비 트리 ViewModel
//  S-09 rev: 스케일·알람 라이브러리 VM 주입 추가
//            → TagEditorView 콤보박스 ItemsSource 제공
//  S-10 patch: 계층 규칙 확정
//              그룹 하위: 그룹·장비·PLC
//              장비 하위: 장비·PLC·Tag
//              PLC  하위: PLC·장비·Tag
//              Tag  하위: 없음
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class DeviceTreeViewModel : ObservableObject
{
    // §1 ─ 라이브러리 참조 ────────────────────────────────────

    /// <summary>
    /// 스케일 라이브러리 — TagEditorView 콤보박스 ItemsSource.
    /// MainViewModel 이 동일 인스턴스를 DeviceTree 와 ScaleLibrary 양쪽에 주입.
    /// </summary>
    public ScaleLibraryViewModel ScaleLibrary { get; }

    /// <summary>알람 라이브러리 — TagEditorView 콤보박스 ItemsSource.</summary>
    public AlarmLibraryViewModel AlarmLibrary { get; }

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public DeviceTreeViewModel(
        ScaleLibraryViewModel scaleLibrary,
        AlarmLibraryViewModel alarmLibrary)
    {
        ScaleLibrary = scaleLibrary;
        AlarmLibrary = alarmLibrary;
    }

    // §3 ─ 루트 + 상태 메시지 ────────────────────────────────

    public ObservableCollection<AbstractTreeNode> RootNodes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    private string _statusMessage = string.Empty;

    public bool IsStatusVisible => !string.IsNullOrEmpty(StatusMessage);

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
    private AbstractTreeNode? _selectedNode;

    // §5 ─ 선택 타입 판별 ─────────────────────────────────────

    public bool IsNoneSelected => SelectedNode is null;
    public bool IsGroupSelected => SelectedNode is GroupTreeNode;
    public bool IsDeviceSelected => SelectedNode is DeviceTreeNode;
    public bool IsPlcSelected => SelectedNode is PlcTreeNode;
    public bool IsTagSelected => SelectedNode is TagTreeNode;

    // §6 ─ 편집기 캐스팅 ──────────────────────────────────────

    public GroupTreeNode? GroupEditor => SelectedNode as GroupTreeNode;
    public DeviceTreeNode? DeviceEditor => SelectedNode as DeviceTreeNode;
    public PlcTreeNode? PlcEditor => SelectedNode as PlcTreeNode;
    public TagTreeNode? TagEditor => SelectedNode as TagTreeNode;

    /// <summary>
    /// 현재 활성 편집기 노드 — ContentControl 바인딩용.
    /// null 이면 ContentControl 이 아무것도 렌더링하지 않음.
    /// ★ 편집기 겹침 버그 완전 방지
    /// </summary>
    public AbstractTreeNode? ActiveEditor => SelectedNode;

    // §7 ─ 선택 메서드 ────────────────────────────────────────

    public void SelectNode(object? item)
        => SelectedNode = item as AbstractTreeNode;

    // §7-1 ─ 라이브러리 연결 해제 커맨드 ─────────────────────

    /// <summary>선택된 Tag 의 스케일 연결 해제</summary>
    [RelayCommand]
    private void ClearScale()
    {
        if (SelectedNode is TagTreeNode tag)
            tag.ScaleEntryId = null;
    }

    /// <summary>선택된 Tag 의 알람 연결 해제</summary>
    [RelayCommand]
    private void ClearAlarm()
    {
        if (SelectedNode is TagTreeNode tag)
            tag.AlarmEntryId = null;
    }

    // §8 ─ 커맨드 (B안: 타입별 형제/하위 분리) ────────────────

    /*  ━━━ 확정 계층 규칙 (S-10 patch) ━━━
     *
     *    그룹  하위: 그룹·장비·PLC
     *    장비  하위: 장비·PLC·Tag
     *    PLC   하위: PLC·장비·Tag
     *    Tag   하위: 없음 (자식 불가)
     *
     *    공통: Tag 하위에는 어떤 노드도 추가 불가
     *    Tag 위치: PLC 또는 장비 하위에만 배치 가능
     */

    // ── 그룹 ──────────────────────────────────────────────

    [RelayCommand]
    private void AddGroupSibling()
    {
        var node = new GroupTreeNode($"그룹 {_CountAll<GroupTreeNode>() + 1}");
        _AddAsSibling(node, allowedParentTypes: null);
    }

    [RelayCommand]
    private void AddGroupChild()
    {
        // Tag 하위에만 추가 불가
        if (SelectedNode is TagTreeNode)
        {
            _ShowWarning("⚠ Tag 하위에는 추가할 수 없습니다.");
            return;
        }
        var node = new GroupTreeNode($"그룹 {_CountAll<GroupTreeNode>() + 1}");
        _AddAsChild(node);
    }

    // ── 장비 ──────────────────────────────────────────────

    [RelayCommand]
    private void AddDeviceSibling()
    {
        var node = new DeviceTreeNode($"장비 {_CountAll<DeviceTreeNode>() + 1}");
        _AddAsSibling(node, allowedParentTypes: null);
    }

    [RelayCommand]
    private void AddDeviceChild()
    {
        // ★ S-10 patch: Tag 하위에만 불가 (PLC 하위 장비 허용)
        if (SelectedNode is TagTreeNode)
        {
            _ShowWarning("⚠ Tag 하위에는 장비를 추가할 수 없습니다.");
            return;
        }
        var node = new DeviceTreeNode($"장비 {_CountAll<DeviceTreeNode>() + 1}");
        _AddAsChild(node);
    }

    // ── PLC ───────────────────────────────────────────────

    [RelayCommand]
    private void AddPlcSibling()
    {
        var node = new PlcTreeNode($"PLC {_CountAll<PlcTreeNode>() + 1}");
        _AddAsSibling(node, allowedParentTypes: null);
    }

    [RelayCommand]
    private void AddPlcChild()
    {
        // ★ S-10 patch: Tag 하위에만 불가 (그룹 하위 PLC 허용)
        if (SelectedNode is TagTreeNode)
        {
            _ShowWarning("⚠ Tag 하위에는 PLC를 추가할 수 없습니다.");
            return;
        }
        var node = new PlcTreeNode($"PLC {_CountAll<PlcTreeNode>() + 1}");
        _AddAsChild(node);
    }

    // ── Tag ───────────────────────────────────────────────

    /// <summary>
    /// Tag 추가.
    /// ① PLC / 장비 선택 → 해당 노드 하위에 추가
    /// ② Tag 선택       → 같은 부모(형제)에 연속 추가
    /// ③ 미선택 / 그룹  → 경고
    /// ★ 부모는 반드시 PLC 또는 장비여야 함
    /// </summary>
    [RelayCommand]
    private void AddTag()
    {
        var newTag = new TagTreeNode($"Tag {_CountAll<TagTreeNode>() + 1}");

        // ① PLC / 장비 선택 → 하위 추가
        if (SelectedNode is PlcTreeNode or DeviceTreeNode)
        {
            SelectedNode.Children.Add(newTag);
            SelectedNode = newTag;
            return;
        }

        // ② Tag 선택 → 같은 부모(형제)에 연속 추가
        if (SelectedNode is TagTreeNode)
        {
            var (parentCol, parentNode) =
                _FindParentCollection(RootNodes, SelectedNode);

            if (parentCol is not null
                && parentNode is PlcTreeNode or DeviceTreeNode)
            {
                var idx = parentCol.IndexOf(SelectedNode);
                if (idx >= 0)
                    parentCol.Insert(idx + 1, newTag);
                else
                    parentCol.Add(newTag);

                SelectedNode = newTag;
                return;
            }
        }

        // ③ 그 외 → 경고
        if (SelectedNode is null)
            _ShowWarning("⚠ Tag 추가 전 PLC 또는 장비를 먼저 선택하세요.");
        else
            _ShowWarning("⚠ Tag 는 PLC 또는 장비 하위에만 추가할 수 있습니다.");
    }

    // ── 삭제 ──────────────────────────────────────────────

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode is null) return;

        if (RootNodes.Remove(SelectedNode))
        {
            SelectedNode = null;
            return;
        }

        _RemoveFromChildren(RootNodes, SelectedNode);
        SelectedNode = null;
    }

    // §9 ─ 핵심 헬퍼: 형제 / 하위 추가 ──────────────────────

    private void _AddAsSibling(AbstractTreeNode newNode, Type[]? allowedParentTypes)
    {
        if (SelectedNode is null)
        {
            _AppendToRoot(newNode);
            return;
        }

        var (parentCol, parentNode) = _FindParentCollection(RootNodes, SelectedNode);

        if (parentCol is null)
        {
            _AppendToRoot(newNode);
            return;
        }

        if (allowedParentTypes is not null && parentNode is not null
            && !allowedParentTypes.Contains(parentNode.GetType()))
        {
            _ShowWarning("⚠ 해당 위치에는 추가할 수 없습니다.");
            return;
        }

        var idx = parentCol.IndexOf(SelectedNode);
        if (idx >= 0)
            parentCol.Insert(idx + 1, newNode);
        else
            parentCol.Add(newNode);

        SelectedNode = newNode;
    }

    private void _AddAsChild(AbstractTreeNode newNode)
    {
        if (SelectedNode is null)
        {
            _AppendToRoot(newNode);
            return;
        }

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

    // §10 ─ 내부 헬퍼: 부모 역추적 / 카운트 / 삭제 ───────────

    private (ObservableCollection<AbstractTreeNode>? collection,
             AbstractTreeNode? parent)
        _FindParentCollection(
            ObservableCollection<AbstractTreeNode> nodes,
            AbstractTreeNode target)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Contains(target))
                return (node.Children, node);

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
}