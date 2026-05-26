// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceTreeView.xaml.cs
//  역할: 장비 트리뷰 코드 비하인드 — 인라인 편집 입력 처리
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — 다중 선택 버그 (IsSelected OneWayToSource)
//  수정: 2025-05-23 v3 — IsEditing 유지 버그 (CommitEdit 강제)
//  수정: 2025-05-26 v4 — IsEditing 잔존 버그 (이중 추적 확인)
//  수정: 2025-05-26 v5 — 더블클릭 시 상위 노드 에디트 박스 활성화 버그 수정
//        [원인]
//        Control.MouseDoubleClick 은 WPF에서 Direct 라우팅 이벤트.
//        MouseLeftButtonDown(Bubbling)이 부모 TreeViewItem까지 전파되면서
//        각 TreeViewItem마다 MouseDoubleClick 이 독립적으로 발생함.
//        e.Handled = true 는 Direct 이벤트에서 상위 요소 발생을 차단하지 못함.
//        [해결]
//        e.OriginalSource 에서 visual tree를 역추적하여
//        이벤트 발생 위치가 현재 TreeViewItem 의 직속 콘텐츠인지
//        자식 TreeViewItem 내부인지 판별 → 자식 내부이면 무시
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.ViewModels.DeviceTree;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IIoT.DeviceManager.Views.DeviceTree;

public partial class DeviceTreeView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public DeviceTreeView()
    {
        InitializeComponent();
    }

    // §2 ─ 선택 변경 ──────────────────────────────────────────

    /// <summary>
    /// 트리 선택 변경 → ViewModel.SelectedNode 동기화.
    ///
    /// [경로 A] e.OldValue: TreeView가 추적하는 이전 선택 노드
    /// [경로 B] vm.SelectedNode: Add* 커맨드로 설정된 VM 현재 노드
    ///   - 툴바 Add* 후 첫 TreeView 클릭 시 e.OldValue=null 이지만
    ///     vm.SelectedNode 에 편집 중인 노드가 있을 수 있음
    /// </summary>
    private void DeviceTree_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not DeviceTreeViewModel vm) return;

        // ── [경로 A] TreeView 추적 기준 ──────────────────────
        if (e.OldValue is DeviceNodeViewModel oldTreeNode)
        {
            if (oldTreeNode.IsEditing)
                oldTreeNode.CommitEditCommand.Execute(null);
            oldTreeNode.IsSelected = false;
        }

        // ── [경로 B] VM 추적 기준 ────────────────────────────
        var vmCurrent = vm.SelectedNode;
        if (vmCurrent is { IsEditing: true }
            && !ReferenceEquals(vmCurrent, e.OldValue)
            && !ReferenceEquals(vmCurrent, e.NewValue))
        {
            vmCurrent.CommitEditCommand.Execute(null);
        }

        // ── 새 노드 IsSelected 동기화 ─────────────────────────
        var newNode = e.NewValue as DeviceNodeViewModel;
        if (newNode is not null)
            newNode.IsSelected = true;

        vm.SelectedNode = newNode;
    }

    // §3 ─ 더블클릭 편집 ──────────────────────────────────────

    /// <summary>
    /// 더블클릭 → 인라인 이름 편집 시작.
    ///
    /// ★ v5 핵심 수정: 자식 TreeViewItem 에서 발생한 더블클릭 무시
    ///
    /// [문제]
    ///   Control.MouseDoubleClick 은 WPF에서 Direct RoutedEvent.
    ///   MouseLeftButtonDown(Bubbling)이 부모 TreeViewItem까지 전파되면서
    ///   각 TreeViewItem마다 MouseDoubleClick 이 독립적으로 발생.
    ///   e.Handled = true 는 Direct 이벤트에서 상위 요소 발생을 차단하지 못함.
    ///   → 자식 더블클릭 시 부모·조부모 모두 BeginEditCommand 가 호출됨.
    ///
    /// [해결]
    ///   _IsEventFromChildTreeViewItem() 으로 e.OriginalSource 에서
    ///   Visual Tree 를 역추적:
    ///     - sender TreeViewItem 에 도달하기 전에 다른 TreeViewItem 발견
    ///       → 자식 노드에서 발생한 이벤트 → return (무시)
    ///     - sender TreeViewItem 에 바로 도달
    ///       → 이 노드 자신의 콘텐츠에서 발생 → BeginEditCommand 실행
    /// </summary>
    private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem tvi) return;
        if (tvi.DataContext is not DeviceNodeViewModel node) return;

        // ★ 자식 TreeViewItem 에서 발생한 이벤트 무시 (v5 핵심 수정)
        if (_IsEventFromChildTreeViewItem(tvi, e.OriginalSource as DependencyObject))
            return;

        // 이미 편집 중이면 중복 진입 방지
        if (node.IsEditing) return;

        node.BeginEditCommand.Execute(null);
        e.Handled = true;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => FocusEditBox(tvi));
    }

    // §4 ─ 키 입력 처리 ───────────────────────────────────────

    private void TreeViewItem_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TreeViewItem { DataContext: DeviceNodeViewModel node }) return;
        if (!node.IsEditing) return;

        switch (e.Key)
        {
            case Key.Enter:
                node.CommitEditCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                node.CancelEditCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // §5 ─ 내부 헬퍼 ──────────────────────────────────────────

    /// <summary>
    /// 이벤트가 자식 TreeViewItem 내부에서 발생했는지 판별합니다.
    ///
    /// e.OriginalSource 에서 Visual Tree 를 역추적하여:
    ///   · owner TreeViewItem 에 도달하기 전에 다른 TreeViewItem 발견
    ///     → true  (자식에서 발생, 무시해야 함)
    ///   · owner TreeViewItem 에 바로 도달 (중간에 다른 TreeViewItem 없음)
    ///     → false (이 노드 자신의 콘텐츠에서 발생, 처리해야 함)
    ///
    /// [Visual Tree 예시]
    ///   ParentTreeViewItem (owner)
    ///     ItemsPresenter
    ///       ChildTreeViewItem      ← 여기서 발견 → true 반환
    ///         ContentPresenter
    ///           StackPanel
    ///             TextBlock        ← e.OriginalSource (클릭된 요소)
    /// </summary>
    private static bool _IsEventFromChildTreeViewItem(
        TreeViewItem owner, DependencyObject? source)
    {
        if (source is null) return false;

        var current = source;
        while (current is not null)
        {
            // owner 에 도달 → 자식 TreeViewItem 없이 도달 → 자신의 콘텐츠
            if (ReferenceEquals(current, owner))
                return false;

            // owner 가 아닌 다른 TreeViewItem 발견 → 자식 노드에서 발생
            if (current is TreeViewItem)
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        // Visual Tree 범위를 벗어남 (비정상 케이스) → 안전하게 무시
        return true;
    }

    /// <summary>TreeViewItem 내부의 TextBox (EditBox) 를 찾아 포커스 설정.</summary>
    private static void FocusEditBox(TreeViewItem? item)
    {
        if (item is null) return;
        var textBox = _FindVisualChild<TextBox>(item);
        if (textBox is null) return;
        textBox.Focus();
        textBox.SelectAll();
    }

    /// <summary>Visual Tree 에서 첫 번째 T 타입 자식 탐색.</summary>
    private static T? _FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T target) return target;
            var found = _FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        return null;
    }
}