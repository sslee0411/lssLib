// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Views/Dashboard/DashboardView.xaml.cs
//  역할: [대시보드] 탭 코드비하인드 — DataContext 주입
//  MG-05: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.ViewModels;
using System.Windows.Controls;

namespace IIoT.Manager.Views.Dashboard;

public partial class DashboardView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public DashboardView(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
