// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  MN-Base-1: MonitorMainViewModel DI 주입
//  MN-01/MN-02: CollectorManageView/LiveTagView DI 주입
//  MN-02B: DashboardView DI 주입 추가
//  MN-03: AlarmView DI 주입 추가
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-03)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using IIoT.Monitor.Views.Alarm;
using IIoT.Monitor.Views.CollectorManage;
using IIoT.Monitor.Views.Dashboard;
using IIoT.Monitor.Views.LiveTag;
using System.Windows;

namespace IIoT.Monitor;

public partial class MainWindow : Window
{
    public MainWindow(
        MonitorMainViewModel viewModel,
        CollectorManageView  collectorManageView,
        LiveTagView          liveTagView,
        AlarmView            alarmView,
        DashboardView        dashboardView)
    {
        InitializeComponent();
        DataContext = viewModel;

        // ★ DI 필요 View → ContentControl + 코드 주입 패턴
        CollectorManageHost.Content = collectorManageView;
        TagStatusHost.Content       = liveTagView;
        AlarmHost.Content           = alarmView;
        DashboardHost.Content       = dashboardView;
    }
}
