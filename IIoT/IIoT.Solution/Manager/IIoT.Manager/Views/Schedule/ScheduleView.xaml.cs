// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Views/Schedule/ScheduleView.xaml.cs
//  역할: [스케줄] 탭 코드비하인드 — DataContext 주입
//  MG-07: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.ViewModels;
using System.Windows.Controls;

namespace IIoT.Manager.Views.Schedule;

public partial class ScheduleView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public ScheduleView(ScheduleViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
