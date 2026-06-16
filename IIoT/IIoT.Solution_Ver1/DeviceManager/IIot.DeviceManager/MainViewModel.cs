// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/MainViewModel.cs
//  역할: 메인 윈도우 통합 ViewModel
//  Phase 5R: 트리 선택 → 우측 패널 자동 구성
//            노드 종류에 따라 편집기 + 라이브러리 탭 동적 표시
//            DeviceEditor ↔ CommLibrary 직접 연계
//            SensorEditor ↔ ScaleLibrary / AlarmLibrary 직접 연계
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.Core.DataModel;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using IIoT.DeviceManager.ViewModels.Editors;
using IIoT.DeviceManager.ViewModels.Library;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels;

// ── 우측 패널 탭 모드 ──────────────────────────────────────
/// <summary>트리 선택 노드 종류별 우측 패널 모드</summary>
public enum RightPanelMode
{
    /// <summary>노드 미선택 — 전체 요약 대시보드</summary>
    Dashboard,
    /// <summary>Group 노드 — 그룹 정보만 표시</summary>
    GroupInfo,
    /// <summary>Device 노드 — 장비 정보 + 통신 설정 탭</summary>
    DeviceWithComm,
    /// <summary>Plc 노드 — PLC 설정 + 태그 목록 탭</summary>
    PlcWithTags,
    /// <summary>Tag 노드 — 태그 수집 설정</summary>
    TagEdit,
    /// <summary>Sensor 노드 — 센서 설정 + 스케일 + 알람 탭</summary>
    SensorFull,
    /// <summary>스케일 라이브러리 전체 관리 (상단 탭)</summary>
    ScaleLibrary,
    /// <summary>알람 라이브러리 전체 관리 (상단 탭)</summary>
    AlarmLibrary,
    /// <summary>통신 라이브러리 전체 관리 (상단 탭)</summary>
    CommLibrary,
}

// ── 우측 서브탭 항목 ──────────────────────────────────────
/// <summary>우측 패널 서브탭 1개 항목</summary>
public partial class RightTab : ObservableObject
{
    public string Header { get; init; } = string.Empty;
    public object? Content { get; init; }

    [ObservableProperty] private bool _isSelected;
}

// ── MainViewModel ─────────────────────────────────────────
public partial class MainViewModel : ObservableObject
{
    // §1 ─ 상수 & 의존성 ──────────────────────────────────
    private const string LogSrc = "MainViewModel";

    private readonly JsonConfigLoader  _configLoader;
    private readonly JsonWriteService  _writeService;

    // §2 ─ 서브 ViewModel ─────────────────────────────────
    public DeviceTreeViewModel DeviceTree { get; }

    // §3 ─ 편집기 VM (노드별) ─────────────────────────────
    private readonly DeviceEditorViewModel  _deviceEditor  = new();
    private readonly PlcEditorViewModel     _plcEditor     = new();
    private readonly TagEditorViewModel     _tagEditor     = new();
    private readonly SensorEditorViewModel  _sensorEditor  = new();

    // §4 ─ 라이브러리 VM ──────────────────────────────────
    public ScaleLibraryViewModel  ScaleLibrary  { get; }
    public AlarmLibraryViewModel  AlarmLibrary  { get; }
    public CommLibraryViewModel   CommLibrary   { get; }

    // §5 ─ 우측 패널 상태 ─────────────────────────────────

    /// <summary>현재 우측 패널 모드</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceMode))]
    [NotifyPropertyChangedFor(nameof(IsPlcMode))]
    [NotifyPropertyChangedFor(nameof(IsSensorMode))]
    [NotifyPropertyChangedFor(nameof(IsDashboard))]
    [NotifyPropertyChangedFor(nameof(IsTagMode))]
    private RightPanelMode _rightPanelMode = RightPanelMode.Dashboard;

