// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceTreeView.xaml.cs
//  역할: 장비 트리뷰 코드 비하인드 — 인라인 편집 입력 처리
//  생성: 2025-05-22
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

    // §2 ─ 이벤트 핸들러 ──────────────────────────────────────

    /// <summary>
    /// 트리 선택 변경 → ViewModel.SelectedNode 동기화.
    /// </summary>
    private void DeviceTree_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not DeviceTreeViewModel vm) return;
        vm.SelectedNode = e.NewValue as DeviceNodeViewModel;
    }

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
        e.Handled = true;   // 부모 트리 더블클릭 버블링 차단

        // 포커스 → EditBox (TextBox) 로 이동
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => FocusEditBox(sender as TreeViewItem));
    }

    /// <summary>
    /// 키 입력 처리 — Enter: 편집 확정, Escape: 편집 취소.
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

    // §3 ─ 내부 헬퍼 ──────────────────────────────────────────

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
