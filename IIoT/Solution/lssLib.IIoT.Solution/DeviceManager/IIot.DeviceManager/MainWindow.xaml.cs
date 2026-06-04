// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · MainWindow.xaml.cs
//  역할: 통합 레이아웃 코드비하인드
//  Phase 5R: 상단 탭 전환 로직
//    - 장비관리 탭: DeviceManageArea 표시
//    - 스케일/알람/통신 탭: 라이브러리 관리 모드 전환
//                          MainViewModel 의 SwitchTo* 커맨드 호출
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.ViewModels;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using IIoT.UI.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IIoT.DeviceManager;

public partial class MainWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private readonly MainViewModel       _mainVm;
    private readonly DeviceTreeViewModel _deviceTree;
    private string _activeTopTab = "Device";

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public MainWindow(MainViewModel mainViewModel, DeviceTreeViewModel deviceTree)
    {
        _mainVm     = mainViewModel;
        _deviceTree = deviceTree;

        InitializeComponent();
        DataContext = _mainVm;

        // Phase 0: ThemeChanged 구독
        ThemeManager.ThemeChanged += OnThemeChanged;

        // 트리 선택 변경 → 상태바 갱신
        _deviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
                _UpdateStatusBar();
            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
                TxtNodeCount.Text = _deviceTree.TotalNodeCount.ToString();
        };

        // SaveStatus 변경 → 상태바 갱신
        _mainVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SaveStatus))
                TxtStatus.Text = _mainVm.SaveStatus;
        };

        // 초기 탭 활성화
        _ActivateTopTab("Device");

        // 앱 시작 시 device.json 로드
        Loaded += async (_, _) =>
        {
            await _mainVm.LoadDeviceTreeCommand.ExecuteAsync(null);
            TxtNodeCount.Text = _deviceTree.TotalNodeCount.ToString();
        };
    }

    // §3 ─ 상단 탭 전환 ──────────────────────────────────────

    private void BtnTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _ActivateTopTab(btn.Tag?.ToString() ?? "Device");
    }

    /// <summary>
    /// 상단 탭 전환 처리.
    ///
    /// Device 탭:
    ///   · DeviceManageArea 표시 (트리 + 동적 우측 패널)
    ///   · 마지막 트리 선택 노드 상태 복원
    ///
    /// Scale / Alarm / Comm 탭:
    ///   · MainViewModel.SwitchTo*Command 호출
    ///   · RightPanelMode = ScaleLibrary / AlarmLibrary / CommLibrary
    ///   · 트리 선택 초기화 (라이브러리 전체 관리 모드)
    ///   · DeviceManageArea 에서 우측 패널이 라이브러리 뷰로 교체됨
    /// </summary>
    private void _ActivateTopTab(string tabKey)
    {
        _activeTopTab = tabKey;
        _UpdateTopTabStyles(tabKey);

        switch (tabKey)
        {
            case "Device":
                // 트리 선택 노드 유지 → 우측 패널 자연스럽게 복원
                TxtStatus.Text = _mainVm.SaveStatus;
                break;

            case "Scale":
                _mainVm.SwitchToScaleLibraryCommand.Execute(null);
                TxtStatus.Text = "스케일 라이브러리 관리";
                break;

            case "Alarm":
                _mainVm.SwitchToAlarmLibraryCommand.Execute(null);
                TxtStatus.Text = "알람 규칙 라이브러리 관리";
                break;

            case "Comm":
                _mainVm.SwitchToCommLibraryCommand.Execute(null);
                TxtStatus.Text = "통신 설정 라이브러리 관리";
                break;
        }
    }

    private void _UpdateTopTabStyles(string activeKey)
    {
        foreach (var btn in new[] { BtnTabDevice, BtnTabScale, BtnTabAlarm, BtnTabComm })
        {
            bool isActive = btn.Tag?.ToString() == activeKey;
            btn.FontWeight      = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            btn.BorderThickness = isActive ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }
    }

    // §4 ─ 저장 버튼 ──────────────────────────────────────────

    private void BtnSave_Click(object sender, RoutedEventArgs e)
        => _OnSaveRequested();

    // §5 ─ 키보드 단축키 ──────────────────────────────────────

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

    // §6 ─ 윈도우 생명주기 ────────────────────────────────────

    protected override void OnClosed(System.EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }

    // §7 ─ 내부 헬퍼 ──────────────────────────────────────────

    private void OnThemeChanged(ThemeKind kind)
        => TxtStatus.Text = $"테마 변경: {kind}";

    private static void _NavigateTheme(int direction)
    {
        var list = ThemeManager.AllThemes;
        var idx  = list.Select((t, i) => (t, i))
                       .FirstOrDefault(x => x.t.Kind == ThemeManager.Current).i;
        var next = list[((idx + direction) % list.Count + list.Count) % list.Count].Kind;
        ThemeManager.Apply(next);
    }

    private void _OnSaveRequested()
    {
        // 어느 탭에 있든 트리(device.json) 저장
        if (_mainVm.SaveDeviceTreeCommand.CanExecute(null))
            _mainVm.SaveDeviceTreeCommand.Execute(null);

        // 라이브러리 탭에 있으면 해당 라이브러리도 저장
        switch (_activeTopTab)
        {
            case "Scale": _mainVm.ScaleLibrary.SaveCommand.Execute(null); break;
            case "Alarm": _mainVm.AlarmLibrary.SaveCommand.Execute(null); break;
            case "Comm":  _mainVm.CommLibrary.SaveCommand.Execute(null);  break;
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
