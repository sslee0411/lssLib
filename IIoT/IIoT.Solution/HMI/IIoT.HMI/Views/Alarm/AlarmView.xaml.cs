// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/Alarm/AlarmView.xaml.cs
//  역할: [알람] 탭 View 코드비하인드
//        DI로 AlarmViewModel 을 주입받아 DataContext 설정,
//        CollectionViewSource.Source/GroupDescriptions/Filter 를 코드에서 구성
//        (IIoT.Monitor Views/Alarm/AlarmView.xaml.cs 이식 — 동일 구성)
//  HM-14: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Models;
using IIoT.HMI.ViewModels;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace IIoT.HMI.Views.Alarm;

public partial class AlarmView : UserControl
{
    public AlarmView(AlarmViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        var cvs = (CollectionViewSource)Resources["GroupedAlarms"];
        cvs.Source = viewModel.Aggregator.Rows;
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AlarmRow.CollectorName)));

        // 필터 연결 — 실제 판정은 ViewModel.MatchesFilter() 에 위임
        cvs.Filter += (_, e) => e.Accepted = e.Item is AlarmRow row && viewModel.MatchesFilter(row);

        // 필터 조건(콤보/검색어) 변경 시 재적용
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AlarmViewModel.SelectedCollector)
                                or nameof(AlarmViewModel.SelectedLevel)
                                or nameof(AlarmViewModel.SelectedStatus)
                                or nameof(AlarmViewModel.SearchText))
            {
                cvs.View.Refresh();
            }
        };
    }
}
