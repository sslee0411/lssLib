// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Views/Dashboard/DashboardView.xaml.cs
//  역할: [대시보드] 탭 View 코드비하인드 — DI로 DashboardViewModel 주입
//  MN-02B: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using System.Windows.Controls;

namespace IIoT.Monitor.Views.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
