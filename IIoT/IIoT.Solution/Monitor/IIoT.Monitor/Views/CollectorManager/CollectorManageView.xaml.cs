// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Views/CollectorManage/CollectorManageView.xaml.cs
//  역할: [Collector 관리] 탭 View 코드비하인드
//        DI로 CollectorManageViewModel 을 주입받아 DataContext 설정,
//        Loaded 시 monitor.json 로드(InitializeAsync) 수행
//  MN-01: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using System.Windows.Controls;

namespace IIoT.Monitor.Views.CollectorManage;

public partial class CollectorManageView : UserControl
{
    public CollectorManageView(CollectorManageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
