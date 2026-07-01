// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Alarm/AlarmView.xaml.cs
//  역할: 알람 탭 코드비하인드
//  Col-Base-1: 최소 구현 (레이아웃 뼈대)
//  C-06: AlarmViewModel DI 주입 + DataContext 연결
//  생성: 2026-06-29 / 수정: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.ViewModels;
using System.Windows.Controls;

namespace IIoT.Collector.Views.Alarm;

public partial class AlarmView : UserControl
{
    public AlarmView(AlarmViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
