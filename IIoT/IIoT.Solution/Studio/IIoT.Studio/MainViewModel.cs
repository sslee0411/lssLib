// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  S-06: ScaleLibraryViewModel 주입 추가
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.ViewModels;

namespace IIoT.Studio;

public partial class MainViewModel : ObservableObject
{
    // §1 ─ 서브 ViewModel ─────────────────────────────────────

    /// <summary>장비 트리 ViewModel</summary>
    public DeviceTreeViewModel DeviceTree { get; }

    /// <summary>스케일 라이브러리 ViewModel</summary>
    public ScaleLibraryViewModel ScaleLibrary { get; }

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public MainViewModel(
        DeviceTreeViewModel deviceTree,
        ScaleLibraryViewModel scaleLibrary)
    {
        DeviceTree = deviceTree;
        ScaleLibrary = scaleLibrary;
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

    // §5 ─ 탭 가시성 프로퍼티 ────────────────────────────────

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
}