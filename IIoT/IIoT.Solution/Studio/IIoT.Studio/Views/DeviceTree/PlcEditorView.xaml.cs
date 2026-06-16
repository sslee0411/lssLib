// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/PlcEditorView.xaml.cs
//  역할: PLC 편집기 코드비하인드 (최소 구현)
//        DataContext = PlcTreeNode (부모 View 에서 주입)
//  S-04: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace IIoT.Studio.Views.DeviceTree;

public partial class PlcEditorView : UserControl
{
    public PlcEditorView()
    {
        InitializeComponent();
    }
}
