// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  MN-Base-1: 생성자에서 MonitorMainViewModel 을 DI로 주입받아 DataContext 설정
//  MN-01: CollectorManageView 를 DI로 함께 주입받아 CollectorManageHost 에 대입
//  MN-02: LiveTagView 를 DI로 함께 주입받아 TagStatusHost 에 대입
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-02)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using IIoT.Monitor.Views.CollectorManage;
using IIoT.Monitor.Views.LiveTag;
using System.Windows;

namespace IIoT.Monitor;

public partial class MainWindow : Window
{
    public MainWindow(
        MonitorMainViewModel   viewModel,
        CollectorManageView    collectorManageView,
        LiveTagView             liveTagView)
    {
        InitializeComponent();
        DataContext = viewModel;

        // ★ DI 필요 View → ContentControl + 코드 주입 패턴
        CollectorManageHost.Content = collectorManageView;
        TagStatusHost.Content       = liveTagView;
    }
}
