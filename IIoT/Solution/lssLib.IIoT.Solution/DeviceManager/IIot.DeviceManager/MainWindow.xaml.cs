// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · MainWindow.xaml.cs
//  역할: 메인 윈도우 코드비하인드 — 테마 단축키 + ThemeChanged 구독
//  Phase 0: 테마 통합
// ══════════════════════════════════════════════════════════

using IIoT.UI.Themes;
using System.Windows;
using System.Windows.Input;

namespace IIoT.DeviceManager;

public partial class MainWindow : Window
{
    // §1 ─ 생성자 ─────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        // ★ Phase 0 규칙: ThemeChanged 구독 — OnClosed에서 반드시 해제
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    // §2 ─ 테마 이벤트 핸들러 ──────────────────────────────────
    private void OnThemeChanged(ThemeKind kind)
    {
        // 테마 변경 시 필요한 추가 UI 업데이트 처리
        // 예) 상태바 텍스트 갱신, 아이콘 색상 교체 등
        // 현재 Phase 0에서는 DynamicResource가 자동 처리하므로 추가 코드 불필요
    }

    // §3 ─ 단축키 처리 ────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // ★ Phase 0 규칙: AllThemes 인덱스 기반 순환 — enum 산술 절대 금지
        switch (e.Key)
        {
            // Ctrl+T: 다음 테마
            case Key.T when Keyboard.Modifiers == ModifierKeys.Control:
                NavigateTheme(+1);
                e.Handled = true;
                break;

            // Ctrl+Shift+T: 이전 테마
            case Key.T when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                NavigateTheme(-1);
                e.Handled = true;
                break;

            // Ctrl+S: 저장 (Phase 4에서 XmlWriteService 연결 예정)
            case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                OnSaveRequested();
                e.Handled = true;
                break;
        }
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>
    /// 테마를 인덱스 기반으로 순환 이동합니다.
    /// </summary>
    /// <param name="direction">+1 = 다음, -1 = 이전</param>
    private static void NavigateTheme(int direction)
    {
        var list = ThemeManager.AllThemes;
        var idx  = list.Select((t, i) => (t, i))
                       .FirstOrDefault(x => x.t.Kind == ThemeManager.Current).i;
        var next = list[((idx + direction) % list.Count + list.Count) % list.Count].Kind;
        ThemeManager.Apply(next);
    }

    private void OnSaveRequested()
    {
        // TODO Phase 4: XmlWriteService.SaveAsync() 연결 예정
        TxtStatus.Text = "저장 기능은 Phase 4에서 구현됩니다.";
    }

    // §5 ─ 윈도우 수명 주기 ────────────────────────────────────
    protected override void OnClosed(EventArgs e)
    {
        // ★ Phase 0 규칙: 정적 이벤트 구독 해제 필수 (메모리 누수 방지)
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }
}
