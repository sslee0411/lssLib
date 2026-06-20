// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  S-08: CommLibraryViewModel 주입 추가
//  S-10: DeviceConfigService 주입 + SaveCommand 추가
//  S-11: CanvasViewModel + CollectConfigService 주입
//  S-12B: SwitchTab → 탭1 진입 시 Canvas.RefreshDevicePalette() 호출
//  S-15B: HasUnsavedChanges 추가
//         RootNodes/Canvas.Nodes/Library.Entries CollectionChanged 구독
//         저장 완료 시 false 리셋
//  S-19A: Ctrl+S → SaveCommand (XAML InputBinding으로 처리)
//  생성: 2026-06-15 / 수정: 2026-06-20
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

        // ★ S-15B: 변경 감지 구독
        //   CollectionChanged: 노드 추가·삭제 시
        //   [ObservableProperty] 필드 변경은 각 편집기에서 직접 HasUnsavedChanges=true
        DeviceTree.RootNodes.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        Canvas.Nodes.CollectionChanged         += (_, _) => HasUnsavedChanges = true;
        Canvas.Connections.CollectionChanged   += (_, _) => HasUnsavedChanges = true;
        ScaleLibrary.Entries.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        AlarmLibrary.Entries.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        CommLibrary.Entries.CollectionChanged  += (_, _) => HasUnsavedChanges = true;
    }

    // §3 ─ 저장 상태 ──────────────────────────────────────────

    [ObservableProperty]
    private string _saveStatus = "준비됨";

    // ★ S-15B: 미저장 여부 플래그
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnsavedBadgeText))]
    private bool _hasUnsavedChanges;

    /// <summary>헤더 배지 텍스트 — HasUnsavedChanges=true 시 "● 미저장"</summary>
    public string UnsavedBadgeText => HasUnsavedChanges ? "● 미저장" : string.Empty;

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
        {
            // ★ S-15B: 저장 성공 시 미저장 플래그 초기화
            HasUnsavedChanges = false;
            SaveStatus = $"✔ 저장 완료  ({DateTime.Now:HH:mm:ss})";
        }
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
