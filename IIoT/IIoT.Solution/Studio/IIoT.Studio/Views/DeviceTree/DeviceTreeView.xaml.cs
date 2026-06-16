// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceTreeView.xaml.cs
//  역할: 장비 트리 뷰 코드비하인드
//  S-01 rev3: B안 — 드롭다운 팝업 열기/닫기 핸들러 추가
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace IIoT.Studio.Views.DeviceTree;

public partial class DeviceTreeView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────
    public DeviceTreeView()
    {
        InitializeComponent();
    }

    // §2 ─ TreeView 선택 이벤트 ───────────────────────────────

    /// <summary>
    /// TreeView 선택 변경 → ViewModel.SelectNode() 호출.
    /// ★ WPF TreeView.SelectedItem 은 읽기 전용 DependencyProperty
    ///    TwoWay 바인딩 불가 → 코드비하인드 직접 호출 필수
    /// </summary>
    private void TreeView_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is DeviceTreeViewModel vm)
            vm.SelectNode(e.NewValue);
    }

    // §3 ─ 드롭다운 팝업 열기 ────────────────────────────────
    // ★ 각 버튼 Click → 해당 Popup.IsOpen = true
    //   Popup StaysOpen="False" → 외부 클릭 시 자동 닫힘

    private void GroupMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        GroupPopup.IsOpen = true;
    }

    private void DeviceMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        DevicePopup.IsOpen = true;
    }

    private void PlcMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        PlcPopup.IsOpen = true;
    }

    // §4 ─ 팝업 항목 클릭 → 팝업 닫기 ───────────────────────
    // ★ Command 실행은 XAML 바인딩이 처리하고
    //   코드비하인드는 팝업 닫기만 담당

    private void PopupItem_Click(object sender, RoutedEventArgs e)
    {
        // 열려 있는 모든 팝업 닫기
        GroupPopup.IsOpen = false;
        DevicePopup.IsOpen = false;
        PlcPopup.IsOpen = false;
    }
}