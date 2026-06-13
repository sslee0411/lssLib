// ══════════════════════════════════════════════════════════
//  IIoT.Manager · MainWindow.xaml.cs
//  역할: Manager 메인 윈도우 코드비하인드
//  Phase 12: 신규
// ══════════════════════════════════════════════════════════

using IIoT.Manager.ViewModels;
using System.Windows;

namespace IIoT.Manager;

public partial class MainWindow : Window
{
    // §1 ─ 생성자 ─────────────────────────────────────────────
    // ★ 규칙 6: 기본 생성자 제거 → ViewModel 주입 생성자 필수
    public MainWindow(ManagerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
