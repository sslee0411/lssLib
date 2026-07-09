// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  MN-Base-1: MonitorMainViewModel DI 주입
//  MN-01/MN-02: CollectorManageView/LiveTagView DI 주입
//  MN-02B: DashboardView DI 주입 추가
//  MN-03: AlarmView DI 주입 추가
//  MN-05: MonitorHostService DI 주입 + Loaded 시 자체 웹 Hub 시작
//  MN-06: ChartView DI 주입 추가
//  MN-EX-02: AlarmHistoryService DI 주입 + Loaded 시 SQLite 이력 DB 초기화
//  신규(2026-07-08): LogPanelView DI 주입 — [로그] 탭 구현
//  MN-EX-03: TrayNotificationService 주입 — 최소화 시 트레이로 숨김,
//            트레이 더블클릭/메뉴로 복원, 트레이 메뉴로 종료
//  변경(2, 2026-07-08): [로그]가 콘텐츠 탭에서 하단 고정 패널 토글로 변경됨
//            (MainWindow.xaml 구조만 바뀜 — LogHost 코드는 그대로 유효)
//  생성: 2026-07-07 / 수정: 2026-07-08 (로그 하단 패널화)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Notification;
using IIoT.Monitor.Core.Storage;
using IIoT.Monitor.SignalR;
using IIoT.Monitor.ViewModels;
using IIoT.Monitor.Views.Alarm;
using IIoT.Monitor.Views.Chart;
using IIoT.Monitor.Views.CollectorManage;
using IIoT.Monitor.Views.Dashboard;
using IIoT.Monitor.Views.LiveTag;
using IIoT.Monitor.Views.Log;
using System.Windows;

namespace IIoT.Monitor;

public partial class MainWindow : Window
{
    public MainWindow(
        MonitorMainViewModel viewModel,
        CollectorManageView  collectorManageView,
        LiveTagView          liveTagView,
        AlarmView            alarmView,
        DashboardView        dashboardView,
        ChartView             chartView,
        LogPanelView          logPanelView,
        MonitorHostService    monitorHostService,
        AlarmHistoryService   alarmHistoryService,
        TrayNotificationService trayService)
    {
        InitializeComponent();
        DataContext = viewModel;

        // ★ DI 필요 View → ContentControl + 코드 주입 패턴
        CollectorManageHost.Content = collectorManageView;
        TagStatusHost.Content       = liveTagView;
        AlarmHost.Content           = alarmView;
        DashboardHost.Content       = dashboardView;
        ChartHost.Content           = chartView;
        LogHost.Content             = logPanelView;

        // ★ MN-05: 창이 뜬 뒤 Monitor 자체 웹 Hub 시작
        // ★ MN-EX-02: 창이 뜬 뒤 알람 이력 SQLite DB 초기화
        Loaded += async (_, _) =>
        {
            await alarmHistoryService.InitializeAsync();
            await monitorHostService.StartAsync();
        };

        // ★ MN-EX-03: 최소화 시 작업표시줄에서 숨기고 트레이로만 상주
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
        };

        // ★ MN-EX-03: 트레이 더블클릭/메뉴 "열기" → 창 복원
        trayService.RestoreRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
                Activate();
            });
        };

        // ★ MN-EX-03: 트레이 메뉴 "종료" → 앱 정상 종료 (창 X 버튼과 동일 경로)
        trayService.ExitRequested += () =>
            Dispatcher.Invoke(() => Application.Current.Shutdown());
    }
}
