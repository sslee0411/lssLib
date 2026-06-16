// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  Base-0: DI 생성자 + DataContext 주입만
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using System.Windows;

namespace IIoT.Studio;

public partial class MainWindow : Window
{
    // §1 ─ 생성자 ─────────────────────────────────────────────
    /// <summary>
    /// DI 컨테이너에서 ViewModel 주입받아 DataContext 설정.
    /// ★ 기본 생성자(매개변수 없음) 절대 사용 금지
    ///    — App.xaml.cs 의 AddSingleton 팩토리와 충돌
    /// </summary>
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}