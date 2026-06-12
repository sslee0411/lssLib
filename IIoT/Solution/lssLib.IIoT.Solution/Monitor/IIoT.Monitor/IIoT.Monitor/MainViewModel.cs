// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/MainViewModel.cs
//  역할: 모니터 메인 ViewModel
//        MonitorEngine 프록시 + 알람 탭 + 태그 실시간 현황 + 로그
//  Phase 10: 신규
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core;
using lssLib.Log;
using lssLib.Messaging;
using System.Collections.ObjectModel;

namespace IIoT.Monitor.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private const string LogSrc = "MainViewModel";
    private readonly MonitorEngine _engine;
    private IDisposable? _tagSub;
    private IDisposable? _alarmSub;
    private bool _disposed;

    // §2 ─ 엔진 프록시 ────────────────────────────────────────
    public MonitorEngine Engine => _engine;

    // §3 ─ 실시간 태그 현황 ────────────────────────────────────

    /// <summary>수신된 태그 최신값 목록 (TagId → LiveTagRow)</summary>
    public ObservableCollection<LiveTagRow> LiveTags { get; } = [];

    /// <summary>검색 필터 적용된 태그 목록 (UI 바인딩)</summary>
    public IEnumerable<LiveTagRow> FilteredTags =>
        string.IsNullOrWhiteSpace(TagSearchText)
            ? LiveTags
            : LiveTags.Where(t =>
                t.TagId.Contains(TagSearchText,   StringComparison.OrdinalIgnoreCase) ||
                t.TagName.Contains(TagSearchText, StringComparison.OrdinalIgnoreCase));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredTags))]
    [NotifyPropertyChangedFor(nameof(FilteredTagCount))]
    private string _tagSearchText = string.Empty;

    public int FilteredTagCount => FilteredTags.Count();

    // §4 ─ 알람 탭 ────────────────────────────────────────────
    public ObservableCollection<AlarmRecord> ActiveAlarms
        => _engine.AlarmManager.ActiveAlarms;

    public ObservableCollection<AlarmRecord> AlarmHistory
        => _engine.AlarmManager.AlarmHistory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAlarm))]
    private AlarmRecord? _selectedAlarm;

    public bool HasSelectedAlarm => SelectedAlarm is not null;

    // §5 ─ 통계 ────────────────────────────────────────────────
    [ObservableProperty] private int    _goodCount;
    [ObservableProperty] private int    _badCount;
    [ObservableProperty] private int    _activeAlarmCount;

    // §6 ─ 로그 ────────────────────────────────────────────────
    public ObservableCollection<MonitorLogRow> LogRows { get; } = [];
    private const int MaxLog = 500;

    // §7 ─ 생성자 ─────────────────────────────────────────────
    public MainViewModel(MonitorEngine engine)
    {
        _engine = engine;

        // TagValue 수신 → LiveTags 갱신
        _tagSub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagUpdated);

        // 알람 발생 수신 → 통계 갱신
        _alarmSub = EventBus.Instance.Subscribe<AlarmFiredEvent>(_OnAlarmFired);

        // LogAdded 구독
        LogManager.Instance.LogAdded += _OnLogAdded;

        ActiveAlarms.CollectionChanged  += (_, _) => _RefreshStats();
        AlarmHistory.CollectionChanged  += (_, _) => _RefreshStats();
    }

    // §8 ─ 엔진 제어 커맨드 ───────────────────────────────────

    [RelayCommand(CanExecute = nameof(_CanStart))]
    private async Task Start()
    {
        _AddLog("모니터 시작 요청", "INFO");
        await _engine.StartAsync();
        _RefreshCommands();
    }
    private bool _CanStart() => !_engine.IsRunning;

    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task Stop()
    {
        _AddLog("모니터 중지 요청", "INFO");
        await _engine.StopAsync();
        _RefreshCommands();
    }
    private bool _CanStop() => _engine.IsRunning;

    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task Restart()
    {
        _AddLog("모니터 재시작", "WARN");
        await _engine.RestartAsync();
        _RefreshCommands();
    }

    // §9 ─ 알람 ACK 커맨드 ────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelectedAlarm))]
    private async Task AckAlarm()
    {
        if (SelectedAlarm is null) return;
        await _engine.AlarmManager.AckAsync(SelectedAlarm.AlarmId);
        _AddLog($"알람 ACK: {SelectedAlarm.DetectorId}", "INFO");
    }

    [RelayCommand]
    private async Task AckAll()
    {
        await _engine.AlarmManager.AckAllAsync();
        _AddLog("전체 알람 ACK", "WARN");
    }

    // §10 ─ 필터 ──────────────────────────────────────────────
    partial void OnTagSearchTextChanged(string _)
    {
        OnPropertyChanged(nameof(FilteredTags));
        OnPropertyChanged(nameof(FilteredTagCount));
    }

    [RelayCommand]
    private void ClearTagFilter()
    {
        TagSearchText = string.Empty;
    }

    [RelayCommand]
    private void ClearLog() => LogRows.Clear();

    // §11 ─ 내부 처리 ─────────────────────────────────────────private void _OnTagUpdated(TagValueUpdatedEvent e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var existing = LiveTags.FirstOrDefault(t => t.TagId == e.TagId);
            if (existing is not null)
            {
                existing.Update(e.Value, e.Quality);
            }
            else
            {
                LiveTags.Add(new LiveTagRow
                {
                    TagId   = e.TagId,
                    TagName = e.TagId,  // 실제 TagName은 device.json 로드 시 갱신
                    Value   = e.Value,
                    Quality = e.Quality,
                });
            }
            _RefreshStats();
            OnPropertyChanged(nameof(FilteredTags));
            OnPropertyChanged(nameof(FilteredTagCount));
        });
    }

    private void _OnAlarmFired(AlarmFiredEvent e)
    {
        _AddLog($"알람 발생: [{e.Alarm.Level}] {e.Alarm.Message}", "WARN");
    }

    private void _RefreshStats()
    {
        GoodCount        = LiveTags.Count(t => t.Quality == TagQuality.Good);
        BadCount         = LiveTags.Count(t => t.Quality == TagQuality.Bad);
        ActiveAlarmCount = ActiveAlarms.Count;
    }

    private void _RefreshCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
    }

    private void _OnLogAdded(LogData data)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LogRows.Insert(0, new MonitorLogRow(
                data.Time, data.LevelText, data.Source, data.Contents));
            while (LogRows.Count > MaxLog)
                LogRows.RemoveAt(LogRows.Count - 1);
        });
    }

    private void _AddLog(string msg, string level)
        => LogRows.Insert(0, new MonitorLogRow(
            DateTime.Now.ToString("HH:mm:ss.fff"), level, "UI", msg));

    // §12 ─ IDisposable ────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _tagSub?.Dispose();
        _alarmSub?.Dispose();
        LogManager.Instance.LogAdded -= _OnLogAdded;
        _disposed = true;
    }
}

// ── 보조 모델 ─────────────────────────────────────────────

/// <summary>실시간 태그 현황 행 (ObservableObject)</summary>
public partial class LiveTagRow : ObservableObject
{
    public string TagId   { get; init; } = string.Empty;
    [ObservableProperty] private string     _tagName = string.Empty;
    [ObservableProperty] private double     _value;
    [ObservableProperty] private TagQuality _quality = TagQuality.NotCollecting;
    [ObservableProperty] private DateTime   _lastUpdated;

    public string ValueText  => Quality == TagQuality.Bad ? "---" : $"{Value:F3}";
    public string QualityKey => Quality switch
    {
        TagQuality.Good => "GreenBrush",
        TagQuality.Bad  => "RedBrush",
        _               => "Text3Brush",
    };

    public void Update(double value, TagQuality quality)
    {
        Value       = value;
        Quality     = quality;
        LastUpdated = DateTime.Now;
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(QualityKey));
    }
}

/// <summary>로그 패널 행</summary>
public record MonitorLogRow(string Time, string Level, string Source, string Msg);
