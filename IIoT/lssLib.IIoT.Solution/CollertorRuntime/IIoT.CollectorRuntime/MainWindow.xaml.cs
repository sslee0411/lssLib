// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · MainWindow.xaml.cs
//  수정: Phase 8 — MainViewModel 수신 생성자 + 테마 단축키
// ══════════════════════════════════════════════════════════

using IIoT.CollectorRuntime.ViewModels;
using IIoT.UI.Themes;
using System.Windows;
using System.Windows.Input;

namespace IIoT.CollectorRuntime;

public partial class MainWindow : Window
{
    // ★ 수정: 기본 생성자 제거 → MainViewModel 주입 생성자로 교체
    //   기존: public MainWindow() { InitializeComponent(); }
    //   이유: App.xaml.cs 에서 new MainWindow(_vm).Show() 로 생성하므로
    //         DataContext 를 생성자에서 설정해야 합니다.
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // 테마 변경 이벤트 구독 (DynamicResource 로 자동 갱신되므로 실제 처리 불필요)
        ThemeManager.ThemeChanged += _ => { };
        Closed += (_, _) => ThemeManager.ThemeChanged -= _ => { };
    }

    // Ctrl+T / Ctrl+Shift+T 로 테마 순환
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            int dir = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : +1;
            var all = ThemeManager.AllThemes;
            int idx = all.Select((t, i) => (t, i))
                          .FirstOrDefault(x => x.t.Kind == ThemeManager.Current).i;
            int next = ((idx + dir) % all.Count + all.Count) % all.Count;
            ThemeManager.Apply(all[next].Kind);
            e.Handled = true;
        }
    }
}