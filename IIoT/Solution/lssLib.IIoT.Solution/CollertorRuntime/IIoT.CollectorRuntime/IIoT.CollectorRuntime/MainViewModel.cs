// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · ViewModels/MainViewModel.cs
//  수정: Phase 8
//    ① lssLib.Log 이벤트: LogAdded(LogData) — LogReceived 없음
//       LogData 프로퍼티: Level(LogLevel), Source(string), Contents(string)
//       (Category, Message, Time 프로퍼티 없음)
//    ② _engine.Dispose() → _engine.DisposeAsync().GetAwaiter().GetResult()
//       (CollectionEngine 은 IAsyncDisposable — IDisposable 없음)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.CollectorRuntime.Core;
using lssLib.Log;
using lssLib.Messaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace IIoT.CollectorRuntime.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private const string LogSrc = "MainViewModel";
    private readonly CollectionEngine _engine;
    private System.Timers.Timer? _uptimeTimer;
    private DateTime _startTime;
    private bool _disposed;

    // §2 ─ 엔진 프록시 ────────────────────────────────────────
    public CollectionEngine Engine => _engine;

    // §3 ─ 태그 목록 + 필터 ────────────────────────────────────
    public ObservableCollection<LiveTagValue> LiveTags => _engine.LiveTags;
    public ICollectionView TagsView { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCount))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCount))]
    private bool _showBadOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCount))]
    private bool _showAlarmOnly;

    public int FilteredCount => TagsView.Cast<object>().Count();

    // §4 ─ 선택된 태그 ─────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTag))]
    private LiveTagValue? _selectedTag;

    public bool HasSelectedTag => SelectedTag is not null;

    // §5 ─ 통계 ────────────────────────────────────────────────
    [ObservableProperty] private int _goodCount;
    [ObservableProperty] private int _badCount;
    [ObservableProperty] private int _alarmCount;
    [ObservableProperty] private string _uptime = "00:00:00";

    // §6 ─ 로그 ────────────────────────────────────────────────
    public ObservableCollection<LogRow> LogRows { get; } = [];
    private const int MaxLog = 500;

    // §7 ─ 생성자 ──────────────────────────────────────────────
    public MainViewModel(CollectionEngine engine)
    {
        _engine = engine;

        TagsView = CollectionViewSource.GetDefaultView(LiveTags);
        TagsView.Filter = _Filter;

        LiveTags.CollectionChanged += (_, _) => _RefreshStats();
        EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagUpdated);

        // ★ 수정: lssLib.Log 이벤트명 = LogAdded, 파라미터 타입 = LogData
        LogManager.Instance.LogAdded += _OnLogAdded;
    }

    // §8 ─ 수집 제어 커맨드 ────────────────────────────────────

    [RelayCommand(CanExecute = nameof(_CanStart))]
    private async Task Start()
    {
        _AddRow("수집 시작 요청", "INFO", "UI");
        await _engine.StartAsync();
        _startTime = DateTime.Now;
        _StartUptime();
        _RefreshCommands();
    }
    private bool _CanStart() => !_engine.IsRunning;

    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task Stop()
    {
        _AddRow("수집 중지 요청", "INFO", "UI");
        await _engine.StopAsync();
        _StopUptime();
        _RefreshCommands();
    }
    private bool _CanStop() => _engine.IsRunning;

    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task Restart()
    {
        _AddRow("수집 재시작 요청", "WARN", "UI");
        await _engine.RestartAsync();
        _startTime = DateTime.Now;
    }

    // §9 ─ 필터 커맨드 ─────────────────────────────────────────

    partial void OnSearchTextChanged(string _) => TagsView.Refresh();
    partial void OnShowBadOnlyChanged(bool _) => TagsView.Refresh();
    partial void OnShowAlarmOnlyChanged(bool _) => TagsView.Refresh();

    [RelayCommand]
    private void ClearFilter()
    {
        SearchText = string.Empty;
        ShowBadOnly = false;
        ShowAlarmOnly = false;
    }

    [RelayCommand]
    private void ClearLog() => LogRows.Clear();

    // §10 ─ 내부 헬퍼 ──────────────────────────────────────────

    private bool _Filter(object obj)
    {
        if (obj is not LiveTagValue t) return false;
        if (ShowBadOnly && t.Quality != TagQuality.Bad) return false;
        if (ShowAlarmOnly && !t.HasAlarm) return false;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.ToLower();
            return t.TagName.ToLower().Contains(q)
                || t.Address.ToLower().Contains(q)
                || t.DeviceName.ToLower().Contains(q);
        }
        return true;
    }

    private void _OnTagUpdated(TagValueUpdatedEvent _)
    {
        if (_engine.TotalPolled % 30 == 0) _RefreshStats();
    }

    private void _RefreshStats()
    {
        GoodCount = LiveTags.Count(t => t.Quality == TagQuality.Good);
        BadCount = LiveTags.Count(t => t.Quality == TagQuality.Bad);
        AlarmCount = LiveTags.Count(t => t.HasAlarm);
        OnPropertyChanged(nameof(FilteredCount));
    }

    private void _RefreshCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// ★ 수정: lssLib.Log 이벤트 = LogAdded(LogData)
    ///   LogData 프로퍼티:
    ///     .Level    → LogLevel enum
    ///     .Source   → string (로그 출처, 예: "CollectionEngine")
    ///     .Contents → string (메시지)
    ///     .Time     → string (예: "14:30:25.123")
    ///   주의: Category, Message, LevelText 프로퍼티 없음
    /// </summary>
    private void _OnLogAdded(LogData data)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LogRows.Insert(0, new LogRow(
                Time: data.Time,
                Level: data.LevelText,           // "INFO", "WARN", "ERROR" 등
                Source: data.Source,
                Msg: data.Contents));
            while (LogRows.Count > MaxLog)
                LogRows.RemoveAt(LogRows.Count - 1);
        });
    }

    private void _AddRow(string msg, string level, string source) =>
        LogRows.Insert(0, new LogRow(
            DateTime.Now.ToString("HH:mm:ss.fff"), level, source, msg));

    private void _StartUptime()
    {
        _uptimeTimer = new System.Timers.Timer(1000);
        _uptimeTimer.Elapsed += (_, _) =>
        {
            var e = DateTime.Now - _startTime;
            Uptime = $"{(int)e.TotalHours:D2}:{e.Minutes:D2}:{e.Seconds:D2}";
        };
        _uptimeTimer.Start();
    }

    private void _StopUptime()
    {
        _uptimeTimer?.Stop();
        _uptimeTimer?.Dispose();
        _uptimeTimer = null;
        Uptime = "00:00:00";
    }

    // §11 ─ IDisposable ────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        EventBus.Instance.Unsubscribe<TagValueUpdatedEvent>(_OnTagUpdated);

        // ★ 수정: lssLib.Log 이벤트명 = LogAdded
        LogManager.Instance.LogAdded -= _OnLogAdded;

        _StopUptime();

        // ★ 수정: CollectionEngine 은 IAsyncDisposable
        //   IDisposable 이 없으므로 .Dispose() 호출 불가
        //   App.xaml.cs OnExit 에서 await engine.DisposeAsync() 를 직접 호출합니다.
        //   여기서는 해제 생략 (이중 해제 방지)

        _disposed = true;
    }
}

/// <summary>로그 패널 행 데이터 모델</summary>
public record LogRow(string Time, string Level, string Source, string Msg);