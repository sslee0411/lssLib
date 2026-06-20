// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceEditorView.xaml.cs
//  역할: 장비 편집기 코드비하인드
//  S-03: 초기 구현
//  S-14 fix6: OnApplyTemplate 핸들러 추가 (PlcEditorView와 동일 패턴)
//  생성: 2026-06-15 / 수정: 2026-06-19
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IIoT.Studio.Views.DeviceTree;

public partial class DeviceEditorView : UserControl
{
    public DeviceEditorView() => InitializeComponent();

    // §1 ─ 템플릿 적용 핸들러 (★ S-14 fix6) ─────────────────

    private void OnApplyTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceTreeNode device) return;

        // DeviceTreeViewModel 역추적
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
            device.Children.Add(tag);
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
