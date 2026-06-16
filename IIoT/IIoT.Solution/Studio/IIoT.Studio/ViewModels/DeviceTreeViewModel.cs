// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/DeviceTreeViewModel.cs
//  역할: 장비 트리 ViewModel
//        - 트리 노드 CRUD
//        - 선택 노드 관리 → 우측 패널 전환
//  S-01 rev3: B안 적용 — 타입별 형제/하위 커맨드 분리
//             Tag 는 하위 전용 단독 버튼
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class DeviceTreeViewModel : ObservableObject
{
    // §1 ─ 루트 + 상태 메시지 ────────────────────────────────

    public ObservableCollection<AbstractTreeNode> RootNodes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    private string _statusMessage = string.Empty;

    public bool IsStatusVisible => !string.IsNullOrEmpty(StatusMessage);

    // §2 ─ 선택 노드 ──────────────────────────────────────────

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

    // §3 ─ 선택 타입 판별 ─────────────────────────────────────

    public bool IsNoneSelected => SelectedNode is null;
    public bool IsGroupSelected => SelectedNode is GroupTreeNode;
    public bool IsDeviceSelected => SelectedNode is DeviceTreeNode;
    public bool IsPlcSelected => SelectedNode is PlcTreeNode;
    public bool IsTagSelected => SelectedNode is TagTreeNode;

    // §4 ─ 편집기 캐스팅 ──────────────────────────────────────

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

    // §5 ─ 선택 메서드 ────────────────────────────────────────

    public void SelectNode(object? item)
        => SelectedNode = item as AbstractTreeNode;

    // §6 ─ 커맨드 (B안: 타입별 형제/하위 분리) ────────────────

    /*  ━━━ 추가 위치 규칙 ━━━
     *
     *  [형제로 추가]
     *    선택 노드 있음 → 선택 노드와 같은 레벨(부모 컬렉션)에 삽입
     *    선택 노드 없음 → 루트에 추가 (Tag 제외)
     *
     *  [하위로 추가]
     *    선택 노드 있음 → 선택 노드의 자식 컬렉션에 추가
     *    선택 노드 없음 → 루트에 추가 (Tag 제외)
     *
     *  ━━━ 계층 허용 규칙 ━━━
     *    그룹  하위: 그룹·장비
     *    장비  하위: 장비·PLC·Tag
     *    PLC   하위: PLC·Tag
     *    Tag   하위: 없음 (자식 불가)
     *    Tag 위치: 반드시 PLC 또는 장비 하위 (루트·그룹 불가)
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
        if (SelectedNode is PlcTreeNode or TagTreeNode)
        {
            _ShowWarning("⚠ PLC·Tag 하위에는 장비를 추가할 수 없습니다.");
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
        if (SelectedNode is GroupTreeNode or TagTreeNode)
        {
            _ShowWarning("⚠ 그룹·Tag 하위에는 PLC를 추가할 수 없습니다.");
            return;
        }
        var node = new PlcTreeNode($"PLC {_CountAll<PlcTreeNode>() + 1}");
        _AddAsChild(node);
    }

    // ── Tag (하위 추가 + Tag 선택 시 형제 연속 추가) ────────

    /// <summary>
    /// Tag 추가.
    /// ① PLC / 장비 선택 → 해당 노드 하위에 추가
    /// ② Tag 선택       → 같은 부모(형제)에 연속 추가  ← 신규
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

    // §7 ─ 핵심 헬퍼: 형제 / 하위 추가 ──────────────────────

    /// <summary>
    /// 형제로 추가 — 선택 노드의 부모 컬렉션에 삽입.
    /// 선택 없음 → 루트 추가 (allowedParentTypes 에 상관없이).
    /// </summary>
    private void _AddAsSibling(AbstractTreeNode newNode, Type[]? allowedParentTypes)
    {
        if (SelectedNode is null)
        {
            // 미선택 → 루트
            _AppendToRoot(newNode);
            return;
        }

        var (parentCol, parentNode) = _FindParentCollection(RootNodes, SelectedNode);

        if (parentCol is null)
        {
            // 선택 노드가 루트 레벨 → 루트에 형제 추가
            _AppendToRoot(newNode);
            return;
        }

        // 부모 타입 허용 검사
        if (allowedParentTypes is not null && parentNode is not null
            && !allowedParentTypes.Contains(parentNode.GetType()))
        {
            _ShowWarning($"⚠ 해당 위치에는 추가할 수 없습니다.");
            return;
        }

        var idx = parentCol.IndexOf(SelectedNode);
        if (idx >= 0)
            parentCol.Insert(idx + 1, newNode);
        else
            parentCol.Add(newNode);

        SelectedNode = newNode;
    }

    /// <summary>
    /// 하위로 추가 — 선택 노드의 Children 에 추가.
    /// 선택 없음 → 루트 추가.
    /// </summary>
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

    // §8 ─ 내부 헬퍼: 부모 역추적 / 카운트 / 삭제 ────────────

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