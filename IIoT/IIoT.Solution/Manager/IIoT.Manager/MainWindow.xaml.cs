// ══════════════════════════════════════════════════════════
//  IIoT.Manager · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  MG-Base-0: 생성자만
//  MG-01: DI 생성자 주입 — ManagerMainViewModel(DataContext) +
//         ProcessStatusView(ContentControl 호스트 주입)
//  MG-02: Loaded 에서 ManagerMainViewModel.InitializeAsync() 호출
//         (manager.json 로드 — 파일 I/O 는 창 표시 후, Monitor 패턴)
//  MG-04: LogViewerView 주입 추가 (LogViewerHost)
//  MG-05: DashboardView 주입 추가 (DashboardHost)
//  MG-06: DeployView 주입 추가 (DeployHost)
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-06)
// ══════════════════════════════════════════════════════════

// ★ 이동(2026-07-09): ManagerMainViewModel 이 루트 namespace(IIoT.Manager)로
//   이동해 ViewModels using 불필요 (Studio·Collector 컨벤션 정렬)
using IIoT.Manager.Views.Dashboard;
using IIoT.Manager.Views.Deploy;
using IIoT.Manager.Views.LogViewer;
using IIoT.Manager.Views.ProcessStatus;
using System.Windows;

namespace IIoT.Manager;

public partial class MainWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly ManagerMainViewModel _vm;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public MainWindow(ManagerMainViewModel vm,
                      ProcessStatusView    processStatusView,
                      LogViewerView        logViewerView,
                      DashboardView        dashboardView,
                      DeployView           deployView)
    {
        InitializeComponent();

        // ★ DataContext — ProcessStatusView 는 상속으로 같은 VM 을 사용
        //   (LogViewerView/DashboardView 는 자체 VM 을 생성자에서 주입받음)
        _vm         = vm;
        DataContext = vm;

        // ★ DI 필요 View → ContentControl + 코드 주입 패턴
        ProcessStatusHost.Content = processStatusView;

        // ★ MG-04: 로그 뷰어 주입
        LogViewerHost.Content = logViewerView;

        // ★ MG-05: 대시보드 주입
        DashboardHost.Content = dashboardView;

        // ★ MG-06: 배포 주입
        DeployHost.Content = deployView;

        // ★ MG-02: manager.json 로드 (창 표시 후 — 파일 I/O 블로킹 방지)
        Loaded += _OnLoaded;
    }

    // §3 ─ 내부 메서드 ────────────────────────────────────────

    private async void _OnLoaded(object sender, RoutedEventArgs e)
    {
        // ★ 규칙: async void 이벤트 핸들러는 반드시 try/catch
        //   (InitializeAsync 내부에서도 처리하지만 이중 방어)
        try
        {
            await _vm.InitializeAsync();
        }
        catch (Exception ex)
        {
            lssLib.Log.LogManager.Instance.Error("MainWindow", $"초기화 실패: {ex.Message}");
        }
    }
}
