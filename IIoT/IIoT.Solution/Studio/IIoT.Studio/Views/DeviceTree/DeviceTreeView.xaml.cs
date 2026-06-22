// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceTreeView.xaml.cs
//  역할: 장비 트리 뷰 코드비하인드
//  S-01 rev3: B안 — 드롭다운 팝업 열기/닫기 핸들러
//  S-14 fix4: DataContextChanged → TemplateManagerView DataContext 주입
//  S-17B: 이름 인라인 편집 핸들러 추가
//         - OnNodeLabelMouseDown: 더블클릭 감지 → BeginEdit()
//         - OnRenameKeyDown: Enter → CommitEdit() / Esc → CancelEdit()
//         - OnRenameLostFocus: TextBox 포커스 잃으면 CommitEdit()
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IIoT.Studio.Views.DeviceTree;

public partial class DeviceTreeView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public DeviceTreeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // §2 ─ DataContext 변경 → TemplateManagerView DataContext 주입 ──

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DeviceTreeViewModel vm)
            TemplateManagerView.DataContext = vm.TagTemplateVm;
    }

    // §3 ─ TreeView 선택 이벤트 ───────────────────────────────

    private void TreeView_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        // 편집 중인 노드가 있으면 먼저 확정
        if (e.OldValue is AbstractTreeNode old && old.IsEditing)
            old.CommitEdit();

        if (DataContext is DeviceTreeViewModel vm)
            vm.SelectNode(e.NewValue);
    }

    // §4 ─ 드롭다운 팝업 열기 ────────────────────────────────

    private void GroupMenuBtn_Click(object sender, RoutedEventArgs e)
        => GroupPopup.IsOpen = true;

    private void DeviceMenuBtn_Click(object sender, RoutedEventArgs e)
        => DevicePopup.IsOpen = true;

    private void PlcMenuBtn_Click(object sender, RoutedEventArgs e)
        => PlcPopup.IsOpen = true;

    private void PopupItem_Click(object sender, RoutedEventArgs e)
    {
        GroupPopup.IsOpen  = false;
        DevicePopup.IsOpen = false;
        PlcPopup.IsOpen    = false;
    }

    // §5 ─ ★ S-17B: 인라인 편집 핸들러 ─────────────────────

    /// <summary>
    /// TextBlock 더블클릭 → 인라인 편집 시작.
    /// ClickCount 2 = 더블클릭.
    /// </summary>
    private void OnNodeLabelMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is FrameworkElement fe
            && fe.DataContext is AbstractTreeNode node)
        {
            node.BeginEdit();

            // ★ BeginEdit() 후 XAML DataTrigger가 TextBox를 Visible로 전환할 때까지
            //   Dispatcher.BeginInvoke로 렌더링 완료 후 포커스 이동
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                new Action(() =>
                {
                    // sender(TextBlock)의 부모 StackPanel → TreeViewItem 탐색
                    var tb = FindVisualChild<TextBox>(
                        FindVisualParent<TreeViewItem>(fe) ?? fe);
                    if (tb is not null)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }));

            e.Handled = true;
        }
    }

    /// <summary>
    /// TextBox 키 입력:
    ///   Enter → 편집 확정 (CommitEdit)
    ///   Esc   → 편집 취소 (CancelEdit)
    /// </summary>
    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AbstractTreeNode node)
        {
            if (e.Key == Key.Enter)
            {
                node.CommitEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                node.CancelEdit();
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// TextBox LostFocus → 편집 확정.
    /// 다른 곳 클릭 시 자동 저장.
    /// </summary>
    private void OnRenameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AbstractTreeNode node
            && node.IsEditing)
        {
            node.CommitEdit();
        }
    }

    // §6 ─ 비주얼 트리 헬퍼 ──────────────────────────────────

    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T t) return t;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        return null;
    }
}
