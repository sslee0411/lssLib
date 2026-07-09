// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Views/Chart/ChartView.xaml.cs
//  역할: [차트] 탭 View 코드비하인드 — DI로 ChartViewModel 주입
//  MN-06: 신규
//  생성: 2026-07-08
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using System.Windows.Controls;

namespace IIoT.Monitor.Views.Chart;

public partial class ChartView : UserControl
{
    public ChartView(ChartViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
