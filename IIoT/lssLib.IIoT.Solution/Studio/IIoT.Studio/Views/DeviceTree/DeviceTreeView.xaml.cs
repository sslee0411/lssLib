// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceTreeView.xaml.cs
//  역할: 장비 트리 뷰 코드비하인드
//  규칙:
//    ★ TreeView.SelectedItem 은 WPF 에서 TwoWay 바인딩 미지원
//       → SelectedItemChanged 이벤트로 ViewModel.SelectNode() 직접 호출
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using IIoT.Studio.ViewModels;
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

    // §2 ─ 이벤트 핸들러 ──────────────────────────────────────

    /// <summary>
    /// TreeView 선택 변경 → ViewModel.SelectNode() 호출.
    /// ★ WPF TreeView.SelectedItem 은 읽기 전용 DependencyProperty
    ///    TwoWay 바인딩 불가 → 코드비하인드에서 직접 호출 필수
    /// </summary>
    private void TreeView_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is DeviceTreeViewModel vm)
            vm.SelectNode(e.NewValue);
    }
}
