// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/StudioMainViewModel.cs
//  역할: Studio 통합 MainViewModel (구 ConfigAppMainViewModel)
//  V3 Step3:
//    · ConfigAppMainViewModel(8개 파라미터) → StudioMainViewModel(2개 파라미터)
//    · ConfigBundle 번들 패턴 적용
//    · 내부 로직은 동일 유지 (bundle.Get<T>()로 꺼내 사용)
//
//  탭 구성 (유지):
//    ① 장비 관리  — 트리 + 편집기 + 라이브러리
//    ② 수집 흐름  — NodeRed 스타일 캔버스
//    ③ 스케일 관리
//    ④ 알람 규칙
//    ⑤ 통신 설정
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
    private int _activeTabIndex;   // 0=장비, 1=수집흐름, 2=스케일, 3=알람, 4=통신

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

    /// <summary>
    /// ★ V3 Step3: ConfigBundle 번들 패턴으로 파라미터 8개 → 2개
    /// </summary>
    public StudioMainViewModel(
        DeviceTreeViewModel tree,
        ConfigBundle        bundle)
    {
        DeviceTree = tree;

        // ConfigBundle에서 서비스 꺼내기
        _configLoader   = bundle.Get<JsonConfigLoader>(bundle.Loader);
        _writeService   = bundle.Get<JsonWriteService>(bundle.Writer);
        _collectService = bundle.Get<CollectConfigService>(bundle.Collect);
        ScaleLibrary    = bundle.Get<ScaleLibraryViewModel>(bundle.Scale);
        AlarmLibrary    = bundle.Get<AlarmLibraryViewModel>(bundle.Alarm);
        CommLibrary     = bundle.Get<CommLibraryViewModel>(bundle.Comm);
        Canvas          = bundle.Get<CanvasViewModel>(bundle.Canvas);

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

        LogManager.Instance.Info(LogSrc, "StudioMainViewModel 초기화 완료");
    }

    // §7 ─ 탭 전환 커맨드 ─────────────────────────────────────

    [RelayCommand]
    private void SwitchTab(int tabIndex)
    {
        ActiveTabIndex = tabIndex;

        var bundle = _configLoader.LoadAll();
        switch (tabIndex)
        {
            case 2: ScaleLibrary.Load(bundle.Scales);     break;
            case 3: AlarmLibrary.Load(bundle.AlarmRules); break;
            case 4: CommLibrary.Load(bundle.CommConfigs); break;
            case 1:
                var raw = _collectService.LoadRaw();
                if (raw is not null)
                    Canvas.DeserializeFromJson(raw);
                break;
        }
    }

    // §8 ─ 저장 커맨드 ────────────────────────────────────────

    [RelayCommand]
    private async Task SaveCurrentTabAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            SaveStatus = "저장 중...";
            switch (ActiveTabIndex)
            {
                case 0: await DeviceTree.SaveAsync(_writeService);    break;
                case 1: await Canvas.SaveAsync(_collectService);      break;
                case 2: await ScaleLibrary.SaveAsync(_writeService);  break;
                case 3: await AlarmLibrary.SaveAsync(_writeService);  break;
                case 4: await CommLibrary.SaveAsync(_writeService);   break;
            }
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

    [RelayCommand]
    private async Task LoadDeviceTreeAsync()
    {
        IsBusy = true;
        try
        {
            await DeviceTree.LoadAsync(_configLoader);
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