    public bool IsDashboard    => RightPanelMode == RightPanelMode.Dashboard;
    public bool IsDeviceMode   => RightPanelMode == RightPanelMode.DeviceWithComm;
    public bool IsPlcMode      => RightPanelMode == RightPanelMode.PlcWithTags;
    public bool IsTagMode      => RightPanelMode == RightPanelMode.TagEdit;
    public bool IsSensorMode   => RightPanelMode == RightPanelMode.SensorFull;

    /// <summary>우측 서브탭 목록 (노드 종류에 따라 동적 생성)</summary>
    public ObservableCollection<RightTab> RightTabs { get; } = [];

    /// <summary>현재 선택된 서브탭</summary>
    [ObservableProperty]
    private RightTab? _selectedRightTab;

    /// <summary>현재 서브탭의 콘텐츠 ViewModel</summary>
    [ObservableProperty]
    private object? _rightContent;

    // §6 ─ 편집기 직접 노출 (XAML 바인딩용) ──────────────
    public DeviceEditorViewModel  DeviceEditor  => _deviceEditor;
    public PlcEditorViewModel     PlcEditor     => _plcEditor;
    public TagEditorViewModel     TagEditor     => _tagEditor;
    public SensorEditorViewModel  SensorEditor  => _sensorEditor;

    // §7 ─ 대시보드 통계 ──────────────────────────────────
    [ObservableProperty] private int _totalDeviceCount;
    [ObservableProperty] private int _totalTagCount;
    [ObservableProperty] private int _totalSensorCount;
    [ObservableProperty] private int _scaleLibraryCount;
    [ObservableProperty] private int _alarmLibraryCount;
    [ObservableProperty] private int _commLibraryCount;

    // §8 ─ 상태 프로퍼티 ──────────────────────────────────
    [ObservableProperty] private string _saveStatus = "준비";
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasUnsavedChanges;

