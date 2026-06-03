// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · MainWindow.xaml.cs
//  역할: 메인 윈도우 코드비하인드
//  Phase 0 : 테마 단축키 + ThemeChanged 구독
//  Phase 2 : DeviceTree 상태바 바인딩
//  Phase 4 : 탭 전환 → ColTree.Width + LibraryArea.Content 교체
//  Phase 5 : DataContext = MainViewModel 교체
//            Ctrl+S → SaveDeviceTreeCommand 실동작
//            앱 시작 시 device.json 자동 로드 (LoadDeviceTreeCommand)
//
//  ★ Phase 5 Fix:
//    - CommunityToolkit [RelayCommand] 비동기 메서드명 규칙 적용
//      메서드명 SaveDeviceTree  → 커맨드명 SaveDeviceTreeCommand  (O)
//      메서드명 LoadDeviceTree  → 커맨드명 LoadDeviceTreeCommand  (O)
//      (기존 SaveDeviceTreeAsyncCommand / LoadDeviceTreeAsyncCommand 는 오류)
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.ViewModels;
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

    // ★ Phase 5: MainViewModel 직접 보관 (DataContext 이기도 함)
    private readonly MainViewModel        _mainVm;
    private readonly DeviceTreeViewModel  _deviceTree;
    private readonly JsonConfigLoader     _configLoader;
    private readonly ScaleLibraryViewModel _scaleVm;
    private readonly AlarmLibraryViewModel _alarmVm;
    private readonly CommLibraryViewModel  _commVm;

    // Phase 4: 라이브러리 뷰 인스턴스 (재사용)
    private readonly ScaleLibraryView _scaleView = new();
    private readonly AlarmRuleView    _alarmView = new();
    private readonly CommLibraryView  _commView  = new();

    private string _activeTab = "Device";

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public MainWindow(
        MainViewModel          mainViewModel,   // ★ Phase 5
        DeviceTreeViewModel    deviceTree,
        JsonConfigLoader       configLoader,
        ScaleLibraryViewModel  scaleVm,
        AlarmLibraryViewModel  alarmVm,
        CommLibraryViewModel   commVm)
    {
        _mainVm       = mainViewModel;
        _deviceTree   = deviceTree;
        _configLoader = configLoader;
        _scaleVm      = scaleVm;
        _alarmVm      = alarmVm;
        _commVm       = commVm;

        InitializeComponent();

        // ★ Phase 5: DataContext = MainViewModel (기존 DataContext = this 교체)
        DataContext = _mainVm;

        // Phase 4: 라이브러리 뷰 DataContext 연결
        _scaleView.DataContext = _scaleVm;
        _alarmView.DataContext = _alarmVm;
        _commView.DataContext  = _commVm;

        // Phase 0: ThemeChanged 구독 — OnClosed에서 반드시 해제
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Phase 2: 트리 선택 변경 → 상태바 동기화
        _deviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
                _UpdateStatusBar();
            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
                TxtNodeCount.Text = _deviceTree.TotalNodeCount.ToString();
        };

        // ★ Phase 5: SaveStatus 변경 → 상태바 동기화
        _mainVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SaveStatus))
                TxtStatus.Text = _mainVm.SaveStatus;
        };

        // Device 탭 기본 활성화
        _ActivateTab("Device");

        // ★ Phase 5: 앱 시작 시 device.json 자동 로드
        //   커맨드명: LoadDeviceTreeCommand
        //   (CommunityToolkit [RelayCommand] → 메서드명 LoadDeviceTree → 커맨드명 LoadDeviceTreeCommand)
        Loaded += async (_, _) =>
        {
            await _mainVm.LoadDeviceTreeCommand.ExecuteAsync(null);
            TxtNodeCount.Text = _deviceTree.TotalNodeCount.ToString();
        };
    }

    // §3 ─ 탭 전환 (Phase 4) ──────────────────────────────────

    private void BtnTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _ActivateTab(btn.Tag?.ToString() ?? "Device");
    }

    private void _ActivateTab(string tabKey)
    {
        _activeTab = tabKey;
        _UpdateTabButtonStyles(tabKey);

        if (tabKey == "Device")
        {
            ColTree.Width     = new GridLength(280, GridUnitType.Pixel);
            ColSplitter.Width = new GridLength(4,   GridUnitType.Pixel);
            DeviceTreePanel.Visibility  = Visibility.Visible;
            GridSplitter1.Visibility    = Visibility.Visible;
            DeviceEditorGrid.Visibility = Visibility.Visible;
            LibraryArea.Visibility      = Visibility.Collapsed;
            LibraryArea.Content         = null;
            TxtStatus.Text = _mainVm.SaveStatus;
            return;
        }

        ColTree.Width     = new GridLength(0, GridUnitType.Pixel);
        ColSplitter.Width = new GridLength(0, GridUnitType.Pixel);
        DeviceTreePanel.Visibility  = Visibility.Collapsed;
        GridSplitter1.Visibility    = Visibility.Collapsed;
        DeviceEditorGrid.Visibility = Visibility.Collapsed;
        LibraryArea.Visibility      = Visibility.Visible;

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

    private void _UpdateTabButtonStyles(string activeKey)
    {
        foreach (var btn in new[] { BtnTabDevice, BtnTabScale, BtnTabAlarm, BtnTabComm })
        {
            bool isActive = btn.Tag?.ToString() == activeKey;
            btn.FontWeight      = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            btn.BorderThickness = isActive ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }
    }

    // §4 ─ 테마 이벤트 ────────────────────────────────────────

    private void OnThemeChanged(ThemeKind kind)
        => TxtStatus.Text = $"테마 변경: {kind}";

    // §5 ─ 키보드 단축키 ──────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.T when Keyboard.Modifiers == ModifierKeys.Control:
                _NavigateTheme(+1);
                e.Handled = true;
                break;
            case Key.T when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                _NavigateTheme(-1);
                e.Handled = true;
                break;
            case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                _OnSaveRequested();
                e.Handled = true;
                break;
        }
    }

    // §6 ─ 버튼 이벤트 ────────────────────────────────────────

    private void BtnSave_Click(object sender, RoutedEventArgs e)
        => _OnSaveRequested();

    // §7 ─ 윈도우 생명주기 ────────────────────────────────────

    protected override void OnClosed(System.EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }

    // §8 ─ 내부 메서드 ────────────────────────────────────────

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
        switch (_activeTab)
        {
            // ★ Phase 5: 커맨드명 SaveDeviceTreeCommand
            //   (CommunityToolkit [RelayCommand] → 메서드명 SaveDeviceTree → SaveDeviceTreeCommand)
            case "Device":
                if (_mainVm.SaveDeviceTreeCommand.CanExecute(null))
                    _mainVm.SaveDeviceTreeCommand.Execute(null);
                break;

            case "Scale": _scaleVm.SaveCommand.Execute(null); break;
            case "Alarm": _alarmVm.SaveCommand.Execute(null); break;
            case "Comm":  _commVm.SaveCommand.Execute(null);  break;
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
