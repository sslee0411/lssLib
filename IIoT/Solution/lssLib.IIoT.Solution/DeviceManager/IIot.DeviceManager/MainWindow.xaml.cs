// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · MainWindow.xaml.cs
//  역할: 메인 윈도우 코드비하인드
//  Phase 0 : 테마 단축키 + ThemeChanged 구독
//  Phase 2 : DeviceTree 상태바 바인딩
//  Phase 4 : 탭 전환 → ColTree.Width + LibraryArea.Content 교체
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using IIoT.DeviceManager.ViewModels.Library;
using IIoT.DeviceManager.Views.Library;
using IIoT.UI.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IIoT.DeviceManager;

public partial class MainWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private readonly DeviceTreeViewModel _deviceTree;
    private readonly JsonConfigLoader _configLoader;
    private readonly ScaleLibraryViewModel _scaleVm;
    private readonly AlarmLibraryViewModel _alarmVm;
    private readonly CommLibraryViewModel _commVm;

    // ★ Phase 4: 라이브러리 뷰 인스턴스 (재사용, DataContext는 생성자에서 주입)
    private readonly ScaleLibraryView _scaleView = new();
    private readonly AlarmRuleView _alarmView = new();
    private readonly CommLibraryView _commView = new();

    private string _activeTab = "Device";

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public MainWindow(
        DeviceTreeViewModel deviceTree,
        JsonConfigLoader configLoader,
        ScaleLibraryViewModel scaleVm,
        AlarmLibraryViewModel alarmVm,
        CommLibraryViewModel commVm)
    {
        _deviceTree = deviceTree;
        _configLoader = configLoader;
        _scaleVm = scaleVm;
        _alarmVm = alarmVm;
        _commVm = commVm;

        InitializeComponent();
        DataContext = this;

        // ★ Phase 4: 라이브러리 뷰 DataContext 연결
        _scaleView.DataContext = _scaleVm;
        _alarmView.DataContext = _alarmVm;
        _commView.DataContext = _commVm;

        // Phase 0 규칙: ThemeChanged 구독 — OnClosed에서 반드시 해제
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Phase 2: 트리 선택 변경 → 상태바 동기화
        _deviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
                _UpdateStatusBar();
            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
                TxtNodeCount.Text = _deviceTree.TotalNodeCount.ToString();
        };

        // Device 탭 기본 활성화 (BtnTabDevice 하이라이트)
        _ActivateTab("Device");
    }

    // §3 ─ 바인딩용 속성 (DataContext = this) ─────────────────
    public DeviceTreeViewModel DeviceTree => _deviceTree;

    /// <summary>
    /// Phase 5에서 MainViewModel.SelectedEditor 로 교체 예정.
    /// 현재는 null 반환 → DeviceEditorGrid 플레이스홀더 표시.
    /// </summary>
    public object? SelectedEditor => null;

    // §4 ─ 탭 전환 (★ Phase 4 핵심) ──────────────────────────

    private void BtnTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _ActivateTab(btn.Tag?.ToString() ?? "Device");
    }

    /// <summary>
    /// 탭 전환 처리.
    ///
    /// Device 탭:
    ///   · ColTree, ColSplitter 원래 Width 복원
    ///   · DeviceTreePanel, GridSplitter1 표시
    ///   · DeviceEditorGrid 표시, LibraryArea 숨김
    ///
    /// Library 탭 (Scale/Alarm/Comm):
    ///   · ColTree, ColSplitter Width = 0 (전체 너비 확보)
    ///   · DeviceTreePanel, GridSplitter1 숨김
    ///   · DeviceEditorGrid 숨김, LibraryArea 표시
    ///   · LibraryArea.Content 에 해당 뷰 주입
    /// </summary>
    private void _ActivateTab(string tabKey)
    {
        _activeTab = tabKey;
        _UpdateTabButtonStyles(tabKey);

        if (tabKey == "Device")
        {
            // 좌측 트리 패널 복원
            ColTree.Width = new GridLength(280, GridUnitType.Pixel);
            ColSplitter.Width = new GridLength(4, GridUnitType.Pixel);
            DeviceTreePanel.Visibility = Visibility.Visible;
            GridSplitter1.Visibility = Visibility.Visible;

            // 편집 영역 전환
            DeviceEditorGrid.Visibility = Visibility.Visible;
            LibraryArea.Visibility = Visibility.Collapsed;
            LibraryArea.Content = null;

            TxtStatus.Text = "장비 관리";
            return;
        }

        // ── 라이브러리 탭 ─────────────────────────────────────
        // 좌측 트리 패널 숨김 (라이브러리 뷰 전체 너비 확보)
        ColTree.Width = new GridLength(0, GridUnitType.Pixel);
        ColSplitter.Width = new GridLength(0, GridUnitType.Pixel);
        DeviceTreePanel.Visibility = Visibility.Collapsed;
        GridSplitter1.Visibility = Visibility.Collapsed;

        // 편집 영역 전환
        DeviceEditorGrid.Visibility = Visibility.Collapsed;
        LibraryArea.Visibility = Visibility.Visible;

        // 탭에 맞는 뷰 + 데이터 주입
        var bundle = _configLoader.LoadAll();

        switch (tabKey)
        {
            case "Scale":
                _scaleVm.Load(bundle.Scales);
                LibraryArea.Content = _scaleView;
                TxtStatus.Text = "스케일 라이브러리";
                break;

            case "Alarm":
                _alarmVm.Load(bundle.AlarmRules);
                LibraryArea.Content = _alarmView;
                TxtStatus.Text = "알람 규칙 라이브러리";
                break;

            case "Comm":
                _commVm.Load(bundle.CommConfigs);
                LibraryArea.Content = _commView;
                TxtStatus.Text = "통신 설정 라이브러리";
                break;
        }
    }

    /// <summary>활성 탭 버튼 하이라이트 업데이트</summary>
    private void _UpdateTabButtonStyles(string activeKey)
    {
        foreach (var btn in new[] { BtnTabDevice, BtnTabScale, BtnTabAlarm, BtnTabComm })
        {
            bool isActive = btn.Tag?.ToString() == activeKey;
            btn.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            btn.BorderThickness = isActive ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }
    }

    // §5 ─ 테마 이벤트 핸들러 ──────────────────────────────────
    private void OnThemeChanged(ThemeKind kind)
        => TxtStatus.Text = $"테마 변경: {kind}";

    // §6 ─ 키보드 단축키 ──────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.T when Keyboard.Modifiers == ModifierKeys.Control:
                _NavigateTheme(+1); e.Handled = true; break;
            case Key.T when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                _NavigateTheme(-1); e.Handled = true; break;
            case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                _OnSaveRequested(); e.Handled = true; break;
        }
    }

    // §7 ─ 버튼 이벤트 ────────────────────────────────────────
    private void BtnSave_Click(object sender, RoutedEventArgs e)
        => _OnSaveRequested();

    // §8 ─ 윈도우 생명주기 ────────────────────────────────────
    protected override void OnClosed(System.EventArgs e)
    {
        // ★ Phase 0 규칙: ThemeChanged 구독 반드시 해제
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }

    // §9 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>테마 인덱스 기반 순환 — AllThemes 리스트 기반 (enum 산술 금지)</summary>
    private static void _NavigateTheme(int direction)
    {
        var list = ThemeManager.AllThemes;
        var idx = list.Select((t, i) => (t, i))
                       .FirstOrDefault(x => x.t.Kind == ThemeManager.Current).i;
        var next = list[((idx + direction) % list.Count + list.Count) % list.Count].Kind;
        ThemeManager.Apply(next);
    }

    /// <summary>현재 활성 탭에 맞는 저장 실행</summary>
    private void _OnSaveRequested()
    {
        switch (_activeTab)
        {
            case "Scale": _scaleVm.SaveCommand.Execute(null); break;
            case "Alarm": _alarmVm.SaveCommand.Execute(null); break;
            case "Comm": _commVm.SaveCommand.Execute(null); break;
            default:
                TxtStatus.Text = "장치 트리 저장은 Phase 5에서 구현됩니다.";
                break;
        }
    }

    private void _UpdateStatusBar()
    {
        var node = _deviceTree.SelectedNode;
        TxtSelectedNode.Text = node is null
            ? "없음"
            : $"{node.IconGlyph} {node.Name} ({node.Kind})";
    }
}