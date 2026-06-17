// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  S-08: CommLibraryViewModel 주입 추가
//  S-10: DeviceConfigService 주입 + SaveCommand 추가
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Config;
using IIoT.Studio.ViewModels;

namespace IIoT.Studio;

public partial class MainViewModel : ObservableObject
{
    // §1 ─ 서브 ViewModel ─────────────────────────────────────

    public DeviceTreeViewModel DeviceTree { get; }
    public ScaleLibraryViewModel ScaleLibrary { get; }
    public AlarmLibraryViewModel AlarmLibrary { get; }
    public CommLibraryViewModel CommLibrary { get; }

    // §1-1 ─ 서비스 ───────────────────────────────────────────

    private readonly DeviceConfigService _configSvc;  // ★ S-10

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public MainViewModel(
        DeviceTreeViewModel deviceTree,
        ScaleLibraryViewModel scaleLibrary,
        AlarmLibraryViewModel alarmLibrary,
        CommLibraryViewModel commLibrary,
        DeviceConfigService configSvc)       // ★ S-10
    {
        DeviceTree = deviceTree;
        ScaleLibrary = scaleLibrary;
        AlarmLibrary = alarmLibrary;
        CommLibrary = commLibrary;
        _configSvc = configSvc;
    }

    // §3 ─ 저장 상태 ──────────────────────────────────────────

    [ObservableProperty]
    private string _saveStatus = "준비됨";

    // §4 ─ 탭 전환 ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceTab))]
    [NotifyPropertyChangedFor(nameof(IsCanvasTab))]
    [NotifyPropertyChangedFor(nameof(IsScaleTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsCommTab))]
    private int _activeTabIndex;

    // §5 ─ 탭 가시성 ─────────────────────────────────────────

    public bool IsDeviceTab => ActiveTabIndex == 0;
    public bool IsCanvasTab => ActiveTabIndex == 1;
    public bool IsScaleTab => ActiveTabIndex == 2;
    public bool IsAlarmTab => ActiveTabIndex == 3;
    public bool IsCommTab => ActiveTabIndex == 4;

    // §6 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private void SwitchTab(string tabParam)
    {
        if (int.TryParse(tabParam, out var idx))
            ActiveTabIndex = idx;
    }

    // §7 ─ 저장 커맨드 (★ S-10) ─────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaveEnabled))]
    private bool _isSaving;

    /// <summary>저장 버튼 활성화 여부 (저장 중에는 비활성)</summary>
    public bool IsSaveEnabled => !_isSaving;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_isSaving) return;

        IsSaving = true;
        SaveStatus = "저장 중…";

        var result = await _configSvc.SaveAsync();

        SaveStatus = result.IsSuccess
            ? $"✔ {result.Message}  ({DateTime.Now:HH:mm:ss})"
            : $"✖ {result.Message}";

        IsSaving = false;

        // 3초 후 "준비됨" 복귀
        await Task.Delay(3000);
        if (!_isSaving)
            SaveStatus = "준비됨";
    }
}