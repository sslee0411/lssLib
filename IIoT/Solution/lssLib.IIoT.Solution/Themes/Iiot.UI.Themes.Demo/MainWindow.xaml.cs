// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes.Demo · MainWindow.xaml.cs
//  역할: 테마 데모 메인 윈도우 코드비하인드
//        - 탭 전환 (5개 섹션)
//        - 테마 이전/다음 버튼 + 단축키 (Ctrl+T / Ctrl+Shift+T)
//        - 테마 변경 이벤트 → 상태바 갱신
//  생성: 2025-05-16
// ══════════════════════════════════════════════════════════
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IIoT.UI.Themes;
namespace IIoT.UI.Themes.Demo;

public partial class MainWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private int _currentTab = 1;

    // 탭 버튼 / 뷰 매핑 (탭 번호 → (버튼, ScrollViewer))
    private Dictionary<int, (Button Btn, UIElement View)> _tabs = null!;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        // 탭 매핑 초기화
        _tabs = new()
        {
            [1] = (Tab1Btn, Tab1),
            [2] = (Tab2Btn, Tab2),
            [3] = (Tab3Btn, Tab3),
            [4] = (Tab4Btn, Tab4),
            [5] = (Tab5Btn, Tab5),
        };

        // 테마 변경 이벤트 구독
        ThemeManager.ThemeChanged += OnThemeChanged;

        // 초기 UI 갱신
        UpdateThemeDisplay(ThemeManager.Current);
        UpdateLogoIcon(ThemeManager.Current);
        SwitchTab(1);
    }

    // §3 ─ 탭 전환 ────────────────────────────────────────────

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out var tab))
            SwitchTab(tab);
    }

    private void SwitchTab(int tab)
    {
        _currentTab = tab;

        foreach (var (num, (btn, view)) in _tabs)
        {
            var isActive = num == tab;

            // 뷰 표시/숨김
            view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

            // 활성 탭 버튼 강조 — 배경/테두리 동적 변경
            if (isActive)
            {
                btn.Background   = Application.Current.Resources["AccFaintBrush"] as System.Windows.Media.Brush;
                btn.BorderBrush  = Application.Current.Resources["AccBrush"] as System.Windows.Media.Brush;
                btn.Foreground   = Application.Current.Resources["AccBrush"] as System.Windows.Media.Brush;
            }
            else
            {
                btn.Background   = Application.Current.Resources["CardBrush"] as System.Windows.Media.Brush;
                btn.BorderBrush  = Application.Current.Resources["Border2Brush"] as System.Windows.Media.Brush;
                btn.Foreground   = Application.Current.Resources["Text2Brush"] as System.Windows.Media.Brush;
            }
        }
    }

    // §4 ─ 테마 전환 버튼 ─────────────────────────────────────

    private void OnNextTheme(object sender, RoutedEventArgs e)
    {
        var next = (ThemeKind)(((int)ThemeManager.Current + 1) % ThemeManager.AllThemes.Count);
        ThemeManager.Apply(next);
    }

    private void OnPrevTheme(object sender, RoutedEventArgs e)
    {
        var count = ThemeManager.AllThemes.Count;
        var prev  = (ThemeKind)(((int)ThemeManager.Current - 1 + count) % count);
        ThemeManager.Apply(prev);
    }

    // §5 ─ 단축키 ─────────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Ctrl+T → 다음 테마
        if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnNextTheme(this, new RoutedEventArgs());
            e.Handled = true;
        }
        // Ctrl+Shift+T → 이전 테마
        else if (e.Key == Key.T &&
                 Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            OnPrevTheme(this, new RoutedEventArgs());
            e.Handled = true;
        }
        // 숫자 1~5 → 탭 전환
        else if (e.Key >= Key.D1 && e.Key <= Key.D5 &&
                 Keyboard.Modifiers == ModifierKeys.None)
        {
            SwitchTab(e.Key - Key.D0);
            e.Handled = true;
        }
    }

    // §6 ─ 테마 변경 이벤트 처리 ─────────────────────────────

    private void OnThemeChanged(ThemeKind theme)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateThemeDisplay(theme);
            UpdateLogoIcon(theme);
            SwitchTab(_currentTab);
        });
    }

    private void UpdateThemeDisplay(ThemeKind theme)
    {
        var info = ThemeManager.GetInfo(theme);
        CurrentThemeName.Text = info.DisplayName;
        StatusText.Text       = $"{info.DisplayName} 테마 적용됨 — {info.Description}";
    }

    /// <summary>테마별 아이콘 이모지 교체 — 각 테마 분위기에 맞게</summary>
    private void UpdateLogoIcon(ThemeKind theme)
    {
        LogoIcon.Text = theme switch
        {
            ThemeKind.DarkNavy      => "⚡",   // 전기 / 우주 제어실
            ThemeKind.SteelLight    => "🏭",   // 공장 / 산업
            ThemeKind.NeonCyber     => "🔮",   // 사이버펑크 / 네온
            ThemeKind.WarmAmber     => "🔥",   // 정유공장 / 불꽃
            ThemeKind.ArcticFrost   => "❄️",   // 얼음 / 북유럽
            ThemeKind.TerminalGreen => "💻",   // 레트로 터미널
            ThemeKind.CarbonElite   => "💎",   // 럭셔리 / 다이아몬드
            _                       => "⚡"
        };
    }

    // §7 ─ 창 닫기 ────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }
}
