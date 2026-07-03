// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Trend/TrendView.xaml.cs
//  C-13: 신규
//  생성: 2026-07-01
// ══════════════════════════════════════════════════════════

using IIoT.Collector.ViewModels;
using System.Windows.Controls;

namespace IIoT.Collector.Views.Trend;

public partial class TrendView : UserControl
{
    public TrendView(TrendViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
