// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · ViewModels/MainViewModel.cs
//  역할: 수집 모니터링 메인 ViewModel
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
    private IDisposable? _tagSub;   // EventBus 구독 해제 핸들
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

    // §4 ─ 선택된 태그 (상세 패널) ─────────────────────────────
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
        // ★ Subscribe 반환값(IDisposable)을 보관 → Dispose 시 해제
        _tagSub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagUpdated);
        LogManager.Instance.LogReceived += _OnLog;
    }

    // §8 ─ 수집 제어 커맨드 ────────────────────────────────────

    [RelayCommand(CanExecute = nameof(_CanStart))]
    private async Task Start()
    {
        _AddLog("수집 시작 요청", "INFO");
        await _engine.StartAsync();
        _startTime = DateTime.Now;
        _StartUptime();
        _RefreshCommands();
    }
    private bool _CanStart() => !_engine.IsRunning;

    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task Stop()
    {
        _AddLog("수집 중지 요청", "INFO");
        await _engine.StopAsync();
        _StopUptime();
        _RefreshCommands();
    }
    private bool _CanStop() => _engine.IsRunning;

    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task Restart()
    {
        _AddLog("수집 재시작 요청", "WARN");
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

    // §10 ─ 내부 ────────────────────────────────────────────────

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

    private void _OnLog(LogEntry e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LogRows.Insert(0, new LogRow(
                e.Time.ToString("HH:mm:ss.fff"),
                e.Level.ToString().ToUpper(),
                e.Category,
                e.Message));
            while (LogRows.Count > MaxLog)
                LogRows.RemoveAt(LogRows.Count - 1);
        });
    }

    private void _AddLog(string msg, string level) =>
        LogRows.Insert(0, new LogRow(DateTime.Now.ToString("HH:mm:ss.fff"), level, "UI", msg));

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
        // ★ EventBus 구독 해제: Unsubscribe() 없음 → sub.Dispose() 사용
        _tagSub?.Dispose();
        LogManager.Instance.LogReceived -= _OnLog;
        _StopUptime();
        _engine.Dispose();
        _disposed = true;
    }
}

/// <summary>로그 패널 행 모델</summary>
public record LogRow(string Time, string Level, string Source, string Message);