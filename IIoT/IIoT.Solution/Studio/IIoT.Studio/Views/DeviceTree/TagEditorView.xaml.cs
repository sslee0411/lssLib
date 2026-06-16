// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/TagEditorView.xaml.cs
//  역할: Tag 편집기 코드비하인드 (최소 구현)
//        DataContext = TagTreeNode (부모 View 에서 주입)
//  S-05: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace IIoT.Studio.Views.DeviceTree;

public partial class TagEditorView : UserControl
{
    public TagEditorView()
    {
        InitializeComponent();
    }
}
