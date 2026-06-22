// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  S-08: CommLibraryViewModel 주입 추가
//  S-10: DeviceConfigService 주입 + SaveCommand 추가
//  S-11: CanvasViewModel + CollectConfigService 주입
//  S-12B: SwitchTab → 탭1 진입 시 Canvas.RefreshDevicePalette() 호출
//  S-15B: HasUnsavedChanges + CollectionChanged 구독 + 저장 시 리셋
//  S-19A: Ctrl+S → SaveCommand (XAML InputBinding)
//  S-19B: StatusBarPath, TotalTagCount, TotalPlcCount, LastSavedAt 추가
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Config;
using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Collections.ObjectModel;

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
        DeviceTree.RootNodes.CollectionChanged += (_, _) =>
        {
            HasUnsavedChanges = true;
            OnPropertyChanged(nameof(TotalTagCount));
            OnPropertyChanged(nameof(TotalPlcCount));
        };
        Canvas.Nodes.CollectionChanged         += (_, _) => HasUnsavedChanges = true;
        Canvas.Connections.CollectionChanged   += (_, _) => HasUnsavedChanges = true;
        ScaleLibrary.Entries.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        AlarmLibrary.Entries.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        CommLibrary.Entries.CollectionChanged  += (_, _) => HasUnsavedChanges = true;

        // ★ S-19B: 트리 선택 변경 → StatusBarPath 갱신
        DeviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
            {
                OnPropertyChanged(nameof(StatusBarPath));
                OnPropertyChanged(nameof(TotalTagCount));
                OnPropertyChanged(nameof(TotalPlcCount));
            }
        };
    }

    // §3 ─ 저장 상태 ──────────────────────────────────────────

    [ObservableProperty]
    private string _saveStatus = "준비됨";

    // ★ S-15B: 미저장 여부
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    // ★ S-19B: 마지막 저장 시각
    [ObservableProperty]
    private string _lastSavedAt = string.Empty;

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

    // §6 ─ S-19B 상태바 프로퍼티 ────────────────────────────

    /// <summary>
    /// 선택된 노드의 계층 경로.
    /// 예: "공장1 > PLC-01 > 온도Tag"
    /// </summary>
    public string StatusBarPath
    {
        get
        {
            var node = DeviceTree.SelectedNode;
            if (node is null) return "노드 미선택";
            return _BuildPath(node, DeviceTree.RootNodes);
        }
    }

    /// <summary>전체 Tag 수 (재귀 카운트)</summary>
    public int TotalTagCount => _CountAll<TagTreeNode>(DeviceTree.RootNodes);

    /// <summary>전체 PLC 수 (재귀 카운트)</summary>
    public int TotalPlcCount => _CountAll<PlcTreeNode>(DeviceTree.RootNodes);

    // §7 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private void SwitchTab(string tabParam)
    {
        if (!int.TryParse(tabParam, out var idx)) return;
        ActiveTabIndex = idx;

        // ★ S-12B: 수집 흐름 탭 진입 시 장비 팔레트 강제 갱신
        if (idx == 1)
            Canvas.RefreshDevicePalette();
    }

    // §8 ─ 저장 커맨드 ────────────────────────────────────────

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
            HasUnsavedChanges = false;
            // ★ S-19B: 마지막 저장 시각 갱신
            LastSavedAt = DateTime.Now.ToString("HH:mm:ss");
            SaveStatus  = $"✔ 저장 완료  ({LastSavedAt})";
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

    // §9 ─ 내부 헬퍼 ──────────────────────────────────────────

    /// <summary>노드 계층 경로 재귀 빌드</summary>
    private static string _BuildPath(
        AbstractTreeNode target,
        IEnumerable<AbstractTreeNode> nodes,
        string prefix = "")
    {
        foreach (var node in nodes)
        {
            var current = string.IsNullOrEmpty(prefix)
                ? node.Name
                : $"{prefix} > {node.Name}";

            if (ReferenceEquals(node, target)) return current;

            var found = _BuildPath(target, node.Children, current);
            if (!string.IsNullOrEmpty(found)) return found;
        }
        return string.Empty;
    }

    /// <summary>타입 T 노드 재귀 카운트</summary>
    private static int _CountAll<T>(IEnumerable<AbstractTreeNode> nodes)
        where T : AbstractTreeNode
    {
        int count = 0;
        foreach (var node in nodes)
        {
            if (node is T) count++;
            count += _CountAll<T>(node.Children);
        }
        return count;
    }
}
