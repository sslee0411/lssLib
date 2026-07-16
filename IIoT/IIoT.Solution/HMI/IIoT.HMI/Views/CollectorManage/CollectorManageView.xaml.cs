// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/CollectorManage/CollectorManageView.xaml.cs
//  역할: [Collector 관리] 탭 View 코드비하인드
//        DI로 CollectorManageViewModel 을 주입받아 DataContext 설정,
//        Loaded 시 hmi.json 로드(InitializeAsync) 수행
//        (IIoT.Monitor Views/CollectorManager/CollectorManageView.xaml.cs 이식)
//  HM-02: 신규
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.ViewModels;
using System.Windows.Controls;

namespace IIoT.HMI.Views.CollectorManage;

public partial class CollectorManageView : UserControl
{
    public CollectorManageView(CollectorManageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
