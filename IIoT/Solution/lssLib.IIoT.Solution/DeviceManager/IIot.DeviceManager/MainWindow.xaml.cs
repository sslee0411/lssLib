// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · MainWindow.xaml.cs
//  역할: 메인 윈도우 코드비하인드
//  Phase 0 : 테마 단축키 + ThemeChanged 구독
//  Phase 1 : Core 기반
//  Phase 2 : DeviceTree 상태바 바인딩 ← 현재
//  수정    : 2025-05-22
// ══════════════════════════════════════════════════════════
using System;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using IIoT.UI.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IIoT.DeviceManager;

public partial class MainWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly DeviceTreeViewModel _deviceTree;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    /// <summary>DI에서 DeviceTreeViewModel 주입.</summary>
    public MainWindow(DeviceTreeViewModel deviceTree)
    {
        _deviceTree = deviceTree;
        InitializeComponent();

        // DataContext 설정 — DeviceTreeView 가 Binding DeviceTree 를 찾을 수 있도록
        DataContext = this;

        // ★ Phase 0 규칙: ThemeChanged 구독 — OnClosed에서 반드시 해제
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Phase 2: 트리 선택 변경 → 상태바 동기화
        _deviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
                UpdateStatusBar();
            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
                TxtNodeCount.Text = _deviceTree.TotalNodeCount.ToString();
        };
    }

    // §3 ─ 바인딩용 속성 (DataContext = this) ─────────────────

    /// <summary>DeviceTreeView DataContext 바인딩용</summary>
    public DeviceTreeViewModel DeviceTree => _deviceTree;

    // §4 ─ 테마 이벤트 핸들러 ──────────────────────────────────

    private void OnThemeChanged(ThemeKind kind)
    {
        // DynamicResource 가 자동 처리 — 현재 추가 작업 없음
        TxtStatus.Text = $"테마 변경: {kind}";
    }

    // §5 ─ 키보드 단축키 ──────────────────────────────────────

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

            // Ctrl+S: 저장 (Phase 6에서 XmlWriteService 연결 예정)
            case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                OnSaveRequested();
                e.Handled = true;
                break;
        }
    }

    // §6 ─ 버튼 이벤트 ────────────────────────────────────────

    private void BtnSave_Click(object sender, RoutedEventArgs e)
        => OnSaveRequested();

    private void BtnTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        TxtStatus.Text = $"탭: {btn.Tag} (Phase 3~5 구현 예정)";
    }

    // §7 ─ 윈도우 생명주기 ────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        // ★ Phase 0 규칙: ThemeChanged 구독 반드시 해제
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }

    // §8 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>테마를 인덱스 기반으로 순환 이동 (AllThemes 리스트 기반)</summary>
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
        // TODO Phase 6: JsonWriteService / XmlWriteService 연결 예정
        TxtStatus.Text = "저장 기능은 Phase 6에서 구현됩니다.";
    }

    private void UpdateStatusBar()
    {
        var node = _deviceTree.SelectedNode;
        TxtSelectedNode.Text = node is null
            ? "없음"
            : $"{node.IconGlyph} {node.Name} ({node.Kind})";
    }
}
