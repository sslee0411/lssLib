// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Flow/FlowView.xaml.cs
//  역할: 수집 흐름 탭 코드비하인드
//  C-09: FlowViewModel DI 주입 + DataContext 연결
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.ViewModels;
using System.Windows.Controls;

namespace IIoT.Collector.Views.Flow;

public partial class FlowView : UserControl
{
    public FlowView(FlowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
