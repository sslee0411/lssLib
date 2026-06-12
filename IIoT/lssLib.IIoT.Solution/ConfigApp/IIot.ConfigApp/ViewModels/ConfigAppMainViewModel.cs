// ══════════════════════════════════════════════════════════
//  IIoT.ConfigApp · ViewModels/ConfigAppMainViewModel.cs
//  역할: ConfigApp 통합 MainViewModel
//        구 MainViewModel(DeviceManager) + CanvasViewModel 탭 통합
//  Phase 11: 신규
//
//  탭 구성:
//    ① 장비 관리 탭   — 트리 + 편집기 + 라이브러리 (구 DeviceManager)
//    ② 수집 흐름 탭   — NodeRed 스타일 캔버스 (신규 Phase 11)
//    ③ 스케일 관리 탭 — 스케일 라이브러리
//    ④ 알람 규칙 탭   — 알람 라이브러리
//    ⑤ 통신 설정 탭   — 통신 라이브러리
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.ConfigApp.Core.Config;
using IIoT.ConfigApp.ViewModels.Canvas;
using IIoT.ConfigApp.ViewModels.DeviceTree;
using IIoT.ConfigApp.ViewModels.Library;
using lssLib.Log;

namespace IIoT.ConfigApp.ViewModels;

/// <summary>
/// ConfigApp 통합 메인 ViewModel.
/// 구 DeviceManager MainViewModel 기능 전체 + CanvasViewModel 탭 추가.
///
/// ★ 구 DeviceManager 의 기존 MainViewModel 코드는 그대로 유지하고
///   이 클래스가 상속 또는 컴포지션으로 통합합니다.
///   (현재 구현: 핵심 참조만 포함, 구 코드 유지)
/// </summary>
public partial class ConfigAppMainViewModel : ObservableObject
{
    // §1 ─ 상수 ───────────────────────────────────────────────
    private const string LogSrc = "ConfigAppMainVM";

    // §2 ─ 서비스 ─────────────────────────────────────────────
    private readonly JsonConfigLoader     _configLoader;
    private readonly JsonWriteService     _writeService;
    private readonly CollectConfigService _collectService;

    // §3 ─ 서브 ViewModel ─────────────────────────────────────
    public DeviceTreeViewModel    DeviceTree   { get; }
    public ScaleLibraryViewModel  ScaleLibrary { get; }
    public AlarmLibraryViewModel  AlarmLibrary { get; }
    public CommLibraryViewModel   CommLibrary  { get; }
    public CanvasViewModel        Canvas       { get; }  // ★ Phase 11 신규

    // §4 ─ 탭 상태 ────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceTab))]
    [NotifyPropertyChangedFor(nameof(IsCanvasTab))]
    [NotifyPropertyChangedFor(nameof(IsScaleTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsCommTab))]
    private int _activeTabIndex = 0;  // 0=장비, 1=수집흐름, 2=스케일, 3=알람, 4=통신

    public bool IsDeviceTab => ActiveTabIndex == 0;
    public bool IsCanvasTab => ActiveTabIndex == 1;
    public bool IsScaleTab  => ActiveTabIndex == 2;
    public bool IsAlarmTab  => ActiveTabIndex == 3;
    public bool IsCommTab   => ActiveTabIndex == 4;

    // §5 ─ 공통 상태 ──────────────────────────────────────────
    [ObservableProperty] private string _saveStatus = "준비";
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasUnsavedChanges;

    // §6 ─ 생성자 ─────────────────────────────────────────────
    public ConfigAppMainViewModel(
        DeviceTreeViewModel    deviceTree,
        JsonConfigLoader       configLoader,
        JsonWriteService       writeService,
        ScaleLibraryViewModel  scaleLibrary,
        AlarmLibraryViewModel  alarmLibrary,
        CommLibraryViewModel   commLibrary,
        CanvasViewModel        canvas,
        CollectConfigService   collectService)
    {
        DeviceTree      = deviceTree;
        _configLoader   = configLoader;
        _writeService   = writeService;
        ScaleLibrary    = scaleLibrary;
        AlarmLibrary    = alarmLibrary;
        CommLibrary     = commLibrary;
        Canvas          = canvas;
        _collectService = collectService;

        // 트리 변경 → 미저장 표시
        DeviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
                HasUnsavedChanges = true;
        };

