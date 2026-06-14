// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceTreeView.xaml.cs
//  역할: 장비 트리 뷰 코드비하인드
//  Fix CS1061:
//    TreeView_SelectedItemChanged 메서드 추가
//    (DeviceTreeView.xaml 85줄에서
//     SelectedItemChanged="TreeView_SelectedItemChanged" 참조)
// ══════════════════════════════════════════════════════════

using IIoT.Studio.ViewModels.DeviceTree;
using System.Windows;
using System.Windows.Controls;

namespace IIoT.Studio.Views.DeviceTree;

public partial class DeviceTreeView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────
    public DeviceTreeView()
    {
        InitializeComponent();
    }

    // §2 ─ 트리 선택 이벤트 ───────────────────────────────────
    /// <summary>
    /// TreeView.SelectedItem 은 TwoWay 바인딩 미지원
    /// → 코드비하인드에서 직접 ViewModel.SelectNode() 호출
    /// </summary>
    private void TreeView_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is DeviceTreeViewModel vm)
            vm.SelectNode(e.NewValue);
    }
}
