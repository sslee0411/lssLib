// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/ScheduleViewModel.cs
//  역할: [스케줄] 탭 ViewModel — 스케줄 목록 CRUD (요구사항 4-2-8)
//        추가 폼(프로그램·동작·시각·요일) + 목록(활성 토글·삭제)
//        변경 즉시 manager.json 저장
//  MG-07: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core;
using IIoT.Manager.Core.Config;
using IIoT.Manager.Models;
using System.Collections.ObjectModel;
using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)

namespace IIoT.Manager.ViewModels;

/// <summary>추가 폼의 요일 선택 1칸.</summary>
public partial class DaySelectItem : ObservableObject
{
    /// <summary>표시 이름 (일~토)</summary>
    public string Label { get; }

    /// <summary>(int)DayOfWeek 값 (0=일 … 6=토)</summary>
    public int Day { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public DaySelectItem(string label, int day)
    {
        Label = label;
        Day   = day;
    }
}

/// <summary>스케줄 목록 1행 (활성 토글 + 요약 표시).</summary>
public partial class ScheduleItemVm : ObservableObject
{
    private readonly Func<Task> _onChanged;

    /// <summary>원본 엔트리 (manager.json 직렬화 대상)</summary>
    public ScheduleEntry Entry { get; }

    /// <summary>요약 문구 (예: "매일 06:00 · IIoT.Collector · 재시작")</summary>
    public string SummaryText { get; }

    /// <summary>활성 여부 — 토글 시 즉시 저장</summary>
    public bool Enabled
    {
        get => Entry.Enabled;
        set
        {
            if (Entry.Enabled == value) return;
            Entry.Enabled = value;
            OnPropertyChanged();
            _ = _onChanged();   // fire-and-forget 저장 (내부 try/catch)
        }
    }

    public ScheduleItemVm(ScheduleEntry entry, string processName, Func<Task> onChanged)
    {
        Entry       = entry;
        _onChanged  = onChanged;
        SummaryText = $"{entry.DaysText} {entry.Time}  ·  {processName}  ·  {entry.ActionText}";
    }
}

/// <summary>
/// 스케줄 관리 ViewModel.
/// <para>실행 자체는 ScheduleService 가 담당 — 여기는 목록 편집·저장만.</para>
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly ManagerSettingsLoader _settingsLoader;
    private readonly EventHistoryService   _events;
    private bool                           _initialized;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────

    /// <summary>스케줄 목록</summary>
    public ObservableCollection<ScheduleItemVm> Schedules { get; } = [];

    /// <summary>추가 폼 — 대상 프로그램 목록 (manager.json Processes)</summary>
    public ObservableCollection<ManagedProcessInfo> ProcessOptions { get; } = [];

    /// <summary>추가 폼 — 동작 목록 (표시 문구)</summary>
    public ObservableCollection<string> ActionOptions { get; } = ["시작", "정지", "재시작"];

    /// <summary>추가 폼 — 요일 체크 7칸 (일~토)</summary>
    public ObservableCollection<DaySelectItem> DayOptions { get; } =
    [
        new("일", 0), new("월", 1), new("화", 2), new("수", 3),
        new("목", 4), new("금", 5), new("토", 6),
    ];

    // §3 ─ 관찰 속성 (추가 폼) ────────────────────────────────

    /// <summary>추가 폼 — 선택된 프로그램</summary>
    [ObservableProperty]
    private ManagedProcessInfo? _selectedProcess;

    /// <summary>추가 폼 — 선택된 동작 (표시 문구)</summary>
    [ObservableProperty]
    private string _selectedAction = "재시작";

    /// <summary>추가 폼 — 시각 입력 "HH:mm"</summary>
    [ObservableProperty]
    private string _timeText = "06:00";

    /// <summary>상태 문구</summary>
    [ObservableProperty]
    private string _statusText = "";

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public ScheduleViewModel(ManagerSettingsLoader settingsLoader,
                             EventHistoryService   events)
    {
        _settingsLoader = settingsLoader;
        _events         = events;
    }

    // §5 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// manager.json 로드 후 목록·옵션을 구성한다.
    /// ManagerMainViewModel.InitializeAsync 에서 1회 호출 (재호출 무시).
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        ProcessOptions.Clear();
        foreach (var p in _settingsLoader.Settings.Processes)
            ProcessOptions.Add(p);
        SelectedProcess = ProcessOptions.FirstOrDefault();

        _RebuildList();
    }

    // §6 ─ 커맨드 ─────────────────────────────────────────────

    /// <summary>➕ 스케줄 추가 (검증 → 저장 → 목록 갱신)</summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        // ① 검증 — 규칙: 오류는 조용히 삼키지 않고 상태 문구에 노출
        if (SelectedProcess is null)
        {
            StatusText = "대상 프로그램을 선택해 주세요.";
            return;
        }
        if (!TimeSpan.TryParseExact(TimeText, @"hh\:mm", null, out _))
        {
            StatusText = $"시각 형식 오류: \"{TimeText}\" — HH:mm (예: 06:00, 22:30)";
            return;
        }
        var days = DayOptions.Where(d => d.IsSelected).Select(d => d.Day).ToList();
        if (days.Count == 0)
        {
            StatusText = "요일을 1개 이상 선택해 주세요.";
            return;
        }

        // ② 엔트리 생성·추가
        var entry = new ScheduleEntry
        {
            ProcessId = SelectedProcess.Id,
            Action    = SelectedAction switch
            {
                "시작" => ScheduleAction.Start,
                "정지" => ScheduleAction.Stop,
                _      => ScheduleAction.Restart,
            },
            Time = TimeText,
            Days = days,
        };
        _settingsLoader.Settings.Schedules.Add(entry);

        // ③ 저장 + 목록 갱신 + 이벤트 기록
        await _SaveAsync();
        _RebuildList();
        _events.Record(SelectedProcess.Name,
            $"스케줄 등록 — {entry.DaysText} {entry.Time} {entry.ActionText}");
        StatusText = $"추가 완료 ({DateTime.Now:HH:mm:ss})";
    }

    /// <summary>🗑 스케줄 삭제</summary>
    [RelayCommand]
    private async Task DeleteAsync(ScheduleItemVm item)
    {
        _settingsLoader.Settings.Schedules.Remove(item.Entry);
        await _SaveAsync();
        _RebuildList();
        StatusText = $"삭제 완료 ({DateTime.Now:HH:mm:ss})";
    }

    // §7 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>Settings.Schedules → 화면 목록 재구성.</summary>
    private void _RebuildList()
    {
        Schedules.Clear();
        foreach (var s in _settingsLoader.Settings.Schedules)
        {
            var name = _settingsLoader.Settings.Processes
                           .FirstOrDefault(p => p.Id == s.ProcessId)?.Name ?? s.ProcessId;
            Schedules.Add(new ScheduleItemVm(s, name, _SaveAsync));
        }
    }

    /// <summary>manager.json 저장 (규칙: 실패는 로그+상태 문구 노출).</summary>
    private async Task _SaveAsync()
    {
        try
        {
            await _settingsLoader.SaveAsync();
        }
        catch (Exception ex)
        {
            lssLib.Log.LogManager.Instance.Error("Schedule", $"저장 실패: {ex.Message}");
            StatusText = $"저장 실패: {ex.Message}";
        }
    }
}
