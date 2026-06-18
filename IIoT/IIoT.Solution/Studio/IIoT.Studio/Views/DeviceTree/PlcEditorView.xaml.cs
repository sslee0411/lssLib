// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/PlcEditorView.xaml.cs
//  역할: PLC 편집기 코드비하인드
//  S-14: OnApplyTemplate 핸들러 추가
//  생성: 2026-06-18
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IIoT.Studio.Views.DeviceTree;

public partial class PlcEditorView : UserControl
{
    public PlcEditorView() => InitializeComponent();

    // §1 ─ 템플릿 적용 핸들러 (★ S-14) ──────────────────────

    private void OnApplyTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;

        // DeviceTreeViewModel 역추적 (UserControl → 상위 트리 탐색)
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

        // 트리에 Tag 자동 추가
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

    // §2 ─ 비주얼 트리 ViewModel 역추적 헬퍼 ─────────────────

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
