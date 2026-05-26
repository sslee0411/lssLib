// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceTreeView.xaml.cs
//  역할: 장비 트리뷰 코드 비하인드 — 인라인 편집 입력 처리
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — 다중 선택 버그 수정
//        IsSelected 바인딩 OneWayToSource, SelectedItemChanged 단독 관리
//        VM→TreeView 방향 동기화는 SelectedItemChanged만 담당.
//        이전 선택 노드의 IsSelected를 명시적으로 해제하여
//        부모 노드가 함께 선택되는 버그 제거.
//  수정: 2025-05-23 v3 — IsEditing 유지 버그 수정
//        SelectedItemChanged 에서 이전 노드 CommitEdit 강제 호출
//        노드 변경 시 편집 텍스트박스 자동 닫힘 보장
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.ViewModels.DeviceTree;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IIoT.DeviceManager.Views.DeviceTree;

/// <summary>
/// DeviceTreeView 코드 비하인드.
/// ViewModel 로직은 DeviceTreeViewModel 에 있고,
/// 순수 UI 입력 이벤트만 여기서 처리한다.
/// </summary>
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
    /// ★ v2 수정: 이전 선택 노드의 IsSelected를 명시적으로 false 처리.
    ///   IsSelected 바인딩이 OneWayToSource이므로 VM에서 TreeView로
    ///   역방향 주입을 하지 않는다.
    ///   대신 이 이벤트 핸들러에서만 IsSelected를 관리.
    ///
    ///   부모 노드가 함께 선택되는 버그 원인:
    ///   TwoWay 바인딩 + VM에서 IsSelected = true 설정 →
    ///   WPF 내부에서 부모 TreeViewItem까지 IsSelected 전파.
    /// ★ v3: 이전 노드가 편집 중이면 CommitEdit 강제 호출
    ///        → 노드를 변경해도 텍스트박스가 열린 채로 유지되는 버그 수정
    /// </summary>
    private void DeviceTree_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not DeviceTreeViewModel vm) return;

        // ① 이전 노드: 편집 중이면 확정 → IsSelected 해제
        if (e.OldValue is DeviceNodeViewModel oldNode)
        {
            if (oldNode.IsEditing)
                oldNode.CommitEditCommand.Execute(null);   // ★ 편집 강제 확정
            oldNode.IsSelected = false;
        }

        // ② 새 노드: IsSelected 설정
        var newNode = e.NewValue as DeviceNodeViewModel;
        if (newNode is not null)
            newNode.IsSelected = true;

        vm.SelectedNode = newNode;
    }

    // §3 ─ 더블클릭 편집 ──────────────────────────────────────

    /// <summary>
    /// 더블클릭 → 인라인 이름 편집 시작.
    /// </summary>
    private void TreeViewItem_MouseDoubleClick(object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem { DataContext: DeviceNodeViewModel node }) return;

        // 이미 편집 중이면 중복 진입 방지
        if (node.IsEditing) return;

        node.BeginEditCommand.Execute(null);
        e.Handled = true; // 부모 트리 더블클릭 버블링 차단

        // 포커스 → EditBox (TextBox) 로 이동
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => FocusEditBox(sender as TreeViewItem));
    }

    // §4 ─ 키 입력 처리 ───────────────────────────────────────

    /// <summary>
    /// Enter: 편집 확정, Escape: 편집 취소.
    /// </summary>
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
    /// TreeViewItem 내부의 EditBox(TextBox) 를 찾아 포커스 설정.
    /// </summary>
    private static void FocusEditBox(TreeViewItem? item)
    {
        if (item is null) return;
        var textBox = FindVisualChild<TextBox>(item);
        if (textBox is null) return;
        textBox.Focus();
        textBox.SelectAll();
    }

    /// <summary>
    /// VisualTree 에서 첫 번째 T 타입 자식 탐색.
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T target) return target;
            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        return null;
    }
}