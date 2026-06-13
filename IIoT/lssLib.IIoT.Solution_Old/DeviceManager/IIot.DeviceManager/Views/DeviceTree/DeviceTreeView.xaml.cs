// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceTreeView.xaml.cs
//  역할: 장비 트리뷰 코드 비하인드
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — IsSelected OneWayToSource 다중 선택 버그 수정
//  수정: 2025-05-23 v3 — IsEditing CommitEdit 강제 호출
//  수정: 2025-05-26 v4 — IsEditing 잔존: e.OldValue + vm.SelectedNode 이중 확인
//  수정: 2025-05-26 v5 — 더블클릭 상위노드 에디트 박스 활성화 버그 수정
//        Control.MouseDoubleClick = Direct 이벤트 → 자식 필터 추가
//  수정: 2025-05-26 v6 — 태그 버튼 비활성화 버그 수정
//        [원인]
//        Add* 커맨드로 vm.SelectedNode 가 새 자식 노드로 변경된 후,
//        사용자가 부모 노드를 재클릭해도 WPF TreeView 가
//        "이미 선택된 항목" 으로 판단하여 SelectedItemChanged 미발생.
//        vm.SelectedNode 가 자식 노드에 머물러 CanAddTag 등 비활성화.
//        [해결]
//        PreviewMouseLeftButtonDown(터널링 이벤트) 에서
//        vm.SelectedNode 를 클릭된 노드로 강제 동기화.
//        SelectedItemChanged 미발생 케이스를 선점 처리.
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

    // §2 ─ 선택 선점 동기화 (★ v6 핵심 추가) ─────────────────

    /// <summary>
    /// PreviewMouseLeftButtonDown → vm.SelectedNode 강제 동기화.
    ///
    /// ★ v6 신규: SelectedItemChanged 미발생 케이스 대응
    ///
    /// [발생 시나리오]
    ///   1. 사용자가 "새 장비" 클릭 → TreeView.SelectedItem = 새장비
    ///   2. 툴바 "태그 추가" → AddTag() → vm.SelectedNode = 새태그
    ///      (TreeView.SelectedItem 은 여전히 새장비)
    ///   3. 사용자가 "새 장비" 재클릭
    ///      → WPF: 이미 선택된 항목 → SelectedItemChanged 미발생
    ///      → vm.SelectedNode 여전히 새태그 (TagNodeViewModel)
    ///      → CanAddTag() → false → 태그 버튼 비활성!
    ///
    /// [해결]
    ///   PreviewMouseLeftButtonDown 은 WPF 가 선택 처리를 시작하기 전에
    ///   터널링으로 발생하므로, SelectedItemChanged 발생 여부와 무관하게
    ///   vm.SelectedNode 를 클릭된 노드로 미리 동기화.
    ///   이후 SelectedItemChanged 가 발생하면 동일 값 재설정 → SetProperty 멱등, 무해.
    ///
    /// [자식 TreeViewItem 필터]
    ///   PreviewMouseLeftButtonDown 은 터널링이므로 부모가 먼저 수신.
    ///   _IsEventFromChildTreeViewItem() 으로 자식 내부에서 발생한 클릭은 무시.
    ///   → 부모가 자식 클릭을 가로채 vm.SelectedNode 를 잘못 변경하는 것을 방지.
    /// </summary>
    private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem tvi) return;
        if (tvi.DataContext is not DeviceNodeViewModel clickedNode) return;
        if (DataContext is not DeviceTreeViewModel vm) return;

        // 자식 TreeViewItem 내부 클릭이면 부모 handler 에서 처리 안 함
        if (_IsEventFromChildTreeViewItem(tvi, e.OriginalSource as DependencyObject))
            return;

        // vm.SelectedNode 가 다를 때만 동기화 (멱등 보장)
        if (!ReferenceEquals(vm.SelectedNode, clickedNode))
        {
            // OnSelectedNodeChanged(old, clickedNode) 자동 발생:
            //   old.IsEditing = true 이면 CommitEdit 처리
            // CanExecuteChanged 자동 발생:
            //   AddTag/AddDevice 등 CanExecute 즉시 재평가 → 버튼 활성화 복원
            vm.SelectedNode = clickedNode;
        }
    }

    // §3 ─ 선택 변경 (SelectedItemChanged) ───────────────────

    /// <summary>
    /// 트리 선택 변경 → ViewModel.SelectedNode 동기화.
    ///
    /// [경로 A] e.OldValue: TreeView 추적 이전 선택 (일반 클릭 전환)
    /// [경로 B] vm.SelectedNode: Add* 후 첫 TreeView 클릭
    ///   e.OldValue = null 이지만 vm.SelectedNode 에 편집 중 노드 있을 수 있음
    ///
    /// v6: PreviewMouseLeftButtonDown 이 이미 vm.SelectedNode 를 동기화했으므로
    ///     SetProperty 멱등으로 중복 처리 무해.
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

    // §4 ─ 더블클릭 편집 ──────────────────────────────────────

    /// <summary>
    /// 더블클릭 → 인라인 이름 편집 시작.
    ///
    /// ★ v5: Control.MouseDoubleClick 은 Direct 이벤트.
    ///   MouseLeftButtonDown(Bubbling) 이 상위 TreeViewItem 까지 전파되면서
    ///   각 TreeViewItem 마다 MouseDoubleClick 이 독립 발생.
    ///   → _IsEventFromChildTreeViewItem() 으로 자식 발생 이벤트 필터링.
    /// </summary>
    private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem tvi) return;
        if (tvi.DataContext is not DeviceNodeViewModel node) return;

        // 자식 TreeViewItem 에서 발생한 이벤트 무시
        if (_IsEventFromChildTreeViewItem(tvi, e.OriginalSource as DependencyObject))
            return;

        if (node.IsEditing) return;

        node.BeginEditCommand.Execute(null);
        e.Handled = true;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => _FocusEditBox(tvi));
    }

    // §5 ─ 키 입력 처리 ───────────────────────────────────────

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

    // §6 ─ 내부 헬퍼 ──────────────────────────────────────────

    /// <summary>
    /// 이벤트가 자식 TreeViewItem 내부에서 발생했는지 판별.
    ///
    /// source 에서 Visual Tree 역추적:
    ///   · owner 도달 전에 다른 TreeViewItem 발견 → true (자식에서 발생)
    ///   · owner 에 바로 도달 → false (자신 콘텐츠에서 발생)
    ///
    /// [활용]
    ///   MouseDoubleClick (Direct, v5): 부모 handler 에서 자식 이벤트 차단
    ///   PreviewMouseLeftButtonDown (Tunneling, v6): 부모 handler 에서 자식 이벤트 차단
    /// </summary>
    private static bool _IsEventFromChildTreeViewItem(
        TreeViewItem owner, DependencyObject? source)
    {
        if (source is null) return false;

        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, owner)) return false;  // 자신 콘텐츠
            if (current is TreeViewItem) return true;           // 자식 TreeViewItem 발견
            current = VisualTreeHelper.GetParent(current);
        }
        return true; // Visual Tree 이탈 → 안전하게 무시
    }

    /// <summary>TreeViewItem 내부 TextBox (EditBox) 찾아 포커스 설정.</summary>
    private static void _FocusEditBox(TreeViewItem? item)
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