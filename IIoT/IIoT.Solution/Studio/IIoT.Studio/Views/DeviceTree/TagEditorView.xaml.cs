// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/TagEditorView.xaml.cs
//  역할: Tag 편집기 코드비하인드
//  S-21A B-1: 부모 PLC의 PlcVendor 기반으로
//             RegisterTypeCombo ItemsSource 동적 필터링
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IIoT.Studio.Views.DeviceTree;

public partial class TagEditorView : UserControl
{
    public TagEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // §1 ─ DataContext 변경 → 부모 PLC 탐색 → 레지스터 목록 갱신 ──

    private void OnDataContextChanged(object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is TagTreeNode tag)
            _RefreshRegisterTypes(tag);
    }

    private void _RefreshRegisterTypes(TagTreeNode tag)
    {
        // 비주얼 트리를 거슬러 올라가 부모 PLC의 PlcVendor 찾기
        var vendor = _FindParentPlcVendor();

        // 해당 제조사에서 지원하는 레지스터 종류만 표시
        var supportedTypes = RegisterTypeExtensions.ForVendor(vendor);

        RegisterTypeCombo.ItemsSource   = supportedTypes;
        RegisterTypeCombo.SelectedValue = tag.RegisterType;

        // 현재 RegisterType이 지원 목록에 없으면 첫 번째 항목으로 교체
        if (!supportedTypes.Contains(tag.RegisterType)
            && supportedTypes.Count > 0)
        {
            tag.RegisterType = supportedTypes[0];
            RegisterTypeCombo.SelectedValue = tag.RegisterType;
        }
    }

    // §2 ─ 비주얼 트리에서 부모 PLC 탐색 ──────────────────────

    private PlcVendor _FindParentPlcVendor()
    {
        // DeviceTreeViewModel.SelectedNode의 부모가 PlcTreeNode인지 확인
        var treeVm = _FindAncestor<DeviceTreeViewModel>(this);
        if (treeVm?.SelectedNode is null) return PlcVendor.Modbus;

        // 부모 컬렉션 탐색으로 PlcTreeNode 찾기
        var plc = _FindParentPlc(
            treeVm.RootNodes, treeVm.SelectedNode);

        return plc?.PlcVendor ?? PlcVendor.Modbus;
    }

    private static PlcTreeNode? _FindParentPlc(
        System.Collections.ObjectModel.ObservableCollection<AbstractTreeNode> nodes,
        AbstractTreeNode target)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Contains(target))
                return node as PlcTreeNode;

            var found = _FindParentPlc(node.Children as
                System.Collections.ObjectModel.ObservableCollection<AbstractTreeNode>
                ?? new(), target);
            if (found is not null) return found;
        }
        return null;
    }

    private static T? _FindAncestor<T>(DependencyObject child) where T : class
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
