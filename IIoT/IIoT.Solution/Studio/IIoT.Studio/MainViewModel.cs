// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  S-08: CommLibraryViewModel 주입 추가
//  S-10: DeviceConfigService 주입 + SaveCommand 추가
//  S-11: CanvasViewModel + CollectConfigService 주입
//  S-12B: SwitchTab → 탭1 진입 시 Canvas.RefreshDevicePalette() 호출
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

    public DeviceTreeViewModel   DeviceTree   { get; }
    public ScaleLibraryViewModel ScaleLibrary { get; }
    public AlarmLibraryViewModel AlarmLibrary { get; }
    public CommLibraryViewModel  CommLibrary  { get; }
    public CanvasViewModel       Canvas       { get; }

    // §1-1 ─ 서비스 ───────────────────────────────────────────

    private readonly DeviceConfigService  _deviceSvc;
    private readonly CollectConfigService _collectSvc;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public MainViewModel(
        DeviceTreeViewModel   deviceTree,
        ScaleLibraryViewModel scaleLibrary,
        AlarmLibraryViewModel alarmLibrary,
        CommLibraryViewModel  commLibrary,
        CanvasViewModel       canvas,
        DeviceConfigService   deviceSvc,
        CollectConfigService  collectSvc)
    {
        DeviceTree   = deviceTree;
        ScaleLibrary = scaleLibrary;
        AlarmLibrary = alarmLibrary;
        CommLibrary  = commLibrary;
        Canvas       = canvas;
        _deviceSvc   = deviceSvc;
        _collectSvc  = collectSvc;
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
    public bool IsScaleTab  => ActiveTabIndex == 2;
    public bool IsAlarmTab  => ActiveTabIndex == 3;
    public bool IsCommTab   => ActiveTabIndex == 4;

    // §6 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private void SwitchTab(string tabParam)
    {
        if (!int.TryParse(tabParam, out var idx)) return;
        ActiveTabIndex = idx;

        // ★ S-12B: 수집 흐름 탭 진입 시 장비 팔레트 강제 갱신
        // RootNodes.CollectionChanged 재귀 구독 대신
        // 탭 전환 시점에 한 번만 갱신 — 단순하고 안정적
        if (idx == 1)
            Canvas.RefreshDevicePalette();
    }

    // §7 ─ 저장 커맨드 ────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaveEnabled))]
    private bool _isSaving;

    public bool IsSaveEnabled => !_isSaving;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_isSaving) return;

        IsSaving   = true;
        SaveStatus = "저장 중…";

        var deviceResult  = await _deviceSvc.SaveAsync();
        var collectResult = await _collectSvc.SaveAsync();

        if (deviceResult.IsSuccess && collectResult.IsSuccess)
            SaveStatus = $"✔ 저장 완료  ({DateTime.Now:HH:mm:ss})";
        else
        {
            var failed = !deviceResult.IsSuccess
                ? deviceResult.Message
                : collectResult.Message;
            SaveStatus = $"✖ {failed}";
        }

        IsSaving = false;

        await Task.Delay(3000);
        if (!_isSaving) SaveStatus = "준비됨";
    }
}
