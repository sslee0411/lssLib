// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/TagEditorView.xaml.cs
//  역할: Tag 편집기 코드비하인드
//  S-21A B-1: RegisterTypeCombo 부모 PLC 기반 필터링
//  S-26 fix: ScaleCombo / AlarmCombo ItemsSource 코드비하인드 직접 주입
//    문제: TagEditorView.DataContext = TagTreeNode (모델)
//          RelativeSource AncestorType=UserControl 이 TagTreeNode를 반환
//          → ScaleLibrary.Entries 탐색 불가
//    해결: DataContextChanged 시 비주얼 트리 탐색 →
//          DeviceTreeViewModel 찾아서 ScaleCombo/AlarmCombo.ItemsSource 직접 설정
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

    // §1 ─ DataContext 변경 처리 ──────────────────────────────

    private void OnDataContextChanged(object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not TagTreeNode tag) return;

        // 비주얼 트리에서 DeviceTreeViewModel 탐색
        var treeVm = _FindAncestorVm<DeviceTreeViewModel>(this);
        if (treeVm is null) return;

        // ★ S-26 fix: ScaleCombo / AlarmCombo ItemsSource 직접 주입
        ScaleCombo.ItemsSource = treeVm.ScaleLibrary.Entries;
        AlarmCombo.ItemsSource = treeVm.AlarmLibrary.Entries;

        // ★ S-21A B-1: RegisterTypeCombo 부모 PLC 기반 필터링
        _RefreshRegisterTypes(tag, treeVm);
    }

    // §2 ─ RegisterType 필터링 ────────────────────────────────

    private void _RefreshRegisterTypes(TagTreeNode tag, DeviceTreeViewModel treeVm)
    {
        var vendor = _FindParentPlcVendor(treeVm);
        var supported = RegisterTypeExtensions.ForVendor(vendor);

        RegisterTypeCombo.ItemsSource   = supported;
        RegisterTypeCombo.SelectedValue = tag.RegisterType;

        if (!supported.Contains(tag.RegisterType) && supported.Count > 0)
        {
            tag.RegisterType = supported[0];
            RegisterTypeCombo.SelectedValue = tag.RegisterType;
        }
    }

    private static PlcVendor _FindParentPlcVendor(DeviceTreeViewModel treeVm)
    {
        if (treeVm.SelectedNode is null) return PlcVendor.Modbus;
        var plc = _FindParentPlc(treeVm.RootNodes, treeVm.SelectedNode);
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

    // §3 ─ 비주얼 트리 ViewModel 탐색 ────────────────────────

    private static T? _FindAncestorVm<T>(DependencyObject child) where T : class
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
