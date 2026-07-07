// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  MN-Base-0: 최소 구현 (DataContext 없음)
//  MN-Base-1: 생성자에서 MonitorMainViewModel 을 DI로 주입받아 DataContext 설정
//  MN-01: CollectorManageView 를 DI로 함께 주입받아
//         CollectorManageHost(ContentControl).Content 에 대입
//         (View 자체는 항상 로드되어 있고, Visibility 로만 표시/숨김 전환됨
//          → 탭 전환 시마다 View 재생성/재로드 없음)
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-01)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using IIoT.Monitor.Views.CollectorManage;
using System.Windows;

namespace IIoT.Monitor;

public partial class MainWindow : Window
{
    public MainWindow(MonitorMainViewModel viewModel, CollectorManageView collectorManageView)
    {
        InitializeComponent();
        DataContext = viewModel;

        // ★ MN-01: DI 필요 View → ContentControl + 코드 주입 패턴
        CollectorManageHost.Content = collectorManageView;
    }
}
