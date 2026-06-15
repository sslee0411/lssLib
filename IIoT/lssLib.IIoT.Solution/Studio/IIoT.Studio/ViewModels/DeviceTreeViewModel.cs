// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/DeviceTreeViewModel.cs
//  역할: 장비 트리 ViewModel
//        - 트리 노드 CRUD (그룹/장비/PLC/Tag 추가·삭제)
//        - 선택 노드 관리 → 우측 패널 전환
//  S-01: 초기 구현
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

    // §1-1 ─ 상태 메시지 (하단 안내) ────────────────────────

    /// <summary>조작 결과 안내 메시지 (Tag 추가 실패 등)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    private string _statusMessage = string.Empty;

    /// <summary>상태 메시지 표시 여부</summary>
    public bool IsStatusVisible => !string.IsNullOrEmpty(StatusMessage);

    // §2 ─ 선택 노드 ──────────────────────────────────────────

    /// <summary>
    /// 현재 선택된 노드.
    /// ★ [NotifyPropertyChangedFor] 필수
    ///    소스 생성 프로퍼티를 nameof() 로 PropertyChanged 구독 시 컴파일 실패
    /// </summary>
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

    // §3 ─ 선택 타입 판별 프로퍼티 ───────────────────────────

    public bool IsNoneSelected   => SelectedNode is null;
    public bool IsGroupSelected  => SelectedNode is GroupTreeNode;
    public bool IsDeviceSelected => SelectedNode is DeviceTreeNode;
    public bool IsPlcSelected    => SelectedNode is PlcTreeNode;
    public bool IsTagSelected    => SelectedNode is TagTreeNode;

    // §4 ─ 편집기 캐스팅 프로퍼티 ────────────────────────────
    // S-02~S-05 에서 각 편집기 View 의 DataContext 로 사용

    public GroupTreeNode?  GroupEditor  => SelectedNode as GroupTreeNode;
    public DeviceTreeNode? DeviceEditor => SelectedNode as DeviceTreeNode;
    public PlcTreeNode?    PlcEditor    => SelectedNode as PlcTreeNode;
    public TagTreeNode?    TagEditor    => SelectedNode as TagTreeNode;

    // §5 ─ 선택 메서드 ────────────────────────────────────────

    /// <summary>
    /// TreeView SelectedItemChanged 코드비하인드에서 호출.
    /// ★ WPF TreeView.SelectedItem 은 TwoWay 바인딩 미지원
    ///    → 코드비하인드 이벤트로 직접 호출 필수
    /// </summary>
    public void SelectNode(object? item)
    {
        SelectedNode = item as AbstractTreeNode;
    }

    // §6 ─ 커맨드 ─────────────────────────────────────────────

    /*  허용 계층 규칙 (확정)
     *
     *  그룹  → 그룹 하위 가능  (그룹 중첩)
     *  그룹  → 장비 하위 가능
     *  장비  → 장비 하위 가능  (장비 중첩)
     *  장비  → PLC  하위 가능
     *  장비  → Tag  하위 가능
     *  PLC   → PLC  하위 가능  (PLC 중첩)
     *  PLC   → Tag  하위 가능
     *  Tag   → (자식 없음)
     *
     *  루트 배치 가능:  그룹 / 장비 / PLC
     *  루트 배치 불가:  Tag  ← ★ 반드시 PLC 또는 장비 하위에만 추가 가능
     */

    /// <summary>
    /// 그룹 추가.
    /// - 선택: 그룹 → 그룹 하위
    /// - 그 외 / 미선택 → 루트
    /// </summary>
    [RelayCommand]
    private void AddGroup()
    {
        var node = new GroupTreeNode($"그룹 {_CountAll<GroupTreeNode>() + 1}");

        if (SelectedNode is GroupTreeNode parentGroup)
            parentGroup.Children.Add(node);
        else
            RootNodes.Add(node);

        SelectedNode = node;
    }

    /// <summary>
    /// 장비 추가.
    /// - 선택: 그룹  → 그룹 하위
    /// - 선택: 장비  → 장비 하위  (장비 중첩)
    /// - 그 외 / 미선택 → 루트
    /// </summary>
    [RelayCommand]
    private void AddDevice()
    {
        var device = new DeviceTreeNode($"장비 {_CountAll<DeviceTreeNode>() + 1}");

        if (SelectedNode is GroupTreeNode group)
            group.Children.Add(device);
        else if (SelectedNode is DeviceTreeNode parentDevice)
            parentDevice.Children.Add(device);
        else
            RootNodes.Add(device);

        SelectedNode = device;
    }

    /// <summary>
    /// PLC 추가.
    /// - 선택: 장비  → 장비 하위
    /// - 선택: PLC   → PLC 하위   (PLC 중첩)
    /// - 그 외 / 미선택 → 루트
    /// </summary>
    [RelayCommand]
    private void AddPlc()
    {
        var plc = new PlcTreeNode($"PLC {_CountAll<PlcTreeNode>() + 1}");

        if (SelectedNode is DeviceTreeNode device)
            device.Children.Add(plc);
        else if (SelectedNode is PlcTreeNode parentPlc)
            parentPlc.Children.Add(plc);
        else
            RootNodes.Add(plc);

        SelectedNode = plc;
    }

    /// <summary>
    /// Tag 추가.
    /// ★ 규칙: Tag 는 반드시 PLC 또는 장비 하위에만 추가 가능
    ///         루트 추가 불가 / 그룹·Tag 하위 추가 불가
    /// - 선택: PLC  → PLC 하위
    /// - 선택: 장비 → 장비 하위
    /// - 그 외 / 미선택 → 추가 안 함 (StatusText 경고)
    /// </summary>
    [RelayCommand]
    private void AddTag()
    {
        if (SelectedNode is PlcTreeNode plc)
        {
            var tag = new TagTreeNode($"Tag {_CountAll<TagTreeNode>() + 1}");
            plc.Children.Add(tag);
            SelectedNode = tag;
        }
        else if (SelectedNode is DeviceTreeNode device)
        {
            var tag = new TagTreeNode($"Tag {_CountAll<TagTreeNode>() + 1}");
            device.Children.Add(tag);
            SelectedNode = tag;
        }
        else
        {
            // PLC 또는 장비를 먼저 선택해야 Tag 추가 가능
            StatusMessage = "⚠ Tag 는 PLC 또는 장비를 선택한 후 추가하세요.";
            // 3초 후 메시지 자동 클리어
            _ = Task.Delay(3000).ContinueWith(_ =>
                System.Windows.Application.Current?.Dispatcher.Invoke(
                    () => StatusMessage = string.Empty));
        }
    }

    /// <summary>선택된 노드 삭제</summary>
    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode is null) return;

        // 루트에서 삭제 시도
        if (RootNodes.Remove(SelectedNode))
        {
            SelectedNode = null;
            return;
        }

        // 자식 노드에서 재귀 탐색 후 삭제
        _RemoveFromChildren(RootNodes, SelectedNode);
        SelectedNode = null;
    }

    // §7 ─ 내부 헬퍼 ──────────────────────────────────────────

    /// <summary>전체 트리에서 특정 타입 노드 수 카운트</summary>
    private int _CountAll<T>() where T : AbstractTreeNode
    {
        int count = 0;
        _CountRecursive<T>(RootNodes, ref count);
        return count;
    }

    private static void _CountRecursive<T>(
        IEnumerable<AbstractTreeNode> nodes, ref int count) where T : AbstractTreeNode
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
