// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Views/Alarm/AlarmView.xaml.cs
//  역할: [알람] 탭 View 코드비하인드
//        DI로 AlarmViewModel 을 주입받아 DataContext 설정,
//        CollectionViewSource.Source/GroupDescriptions 를 코드에서 구성
//  MN-03: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using System.Windows.Controls;
using System.Windows.Data;

namespace IIoT.Monitor.Views.Alarm;

public partial class AlarmView : UserControl
{
    public AlarmView(AlarmViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        var cvs = (CollectionViewSource)Resources["GroupedAlarms"];
        cvs.Source = viewModel.Aggregator.Rows;
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Models.AlarmRow.CollectorName)));
    }
}
