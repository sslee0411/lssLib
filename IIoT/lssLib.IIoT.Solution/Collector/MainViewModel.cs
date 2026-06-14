// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/MainViewModel.cs
//  역할: 수집+감지 통합 MainViewModel
//        구 CollectorRuntime.MainViewModel + Monitor.MainViewModel 통합
//  V3 Step4: 신규
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Collector.Core;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.Collector.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    // §1 ─ 엔진 참조 ──────────────────────────────────────────
    public CollectionEngine CollectionEngine { get; }
    public MonitorEngine    MonitorEngine    { get; }

    // §2 ─ 탭 상태 ────────────────────────────────────────────
    [ObservableProperty] private int _activeTabIndex;  // 0=수집, 1=알람

    // §3 ─ 헤더 상태 ──────────────────────────────────────────
    [ObservableProperty] private string _collectorStatus = "대기";
    [ObservableProperty] private string _configInfo      = "";
    [ObservableProperty] private int    _liveTagCount;
    [ObservableProperty] private int    _activeAlarmCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAlarms))]
    private bool _hasActiveAlarms;

    // §4 ─ 서브 뷰 ────────────────────────────────────────────
    public CollectorViewModel CollectorView { get; }
    public MonitorViewModel   MonitorView   { get; }

    // §5 ─ 로그 ───────────────────────────────────────────────
    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    // §6 ─ 생성자 ─────────────────────────────────────────────
    public MainViewModel(CollectionEngine collectionEngine,
                         MonitorEngine    monitorEngine)
    {
        CollectionEngine = collectionEngine;
        MonitorEngine    = monitorEngine;

        CollectorView = new CollectorViewModel(collectionEngine);
        MonitorView   = new MonitorViewModel(monitorEngine);

        // 수집 엔진 상태 → 헤더 반영
        collectionEngine.StateChanged += state =>
        {
            CollectorStatus = state switch
            {
                EngineState.Running  => "수집 중",
                EngineState.Stopped  => "중지",
                EngineState.Starting => "시작 중...",
                EngineState.Error    => "오류",
                _                    => state.ToString(),
            };
        };

        // 태그 수 업데이트
        collectionEngine.TagCountChanged += count =>
            LiveTagCount = count;

        // 알람 수 업데이트
        monitorEngine.AlarmCountChanged += count =>
        {
            ActiveAlarmCount = count;
            HasActiveAlarms  = count > 0;
        };

        // 로그
        LogManager.Instance.LogReceived += entry =>
        {
            App.Current.Dispatcher.InvokeAsync(() =>
            {
                LogEntries.Insert(0, new LogEntry(
                    entry.Time.ToString("HH:mm:ss"),
                    entry.Level.ToString()[..4],
                    entry.Source,
                    entry.Message));
                if (LogEntries.Count > 500)
                    LogEntries.RemoveAt(LogEntries.Count - 1);
            });
        };
    }

    // §7 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private async Task StartAsync()
    {
        await CollectionEngine.StartAsync();
        await MonitorEngine.StartAsync();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await CollectionEngine.StopAsync();
        await MonitorEngine.StopAsync();
    }

    // §8 ─ IDisposable ─────────────────────────────────────────
    public void Dispose()
    {
        CollectorView.Dispose();
        MonitorView.Dispose();
    }
}

/// <summary>로그 표시 항목</summary>
public record LogEntry(string Time, string Level, string Source, string Msg);
