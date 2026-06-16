// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes.Demo · MainWindow.xaml.cs
//  역할: 테마 데모 메인 윈도우 코드비하인드
//        - 탭 전환 (7개 섹션)
//        - 단축키 (Ctrl+T / Ctrl+Shift+T) — 헤더는 ThemePickerButton 으로 대체
//        - 테마 변경 이벤트 → 상태바 갱신
//  생성: 2025-05-16
//  수정: 2025-05-17 v1.5
//    · Tab7 "피커 버튼" 추가
//    · 헤더 ◀▶ 버튼 제거 → ThemePickerButton 으로 대체
//      (CurrentThemeName TextBlock 제거, OnNextTheme/OnPrevTheme 는 단축키용 유지)
//    · 단축키 범위 D1~D7 으로 확장
//    · UpdateThemeDisplay: StatusText 만 갱신 (CurrentThemeName 제거됨)
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
    private Dictionary<int, (Button Btn, UIElement View)> _tabs = null!;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        // 탭 매핑 (v1.5: Tab7 추가)
        _tabs = new()
        {
            [1] = (Tab1Btn, Tab1),
            [2] = (Tab2Btn, Tab2),
            [3] = (Tab3Btn, Tab3),
            [4] = (Tab4Btn, Tab4),
            [5] = (Tab5Btn, Tab5),
            [6] = (Tab6Btn, Tab6),
            [7] = (Tab7Btn, Tab7),
        };

        // 테마 변경 이벤트 구독 → 상태바 + 로고 갱신
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
            view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

            if (isActive)
            {
                btn.Background  = Application.Current.Resources["AccFaintBrush"] as System.Windows.Media.Brush;
                btn.BorderBrush = Application.Current.Resources["AccBrush"]      as System.Windows.Media.Brush;
                btn.Foreground  = Application.Current.Resources["AccBrush"]      as System.Windows.Media.Brush;
            }
            else
            {
                btn.Background  = Application.Current.Resources["CardBrush"]    as System.Windows.Media.Brush;
                btn.BorderBrush = Application.Current.Resources["Border2Brush"] as System.Windows.Media.Brush;
                btn.Foreground  = Application.Current.Resources["Text2Brush"]   as System.Windows.Media.Brush;
            }
        }
    }

    // §4 ─ 테마 순환 (단축키 전용) ───────────────────────────
    //
    // ★ 헤더의 ◀▶ 버튼은 v1.5 에서 ThemePickerButton 으로 교체됨.
    //   OnNextTheme / OnPrevTheme 는 Ctrl+T / Ctrl+Shift+T 단축키 처리 전용으로 유지.
    //   AllThemes 인덱스 기반 순환 (enum 값 산술 금지 — NoTheme=-1 로 인한 오류 방지).

    private void OnNextTheme()
    {
        var list = ThemeManager.AllThemes;
        var idx  = list.Select((t, i) => (t, i))
                       .FirstOrDefault(x => x.t.Kind == ThemeManager.Current).i;
        ThemeManager.Apply(list[(idx + 1) % list.Count].Kind);
    }

    private void OnPrevTheme()
    {
        var list = ThemeManager.AllThemes;
        var idx  = list.Select((t, i) => (t, i))
                       .FirstOrDefault(x => x.t.Kind == ThemeManager.Current).i;
        ThemeManager.Apply(list[(idx - 1 + list.Count) % list.Count].Kind);
    }

    // §5 ─ 단축키 ─────────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Ctrl+T → 다음 테마
        if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnNextTheme();
            e.Handled = true;
        }
        // Ctrl+Shift+T → 이전 테마
        else if (e.Key == Key.T &&
                 Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            OnPrevTheme();
            e.Handled = true;
        }
        // 숫자 1~7 → 탭 전환 (v1.5: D7 추가)
        else if (e.Key >= Key.D1 && e.Key <= Key.D7 &&
                 Keyboard.Modifiers == ModifierKeys.None)
        {
            SwitchTab(e.Key - Key.D0);
            e.Handled = true;
        }
    }

    // §6 ─ 테마 변경 이벤트 ───────────────────────────────────

    private void OnThemeChanged(ThemeKind theme)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateThemeDisplay(theme);
            UpdateLogoIcon(theme);
            // 탭 버튼 색상 갱신 (테마 전환 시 AccBrush 등이 바뀌므로)
            SwitchTab(_currentTab);
        });
    }

    /// <summary>
    /// 테마 변경 시 상태바 텍스트 갱신.
    /// v1.5: 헤더의 CurrentThemeName TextBlock 은 ThemePickerButton 으로 대체됨.
    ///        → StatusText 만 갱신.
    /// </summary>
    private void UpdateThemeDisplay(ThemeKind theme)
    {
        var info = ThemeManager.GetInfo(theme);
        StatusText.Text = $"{info.DisplayName} 테마 적용됨 — {info.Description}";
    }

    /// <summary>테마별 로고 이모지 교체</summary>
    private void UpdateLogoIcon(ThemeKind theme)
    {
        LogoIcon.Text = theme switch
        {
            ThemeKind.NoTheme       => "🖥",
            ThemeKind.DarkNavy      => "⚡",
            ThemeKind.SteelLight    => "🏭",
            ThemeKind.NeonCyber     => "🔮",
            ThemeKind.WarmAmber     => "🔥",
            ThemeKind.ArcticFrost   => "❄️",
            ThemeKind.TerminalGreen => "💻",
            ThemeKind.CarbonElite   => "💎",
            _                       => "⚡"
        };
    }

    // §7 ─ 창 닫기 ────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }

    // §8 ─ DataGrid 샘플 데이터 ───────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        LoadGridSamples();
    }

    private void LoadGridSamples()
    {
        TagGrid.ItemsSource = new[]
        {
            new TagRow("온도",     "D100", "150.3",  "°C",  "GOOD",  "Good",      "09:31:22"),
            new TagRow("압력",     "D102", "7.42",   "bar", "GOOD",  "Good",      "09:31:22"),
            new TagRow("속도",     "D104", "1842",   "rpm", "WARN",  "Uncertain", "09:31:20"),
            new TagRow("전류",     "D106", "28.7",   "A",   "GOOD",  "Good",      "09:31:22"),
            new TagRow("진동",     "D108", "---",    "mm/s","ERR",   "Bad",       "---"),
            new TagRow("냉각수온", "D110", "32.1",   "°C",  "GOOD",  "Good",      "09:31:21"),
        };

        AlarmGrid.ItemsSource = new[]
        {
            new AlarmRow("HH", "압출기#1 › 온도",    "286.3 °C", "280.0 °C", "09:23:14", "미확인"),
            new AlarmRow("HH", "사출기#1 › 압력",    "9.87 bar", "9.50 bar", "09:28:44", "미확인"),
            new AlarmRow("H",  "압출기#2 › 전류",    "34.2 A",   "32.0 A",   "09:30:01", "미확인"),
            new AlarmRow("H",  "압출기#1 › 속도",    "2100 rpm", "2000 rpm", "09:15:33", "확인"),
            new AlarmRow("L",  "냉각수 탱크 › 수위", "18.3 %",   "20.0 %",   "09:05:10", "확인"),
        };
    }
}

// ── 샘플 데이터 레코드 ──────────────────────────────────────

internal record TagRow(
    string Name, string Address, string Value,
    string Unit, string Status, string Quality, string Timestamp);

internal record AlarmRow(
    string Level, string Tag, string Value,
    string Threshold, string OccurredAt, string AckStatus);
