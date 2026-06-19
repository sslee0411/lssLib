// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceTreeView.xaml.cs
//  역할: 장비 트리 뷰 코드비하인드
//  S-01 rev3: B안 — 드롭다운 팝업 열기/닫기 핸들러 추가
//  S-14 fix4:
//    [이슈1] ＋Tag: Popup 제거, AddTagCommand 직접 바인딩 (누를 때마다 추가)
//    [이슈2] DataContextChanged → TemplateManagerView.DataContext만 분리 주입
//            Visibility는 XAML에서 ElementName=RootPanel 로 고정되어
//            DataContext 교체의 영향을 받지 않음
//  생성: 2026-06-15 / 수정: 2026-06-19
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

        // ★ 이슈2 fix: DataContext가 DeviceTreeViewModel로 설정될 때
        //   TemplateManagerView의 DataContext만 TagTemplateVm으로 교체
        //   Visibility 바인딩은 XAML에서 ElementName=RootPanel로 고정되어
        //   이 DataContext 교체의 영향을 받지 않음
        DataContextChanged += OnDataContextChanged;
    }

    // §2 ─ DataContext 변경 → TemplateManagerView DataContext 주입 ──

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DeviceTreeViewModel vm)
            TemplateManagerView.DataContext = vm.TagTemplateVm;
    }

    // §3 ─ TreeView 선택 이벤트 ───────────────────────────────

    /// <summary>
    /// TreeView 선택 변경 → ViewModel.SelectNode() 호출.
    /// SelectNode() 내부에서 IsTemplateMode = false 처리됨.
    /// ★ WPF TreeView.SelectedItem TwoWay 바인딩 불가 → 코드비하인드 필수
    /// </summary>
    private void TreeView_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
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

    // §5 ─ 팝업 항목 클릭 → 모든 팝업 닫기 ──────────────────

    private void PopupItem_Click(object sender, RoutedEventArgs e)
    {
        GroupPopup.IsOpen  = false;
        DevicePopup.IsOpen = false;
        PlcPopup.IsOpen    = false;
    }
}