        // 캔버스 변경 → 미저장 표시
        Canvas.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.HasUnsavedChanges)
                && Canvas.HasUnsavedChanges)
                HasUnsavedChanges = true;
        };
    }

    // §7 ─ 탭 전환 커맨드 ─────────────────────────────────────

    [RelayCommand]
    private void SwitchTab(int tabIndex)
    {
        ActiveTabIndex = tabIndex;

        // 탭 전환 시 해당 라이브러리 데이터 로드
        var bundle = _configLoader.LoadAll();
        switch (tabIndex)
        {
            case 2: ScaleLibrary.Load(bundle.Scales);     break;
            case 3: AlarmLibrary.Load(bundle.AlarmRules); break;
            case 4: CommLibrary.Load(bundle.CommConfigs); break;
            case 1:
                // 수집 흐름 탭 — collect.json 로드
                var raw = _collectService.LoadRaw();
                if (raw is not null)
                    Canvas.DeserializeFromJson(raw);
                break;
        }
    }

    // §8 ─ 저장 커맨드 ────────────────────────────────────────

    /// <summary>
    /// 현재 활성 탭 기준 저장.
    /// 장비 탭 → device.json, 캔버스 탭 → collect.json
    /// </summary>
    [RelayCommand(CanExecute = nameof(_CanSave))]
    private async Task SaveAll()
    {
        if (IsBusy) return;
        IsBusy     = true;
        SaveStatus = "저장 중...";

        try
        {
            await Task.Run(() =>
            {
                // device.json 저장 (장비 트리)
                var tree = DeviceTreeSerializer.Serialize(DeviceTree.RootNodes);
                _writeService.SaveDeviceTree(tree, "configapp-save");

                // collect.json 저장 (캔버스)
                if (Canvas.Nodes.Count > 0)
                    _collectService.SaveCanvas(Canvas);

                // 라이브러리 저장
                var bundle = _configLoader.LoadAll();
                if (bundle.Scales.Count > 0)
                    _writeService.SaveScaleLibrary(bundle.Scales);
                if (bundle.AlarmRules.Count > 0)
                    _writeService.SaveAlarmLibrary(bundle.AlarmRules);
                if (bundle.CommConfigs.Count > 0)
                    _writeService.SaveCommLibrary(bundle.CommConfigs);
            });

            Canvas.HasUnsavedChanges = false;
            HasUnsavedChanges        = false;
            SaveStatus               = $"저장 완료 ({DateTime.Now:HH:mm:ss})";
            LogManager.Instance.Info(LogSrc, "전체 저장 완료 (device + collect + 라이브러리)");
        }
        catch (Exception ex)
        {
            SaveStatus = $"저장 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"저장 오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool _CanSave() => !IsBusy;

    /// <summary>캔버스만 저장 (collect.json)</summary>
    [RelayCommand]
    private async Task SaveCanvas()
    {
        if (IsBusy) return;
        IsBusy     = true;
        SaveStatus = "수집 흐름 저장 중...";
        try
        {
            await Task.Run(() => _collectService.SaveCanvas(Canvas));
            Canvas.HasUnsavedChanges = false;
            SaveStatus = $"collect.json 저장 완료 ({DateTime.Now:HH:mm:ss})";
            LogManager.Instance.Info(LogSrc,
                $"collect.json 저장 — {Canvas.Nodes.Count}개 노드");
        }
        catch (Exception ex)
        {
            SaveStatus = $"저장 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"collect.json 저장 오류: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    /// <summary>device.json 로드 (앱 시작 또는 새로고침)</summary>
    [RelayCommand]
    private async Task LoadAll()
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
                    foreach (var vm in DeviceTreeSerializer.Deserialize(tree))
                        DeviceTree.RootNodes.Add(vm);
                });

                // collect.json 로드
                var raw = _collectService.LoadRaw();
                if (raw is not null)
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        Canvas.DeserializeFromJson(raw));

                SaveStatus = ok
                    ? $"로드 완료 — 장비 {DeviceTree.TotalNodeCount}개, 노드 {Canvas.Nodes.Count}개"
                    : "⚠ 무결성 경고 — 로드 완료";
            });

            HasUnsavedChanges = false;
            LogManager.Instance.Info(LogSrc, SaveStatus);
        }
        catch (Exception ex)
        {
            SaveStatus = $"로드 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, SaveStatus);
        }
        finally { IsBusy = false; }
    }
}
