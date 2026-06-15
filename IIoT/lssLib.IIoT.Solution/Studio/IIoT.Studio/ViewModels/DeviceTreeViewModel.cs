// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/DeviceTreeViewModel.cs
//  역할: 장비 트리 ViewModel
//        - 트리 노드 CRUD (그룹/장비/PLC/Tag 추가·삭제)
//        - 선택 노드 관리 → 우측 패널 전환
//  S-01 rev2: 같은 레벨(형제) 추가 기능 적용
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class DeviceTreeViewModel : ObservableObject
{
    // §1 ─ 트리 루트 ──────────────────────────────────────────

    /// <summary>트리 루트 노드 컬렉션 (TreeView ItemsSource)</summary>
    public ObservableCollection<AbstractTreeNode> RootNodes { get; } = new();

    // §1-1 ─ 상태 메시지 ─────────────────────────────────────

    /// <summary>조작 결과 안내 메시지 (Tag 추가 실패 등)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    private string _statusMessage = string.Empty;

    /// <summary>상태 메시지 표시 여부</summary>
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

    // §5 ─ 선택 메서드 ────────────────────────────────────────

    public void SelectNode(object? item)
    {
        SelectedNode = item as AbstractTreeNode;
    }

    // §6 ─ 커맨드 ─────────────────────────────────────────────

    /*  ★ 추가 위치 결정 규칙 (확정)
     *
     *  [선택 노드 있음]
     *    → 선택 노드와 같은 레벨(형제)에 추가
     *      (= 선택 노드의 부모 컬렉션에 삽입)
     *    단, Tag 는 부모가 PLC 또는 장비여야만 허용
     *
     *  [선택 노드 없음 / 루트 노드 선택]
     *    → 루트에 추가 (그룹/장비/PLC 만 가능, Tag 불가)
     *
     *  허용 계층 규칙:
     *    그룹 → 그룹·장비 하위
     *    장비 → 장비·PLC·Tag 하위
     *    PLC  → PLC·Tag 하위
     *    Tag  → 자식 없음, 반드시 PLC/장비 하위
     */

    /// <summary>
    /// 그룹 추가.
    /// 선택 노드 있음 → 같은 레벨(형제) 추가
    /// 미선택 → 루트 추가
    /// </summary>
    [RelayCommand]
    private void AddGroup()
    {
        var node = new GroupTreeNode($"그룹 {_CountAll<GroupTreeNode>() + 1}");
        _AddSibling(node, allowedParentTypes: null); // 어느 부모 아래든 허용
    }

    /// <summary>
    /// 장비 추가.
    /// 선택 노드 있음 → 같은 레벨(형제) 추가
    /// 미선택 → 루트 추가
    /// </summary>
    [RelayCommand]
    private void AddDevice()
    {
        var node = new DeviceTreeNode($"장비 {_CountAll<DeviceTreeNode>() + 1}");
        _AddSibling(node, allowedParentTypes: null);
    }

    /// <summary>
    /// PLC 추가.
    /// 선택 노드 있음 → 같은 레벨(형제) 추가
    /// 미선택 → 루트 추가
    /// </summary>
    [RelayCommand]
    private void AddPlc()
    {
        var node = new PlcTreeNode($"PLC {_CountAll<PlcTreeNode>() + 1}");
        _AddSibling(node, allowedParentTypes: null);
    }

    /// <summary>
    /// Tag 추가.
    /// ★ 부모가 PLC 또는 장비여야만 허용 (루트·그룹 하위 불가)
    /// 선택 노드 있음 → 같은 레벨(형제) 추가
    /// </summary>
    [RelayCommand]
    private void AddTag()
    {
        var node = new TagTreeNode($"Tag {_CountAll<TagTreeNode>() + 1}");
        _AddSibling(node,
            allowedParentTypes: new[] { typeof(PlcTreeNode), typeof(DeviceTreeNode) },
            failMessage: "⚠ Tag 는 PLC 또는 장비 하위에만 추가할 수 있습니다.");
    }

    /// <summary>선택된 노드 삭제</summary>
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

    // §7 ─ 핵심 헬퍼: 형제 추가 ──────────────────────────────

    /// <summary>
    /// 선택 노드와 같은 레벨(형제)에 노드를 추가한다.
    ///
    /// [선택 노드 있음]
    ///   - 부모 컬렉션을 역추적하여 선택 노드 바로 뒤에 삽입
    ///   - allowedParentTypes 지정 시: 부모 타입이 허용 목록에 없으면 failMessage 표시
    ///
    /// [선택 노드 없음]
    ///   - allowedParentTypes == null → 루트에 추가
    ///   - allowedParentTypes != null → failMessage 표시 (Tag 루트 불가)
    /// </summary>
    private void _AddSibling(
        AbstractTreeNode newNode,
        Type[]? allowedParentTypes,
        string failMessage = "")
    {
        if (SelectedNode is null)
        {
            // 미선택
            if (allowedParentTypes is null)
                _AppendToRoot(newNode);
            else
                _ShowWarning(failMessage);
            return;
        }

        // 선택 노드의 부모 컬렉션 탐색
        var (parentCollection, parentNode) =
            _FindParentCollection(RootNodes, SelectedNode);

        if (parentCollection is null)
        {
            // 선택 노드가 루트 레벨
            if (allowedParentTypes is null)
                _AppendToRoot(newNode);
            else
                _ShowWarning(failMessage);
            return;
        }

        // 부모 타입 허용 검사
        if (allowedParentTypes is not null && parentNode is not null)
        {
            if (!allowedParentTypes.Contains(parentNode.GetType()))
            {
                _ShowWarning(failMessage);
                return;
            }
        }

        // 선택 노드 바로 뒤에 삽입
        var idx = parentCollection.IndexOf(SelectedNode);
        if (idx >= 0)
            parentCollection.Insert(idx + 1, newNode);
        else
            parentCollection.Add(newNode);

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

    /// <summary>
    /// target 노드의 부모 컬렉션과 부모 노드를 반환한다.
    /// 루트 레벨이면 (null, null) 반환.
    /// </summary>
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
            if (col is not null)
                return (col, par);
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