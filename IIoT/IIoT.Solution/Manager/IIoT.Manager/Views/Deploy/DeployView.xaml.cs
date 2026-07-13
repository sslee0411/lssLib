// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Views/Deploy/DeployView.xaml.cs
//  역할: [배포] 탭 코드비하인드 — DataContext 주입
//  MG-06: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.ViewModels;
using System.Windows.Controls;

namespace IIoT.Manager.Views.Deploy;

public partial class DeployView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public DeployView(DeployViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
