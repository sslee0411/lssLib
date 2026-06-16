// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/GroupEditorView.xaml.cs
//  역할: 그룹 편집기 코드비하인드 (최소 구현)
//        DataContext = GroupTreeNode (부모 View 에서 주입)
//  S-02: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace IIoT.Studio.Views.DeviceTree;

public partial class GroupEditorView : UserControl
{
    public GroupEditorView()
    {
        InitializeComponent();
    }
}
