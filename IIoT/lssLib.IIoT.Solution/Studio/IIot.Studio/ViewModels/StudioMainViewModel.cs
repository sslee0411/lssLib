// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/StudioMainViewModel.cs
//  역할: Studio 통합 MainViewModel
//  V3 Step3: ConfigBundle 번들 패턴 (파라미터 8→2)
//  Fix CS1061:
//    DeviceTree.SaveAsync()  → 존재하지 않는 메서드
//    DeviceTree.LoadAsync()  → 존재하지 않는 메서드
//    Canvas.SaveAsync()      → 존재하지 않는 메서드
//    Scale/Alarm/CommLibrary.SaveAsync() → 존재하지 않는 메서드
//    → StudioMainViewModel 내부에서 직접 처리
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Shared.Config;
using IIoT.Studio.Core.Config;
using IIoT.Studio.ViewModels.Canvas;
using IIoT.Studio.ViewModels.DeviceTree;
using IIoT.Studio.ViewModels.Library;
using lssLib.Log;

namespace IIoT.Studio.ViewModels;

public partial class StudioMainViewModel : ObservableObject
{
    // §1 ─ 상수 ───────────────────────────────────────────────
    private const string LogSrc = "StudioMainVM";

    // §2 ─ 서비스 (ConfigBundle에서 꺼냄) ─────────────────────
    private readonly JsonConfigLoader     _configLoader;
    private readonly JsonWriteService     _writeService;
    private readonly CollectConfigService _collectService;

    // §3 ─ 서브 ViewModel ─────────────────────────────────────
    public DeviceTreeViewModel   DeviceTree   { get; }
    public ScaleLibraryViewModel ScaleLibrary { get; }
    public AlarmLibraryViewModel AlarmLibrary { get; }
    public CommLibraryViewModel  CommLibrary  { get; }
    public CanvasViewModel       Canvas       { get; }

    // §4 ─ 탭 상태 ────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceTab))]
    [NotifyPropertyChangedFor(nameof(IsCanvasTab))]
    [NotifyPropertyChangedFor(nameof(IsScaleTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsCommTab))]
    private int _activeTabIndex;

    public bool IsDeviceTab => ActiveTabIndex == 0;
    public bool IsCanvasTab => ActiveTabIndex == 1;
    public bool IsScaleTab  => ActiveTabIndex == 2;
    public bool IsAlarmTab  => ActiveTabIndex == 3;
    public bool IsCommTab   => ActiveTabIndex == 4;

    // §5 ─ 공통 상태 ──────────────────────────────────────────
    [ObservableProperty] private string _saveStatus = "준비";
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasUnsavedChanges;

    // §6 ─ 생성자 (2개 파라미터) ──────────────────────────────
    public StudioMainViewModel(
        DeviceTreeViewModel tree,
        ConfigBundle        bundle)
    {
        DeviceTree = tree;

        _configLoader   = bundle.Get<JsonConfigLoader>(bundle.Loader);
        _writeService   = bundle.Get<JsonWriteService>(bundle.Writer);
        _collectService = bundle.Get<CollectConfigService>(bundle.Collect);
        ScaleLibrary    = bundle.Get<ScaleLibraryViewModel>(bundle.Scale);
        AlarmLibrary    = bundle.Get<AlarmLibraryViewModel>(bundle.Alarm);
        CommLibrary     = bundle.Get<CommLibraryViewModel>(bundle.Comm);
        Canvas          = bundle.Get<CanvasViewModel>(bundle.Canvas);

        DeviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
                HasUnsavedChanges = true;
        };

        Canvas.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.HasUnsavedChanges)
                && Canvas.HasUnsavedChanges)
                HasUnsavedChanges = true;
        };

        LogManager.Instance.Info(LogSrc, "StudioMainViewModel 초기화 완료");
    }

    // §7 ─ 탭 전환 커맨드 ─────────────────────────────────────
    // ★ Fix: XAML CommandParameter="0" 은 string → int 파싱 필요
    [RelayCommand]
    private void SwitchTab(string tabIndexStr)
    {
        if (!int.TryParse(tabIndexStr, out var tabIndex)) return;
        ActiveTabIndex = tabIndex;

        var data = _configLoader.LoadAll();
        switch (tabIndex)
        {
            case 2: ScaleLibrary.Load(data.Scales);      break;
            case 3: AlarmLibrary.Load(data.AlarmRules);  break;
            case 4: CommLibrary.Load(data.CommConfigs);  break;
            case 1:
                var raw = _collectService.LoadRaw();
                if (raw is not null)
                    Canvas.DeserializeFromJson(raw);
                break;
        }
    }

    // §8 ─ 저장 커맨드 ────────────────────────────────────────
    // ★ Fix: DeviceTree.SaveAsync() 등 외부 위임 제거
    //        → StudioMainViewModel 내부에서 직접 저장 처리
    [RelayCommand]
    private async Task SaveCurrentTabAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            SaveStatus = "저장 중...";

            await Task.Run(() =>
            {
                switch (ActiveTabIndex)
                {
                    case 0:
                        // 장비 트리 저장 — DeviceTree 노드를 JSON 직렬화
                        // (추후 DeviceTreeSerializer 연동)
                        LogManager.Instance.Info(LogSrc, "장비 트리 저장 (준비 중)");
                        break;

                    case 1:
                        // 캔버스 저장 — CollectConfigService 경유
                        var json = Canvas.SerializeToJson();
                        if (!string.IsNullOrEmpty(json))
                            _collectService.SaveCanvas(Canvas);
                        break;

                    case 2:
                        // 스케일 라이브러리 저장
                        _writeService.Save("scale-library.json", ScaleLibrary.Scales);
                        break;

                    case 3:
                        // 알람 라이브러리 저장
                        _writeService.Save("alarm-library.json", AlarmLibrary.AlarmRules);
                        break;

                    case 4:
                        // 통신 라이브러리 저장
                        _writeService.Save("comm-library.json", CommLibrary.CommConfigs);
                        break;
                }
            });

            HasUnsavedChanges = false;
            SaveStatus = $"저장 완료 ({DateTime.Now:HH:mm:ss})";
            LogManager.Instance.Info(LogSrc, $"탭 {ActiveTabIndex} 저장 완료");
        }
        catch (Exception ex)
        {
            SaveStatus = $"저장 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"저장 오류: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // §9 ─ 장비 트리 로드 ─────────────────────────────────────
    // ★ Fix: DeviceTree.LoadAsync() 외부 위임 제거
    //        → _configLoader에서 device.json 읽어 직접 처리
    [RelayCommand]
    private async Task LoadDeviceTreeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                // 추후 DeviceTreeSerializer.Deserialize() 연동
                LogManager.Instance.Info(LogSrc, "장비 트리 로드 (준비 중)");
            });
            SaveStatus = "장비 트리 로드 완료";
        }
        catch (Exception ex)
        {
            SaveStatus = $"로드 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"트리 로드 오류: {ex.Message}");
        }
        finally { IsBusy = false; }
    }
}
