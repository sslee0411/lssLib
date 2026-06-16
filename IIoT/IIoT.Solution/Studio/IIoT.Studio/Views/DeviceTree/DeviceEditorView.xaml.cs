// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceEditorView.xaml.cs
//  역할: 장비 편집기 코드비하인드 (최소 구현)
//        DataContext = DeviceTreeNode (부모 View 에서 주입)
//  S-03: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace IIoT.Studio.Views.DeviceTree;

public partial class DeviceEditorView : UserControl
{
    public DeviceEditorView()
    {
        InitializeComponent();
    }
}