    // §9 ─ 생성자 ─────────────────────────────────────────
    public MainViewModel(
        DeviceTreeViewModel  deviceTree,
        JsonConfigLoader     configLoader,
        JsonWriteService     writeService,
        ScaleLibraryViewModel  scaleLibrary,
        AlarmLibraryViewModel  alarmLibrary,
        CommLibraryViewModel   commLibrary)
    {
        DeviceTree    = deviceTree;
        _configLoader = configLoader;
        _writeService = writeService;
        ScaleLibrary  = scaleLibrary;
        AlarmLibrary  = alarmLibrary;
        CommLibrary   = commLibrary;

        // 트리 선택 변경 → 우측 패널 자동 교체
        DeviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
                _UpdateRightPanel(DeviceTree.SelectedNode);

            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
            {
                HasUnsavedChanges = true;
                _UpdateDashboardStats();
            }
        };
    }

    // §10 ─ 우측 패널 업데이트 (핵심) ────────────────────

    /// <summary>
    /// 선택된 트리 노드에 따라 우측 패널 모드와 서브탭을 동적으로 구성합니다.
    ///
    /// 노드별 패널 구성:
    ///   null           → Dashboard (전체 요약)
    ///   GroupNode      → [그룹 정보]
    ///   DeviceNode     → [장비 정보] [통신 설정]
    ///   PlcNode        → [PLC 설정] [태그 목록]
    ///   TagNode        → [태그 설정]
    ///   SensorNode     → [센서 설정] [스케일] [알람]
    /// </summary>
    private void _UpdateRightPanel(DeviceNodeViewModel? node)
    {
        var bundle = _configLoader.LoadAll();

        RightTabs.Clear();

        switch (node)
        {
            case null:
                RightPanelMode = RightPanelMode.Dashboard;
                _UpdateDashboardStats();
                RightContent = null;
                return;

            case GroupNodeViewModel group:
                RightPanelMode = RightPanelMode.GroupInfo;
                _AddTab("📁 그룹 정보", null); // GroupEditor 는 인라인 편집으로 충분
                break;

            case DeviceItemViewModel device:
                RightPanelMode = RightPanelMode.DeviceWithComm;

                // 장비 정보 편집기 로드 (CommConfig 목록 주입)
                _deviceEditor.Load(device, bundle.CommConfigs, bundle.Locations);
                _AddTab("📟 장비 정보", _deviceEditor);

                // 통신 설정: 현재 연결된 CommConfig 를 선택 상태로 라이브러리 표시
                CommLibrary.Load(bundle.CommConfigs);
                if (!string.IsNullOrEmpty(device.CommConfigId))
                    CommLibrary.SelectById(device.CommConfigId);
                _AddTab("🔌 통신 설정", CommLibrary);

                break;

            case PlcNodeViewModel plc:
                RightPanelMode = RightPanelMode.PlcWithTags;

                _plcEditor.Load(plc);
                _AddTab("⚙️ PLC 설정", _plcEditor);

                // PLC 하위 태그 목록 탭
                var plcTags = plc.Children.OfType<TagNodeViewModel>().ToList();
                _AddTab($"📋 태그 목록 ({plcTags.Count})", new TagListSummaryViewModel(plcTags));

                break;

            case TagNodeViewModel tag:
                RightPanelMode = RightPanelMode.TagEdit;

                var allNodes = DeviceTree.RootNodes.SelectMany(r => r.Flatten()).ToList();
                _tagEditor.Load(tag, allNodes);
                _AddTab("📋 태그 설정", _tagEditor);

                break;

            case SensorNodeViewModel sensor:
                RightPanelMode = RightPanelMode.SensorFull;

                // 형제 Tag 목록 수집
                var sibTags = node.Parent?.Children.OfType<TagNodeViewModel>().ToList() ?? [];
                _sensorEditor.Load(sensor, sibTags);
                _AddTab("🌡️ 센서 설정", _sensorEditor);

                // 스케일: 현재 ScaleConfigId 선택 상태로 라이브러리 표시
                ScaleLibrary.Load(bundle.Scales);
                if (!string.IsNullOrEmpty(sensor.ScaleConfigId))
                    ScaleLibrary.SelectById(sensor.ScaleConfigId);
                _AddTab("📐 스케일", ScaleLibrary);

                // 알람: 현재 AlarmGroupId 선택 상태로 라이브러리 표시
                AlarmLibrary.Load(bundle.AlarmRules);
                if (!string.IsNullOrEmpty(sensor.AlarmGroupId))
                    AlarmLibrary.SelectById(sensor.AlarmGroupId);
                _AddTab("🔔 알람 규칙", AlarmLibrary);

                break;
        }

        // 첫 번째 탭 자동 선택
        if (RightTabs.Count > 0)
        {
            SelectedRightTab = RightTabs[0];
            SelectedRightTab.IsSelected = true;
            RightContent = SelectedRightTab.Content;
        }
    }

    private void _AddTab(string header, object? content)
    {
        RightTabs.Add(new RightTab { Header = header, Content = content });
    }

    // §11 ─ 서브탭 전환 커맨드 ───────────────────────────

    [RelayCommand]
    private void SelectRightTab(RightTab tab)
    {
        if (tab is null) return;

        foreach (var t in RightTabs)
            t.IsSelected = false;

        tab.IsSelected = true;
        SelectedRightTab = tab;
        RightContent = tab.Content;
    }

    // §12 ─ 상단 탭 — 라이브러리 전체 관리 모드 ─────────

    /// <summary>
    /// 상단 탭에서 라이브러리 전체 관리 모드로 전환합니다.
    /// 트리 선택과 무관하게 라이브러리 전체 목록을 편집합니다.
    /// </summary>
    [RelayCommand]
    private void SwitchToScaleLibrary()
    {
        RightPanelMode = RightPanelMode.ScaleLibrary;
        var bundle = _configLoader.LoadAll();
        ScaleLibrary.Load(bundle.Scales);
        RightTabs.Clear();
        _AddTab("📐 스케일 라이브러리", ScaleLibrary);
        SelectedRightTab = RightTabs[0];
        SelectedRightTab.IsSelected = true;
        RightContent = ScaleLibrary;
        DeviceTree.SelectedNode = null;
    }

    [RelayCommand]
    private void SwitchToAlarmLibrary()
    {
        RightPanelMode = RightPanelMode.AlarmLibrary;
        var bundle = _configLoader.LoadAll();
        AlarmLibrary.Load(bundle.AlarmRules);
        RightTabs.Clear();
        _AddTab("🔔 알람 라이브러리", AlarmLibrary);
        SelectedRightTab = RightTabs[0];
        SelectedRightTab.IsSelected = true;
        RightContent = AlarmLibrary;
        DeviceTree.SelectedNode = null;
    }

    [RelayCommand]
    private void SwitchToCommLibrary()
    {
        RightPanelMode = RightPanelMode.CommLibrary;
        var bundle = _configLoader.LoadAll();
        CommLibrary.Load(bundle.CommConfigs);
        RightTabs.Clear();
        _AddTab("🔌 통신 라이브러리", CommLibrary);
        SelectedRightTab = RightTabs[0];
        SelectedRightTab.IsSelected = true;
        RightContent = CommLibrary;
        DeviceTree.SelectedNode = null;
    }

    // §13 ─ Sensor ↔ ScaleLibrary 연계 커맨드 ────────────

    /// <summary>
    /// ScaleLibrary 에서 선택한 Scale 을 현재 Sensor 에 연결합니다.
    /// Sensor 탭 → 스케일 탭에서 선택 후 이 커맨드 호출.
    /// </summary>
    [RelayCommand]
    private void ApplyScaleToSensor()
    {
        if (DeviceTree.SelectedNode is not SensorNodeViewModel sensor) return;
        if (ScaleLibrary.SelectedItem is null) return;

        sensor.ScaleConfigId  = ScaleLibrary.SelectedItem.Id;
        _sensorEditor.ScaleConfigId = ScaleLibrary.SelectedItem.Id;
        HasUnsavedChanges = true;

        SaveStatus = $"스케일 '{ScaleLibrary.SelectedItem.Name}' → " +
                     $"센서 '{sensor.Name}' 연결 완료";
        LogManager.Instance.Info(LogSrc,
            $"Scale 연결: {sensor.Name} ← {ScaleLibrary.SelectedItem.Name}");
    }

    /// <summary>
    /// AlarmLibrary 에서 선택한 AlarmRule 을 현재 Sensor 에 연결합니다.
    /// </summary>
    [RelayCommand]
    private void ApplyAlarmToSensor()
    {
        if (DeviceTree.SelectedNode is not SensorNodeViewModel sensor) return;
        if (AlarmLibrary.SelectedItem is null) return;

        sensor.AlarmGroupId    = AlarmLibrary.SelectedItem.Id;
        _sensorEditor.ScaleConfigId = AlarmLibrary.SelectedItem.Id;
        HasUnsavedChanges = true;

        SaveStatus = $"알람 규칙 '{AlarmLibrary.SelectedItem.Name}' → " +
                     $"센서 '{sensor.Name}' 연결 완료";
        LogManager.Instance.Info(LogSrc,
            $"Alarm 연결: {sensor.Name} ← {AlarmLibrary.SelectedItem.Name}");
    }

    /// <summary>
    /// CommLibrary 에서 선택한 CommConfig 를 현재 Device 에 연결합니다.
    /// </summary>
    [RelayCommand]
    private void ApplyCommToDevice()
    {
        if (DeviceTree.SelectedNode is not DeviceItemViewModel device) return;
        if (CommLibrary.SelectedItem is null) return;

        device.CommConfigId        = CommLibrary.SelectedItem.Id;
        _deviceEditor.CommConfigId = CommLibrary.SelectedItem.Id;
        HasUnsavedChanges = true;

        SaveStatus = $"통신 설정 '{CommLibrary.SelectedItem.Name}' → " +
                     $"장비 '{device.Name}' 연결 완료";
        LogManager.Instance.Info(LogSrc,
            $"Comm 연결: {device.Name} ← {CommLibrary.SelectedItem.Name}");
    }

    // §14 ─ 대시보드 통계 갱신 ────────────────────────────

    private void _UpdateDashboardStats()
    {
        var allNodes = DeviceTree.RootNodes.SelectMany(n => n.Flatten()).ToList();
        TotalDeviceCount  = allNodes.OfType<DeviceItemViewModel>().Count();
        TotalTagCount     = allNodes.OfType<TagNodeViewModel>().Count();
        TotalSensorCount  = allNodes.OfType<SensorNodeViewModel>().Count();

        var bundle = _configLoader.LoadAll();
        ScaleLibraryCount = bundle.Scales.Count;
        AlarmLibraryCount = bundle.AlarmRules.Count;
        CommLibraryCount  = bundle.CommConfigs.Count;
    }

    // §15 ─ 저장 커맨드 ───────────────────────────────────

    [RelayCommand(CanExecute = nameof(_CanExecute))]
    private async Task SaveDeviceTree()
    {
        if (IsBusy) return;
        IsBusy     = true;
        SaveStatus = "저장 중...";
        try
        {
            _ApplyCurrentEditor();
            await Task.Run(() =>
            {
                var tree = DeviceTreeSerializer.Serialize(DeviceTree.RootNodes);
                _writeService.SaveDeviceTree(tree);
            });
            HasUnsavedChanges = false;
            SaveStatus = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — {DeviceTree.TotalNodeCount}개 노드";
            LogManager.Instance.Info(LogSrc, $"device.json 저장 완료");
        }
        catch (Exception ex)
        {
            SaveStatus = $"저장 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"저장 오류: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(_CanExecute))]
    private async Task LoadDeviceTree()
    {
        if (IsBusy) return;
        IsBusy     = true;
        SaveStatus = "로드 중...";
        try
        {
            await Task.Run(() =>
            {
                var (tree, ok) = _configLoader.LoadDeviceTree();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    DeviceTree.RootNodes.Clear();
                    DeviceTree.SelectedNode = null;
                    foreach (var vm in DeviceTreeSerializer.Deserialize(tree))
                        DeviceTree.RootNodes.Add(vm);

                    SaveStatus = ok
                        ? $"로드 완료 — {DeviceTree.TotalNodeCount}개 노드"
                        : $"⚠ 무결성 경고 — {DeviceTree.TotalNodeCount}개 노드";
                });
            });
            HasUnsavedChanges = false;
            _UpdateDashboardStats();
            LogManager.Instance.Info(LogSrc, "device.json 로드 완료");
        }
        catch (Exception ex)
        {
            SaveStatus = $"로드 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"로드 오류: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private bool _CanExecute() => !IsBusy;

    private void _ApplyCurrentEditor()
    {
        if (_deviceEditor.HasChanges) _deviceEditor.ApplyCommand.Execute(null);
        if (_plcEditor.HasChanges)    _plcEditor.ApplyCommand.Execute(null);
        if (_tagEditor.HasChanges)    _tagEditor.ApplyCommand.Execute(null);
        if (_sensorEditor.HasChanges) _sensorEditor.ApplyCommand.Execute(null);
    }
}

// ── 태그 목록 요약 ViewModel (PLC 선택 시 탭에 표시) ─────
public partial class TagListSummaryViewModel : ObservableObject
{
    public IReadOnlyList<TagNodeViewModel> Tags { get; }
    public int Count => Tags.Count;

    public TagListSummaryViewModel(List<TagNodeViewModel> tags)
    {
        Tags = tags;
    }
}
