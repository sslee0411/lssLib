// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Views/LiveTag/LiveTagView.xaml.cs
//  역할: [태그현황] 탭 View 코드비하인드
//        DI로 LiveTagViewModel 을 주입받아 DataContext 설정,
//        CollectionViewSource.Source/GroupDescriptions 를 코드에서 구성
//        (Resources 는 DataContext 상속이 안 되므로 XAML Binding으로는 불가)
//  MN-02: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace IIoT.Monitor.Views.LiveTag;

public partial class LiveTagView : UserControl
{
    public LiveTagView(LiveTagViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        var cvs = (CollectionViewSource)Resources["GroupedRows"];
        cvs.Source = viewModel.Aggregator.Rows;
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Models.LiveTagRow.CollectorName)));
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Models.LiveTagRow.PlcId)));
        cvs.SortDescriptions.Add(new SortDescription(nameof(Models.LiveTagRow.CollectorName), ListSortDirection.Ascending));
        cvs.SortDescriptions.Add(new SortDescription(nameof(Models.LiveTagRow.PlcId), ListSortDirection.Ascending));
        cvs.SortDescriptions.Add(new SortDescription(nameof(Models.LiveTagRow.TagId), ListSortDirection.Ascending));
    }
}
