// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/PlcEditorView.xaml.cs
//  역할: PLC 편집기 코드비하인드
//  S-14: OnApplyTemplate 핸들러
//  S-21B: OnBulkAddress 핸들러
//  S-28: DataContextChanged → CommEntryCombo ItemsSource 주입
//         OnCommEntryChanged → CommEntry 선택 시 PLC 필드 자동 채움
//         OnCommEntryDetach  → 참조 해제
//  생성: 2026-06-18 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using IIoT.Studio;   // MainViewModel
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IIoT.Studio.Views.DeviceTree;

public partial class PlcEditorView : UserControl
{
    public PlcEditorView()
    {
        InitializeComponent();
        // ★ S-28: DataContext 변경 시 CommEntryCombo ItemsSource 주입
        DataContextChanged += OnDataContextChanged;
    }

    // §0 ─ ★ S-28: DataContext 변경 → CommLibrary 주입 ────────

    private void OnDataContextChanged(object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not PlcTreeNode) return;

        // ★ CommLibrary는 MainViewModel에 있음 (DeviceTreeViewModel에 없음)
        var mainVm = _FindAncestorVm<MainViewModel>(this);
        if (mainVm is null) return;

        CommEntryCombo.ItemsSource = mainVm.CommLibrary.Entries;
    }

    // §0-1 ─ ★ S-28: 통신 라이브러리 항목 선택 → PLC 필드 자동 채움 ──

    private void OnCommEntryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;
        if (CommEntryCombo.SelectedItem is not CommEntry entry) return;

        // 라이브러리 항목 값을 PLC 필드에 자동 복사 (표시용)
        plc.Host   = entry.Host;
        plc.Port   = entry.Port;
        plc.PollMs = entry.PollMs;
        // CommEntryId는 ComboBox의 SelectedValue 바인딩으로 자동 설정됨
    }

    // §0-2 ─ ★ S-28: 참조 해제 → 직접 입력 모드로 전환 ──────

    private void OnCommEntryDetach(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;
        plc.CommEntryId = null;
        CommEntryCombo.SelectedItem = null;
    }

    // §1 ─ 템플릿 적용 핸들러 (S-14) ─────────────────────────

    private void OnApplyTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;

        var treeVm = _FindAncestorVm<DeviceTreeViewModel>(this);
        if (treeVm?.TagTemplateVm is null) return;

        if (!treeVm.TagTemplateVm.Templates.Any())
        {
            MessageBox.Show(
                "저장된 템플릿이 없습니다.\n툴바 [📋 템플릿 관리] 버튼으로 먼저 작성하세요.",
                "템플릿 없음",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dlg = new ApplyTemplateDialog(
            treeVm.TagTemplateVm.Templates,
            Window.GetWindow(this));

        if (dlg.ShowDialog() != true) return;
        if (dlg.ResultTemplate is null) return;

        foreach (var item in dlg.ResultTemplate.Items)
        {
            var tag = new TagTreeNode(item.Name)
            {
                Address  = item.CalcAddress(dlg.ResultStartAddress).ToString(),
                DataType = item.BufType,
                Unit     = item.Unit
            };
            plc.Children.Add(tag);
        }
    }

    // §2 ─ ★ S-21B: 일괄 주소 부여 핸들러 ───────────────────

    private void OnBulkAddress(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;

        // Tag가 없으면 안내
        var tags = plc.Children.OfType<TagTreeNode>().ToList();
        if (tags.Count == 0)
        {
            MessageBox.Show(
                "이 PLC 하위에 Tag가 없습니다.\n먼저 Tag를 추가하세요.",
                "Tag 없음",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // 다이얼로그 열기
        var dlg = new BulkAddressDialog(plc, Window.GetWindow(this));

        if (dlg.ShowDialog() != true) return;
        if (dlg.Result.Count == 0) return;

        // 결과 적용 — Tag.Address 일괄 갱신
        foreach (var (tag, address) in dlg.Result)
            tag.Address = address;
    }

    // §3 ─ 비주얼 트리 ViewModel 역추적 헬퍼 ─────────────────

    private static T? _FindAncestorVm<T>(DependencyObject child)
        where T : class
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is FrameworkElement fe && fe.DataContext is T vm)
                return vm;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
