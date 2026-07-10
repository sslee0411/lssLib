// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Views/ProcessStatus/ProcessStatusView.xaml.cs
//  역할: 프로세스 상태 화면 코드비하인드
//  MG-01: 신규 — 생성자만 (DataContext 는 MainWindow 로부터 상속:
//         ManagerMainViewModel.Processes 바인딩)
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace IIoT.Manager.Views.ProcessStatus;

public partial class ProcessStatusView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public ProcessStatusView()
    {
        InitializeComponent();
    }
}
