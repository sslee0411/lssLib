// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/FlowViewModel.cs
//  역할: 수집 흐름 탭([🔀 수집 흐름]) ViewModel
//        FlowEngine.Stats 를 1초 주기로 읽어 PlcFlowCard 목록 갱신
//  C-09: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Engine;
using IIoT.Collector.Models;
using lssLib.Messaging;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.Collector.ViewModels;

/// <summary>
/// 수집 흐름 탭 ViewModel (DI 싱글턴).
/// <para>
/// AsyncScheduler 를 이용해 1초 주기로 FlowEngine.Stats 를 읽어
/// PLC 카드 목록(<see cref="PlcCards"/>) 을 갱신한다.
/// </para>
/// </summary>
public partial class FlowViewModel : ObservableObject, IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly FlowEngine           _flowEngine;
    private readonly CollectorConfigLoader _configLoader;
    private          ScheduledTask?        _refreshTask;

    // §2 ─ 바인딩 컬렉션 ───────────────────────────────────

    /// <summary>PLC 흐름 카드 목록 (FlowView 에 바인딩)</summary>
    public ObservableCollection<PlcFlowCardViewModel> PlcCards { get; } = new();

    // §3 ─ 요약 통계 ───────────────────────────────────────

    /// <summary>현재 수집 중인 PLC 수</summary>
    [ObservableProperty] private int    _connectedPlcCount;

    /// <summary>전체 PLC 수</summary>
    [ObservableProperty] private int    _totalPlcCount;

    /// <summary>전체 누적 폴링 수</summary>
    [ObservableProperty] private long   _totalPollCount;

    /// <summary>전체 누적 오류 수</summary>
    [ObservableProperty] private long   _totalErrorCount;

    /// <summary>엔진 실행 여부</summary>
    [ObservableProperty] private bool   _isEngineRunning;

    /// <summary>마지막 갱신 시각 (표시용)</summary>
    [ObservableProperty] private string _lastRefreshedAt = "—";

    // §4 ─ 생성자 ──────────────────────────────────────────

    public FlowViewModel(
        FlowEngine            flowEngine,
        CollectorConfigLoader configLoader)
    {
        _flowEngine    = flowEngine;
        _configLoader  = configLoader;

        BindingOperations.EnableCollectionSynchronization(PlcCards, new object());
    }

    // §5 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// PLC 카드 초기 목록 생성 + 1초 주기 갱신 스케줄러 시작.
    /// App.xaml.cs 에서 FlowEngine.StartAsync() 이후 호출.
    /// </summary>
    public void Initialize()
    {
        PlcCards.Clear();

        foreach (var plc in _configLoader.Plcs)
        {
            PlcCards.Add(new PlcFlowCardViewModel(
                plc.PlcId, plc.Name, plc.DriverId,
                plc.Tags.Count, plc.PollMs));
        }

        TotalPlcCount = PlcCards.Count;

        // 기존 스케줄 취소 (재초기화 대비)
        _refreshTask?.Cancel();

        // 1초 주기 갱신
        _refreshTask = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromSeconds(1),
            _RefreshAsync,
            name: "flow:refresh");
    }

    // §6 ─ 1초 주기 갱신 ───────────────────────────────────

    private Task _RefreshAsync(CancellationToken ct)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsEngineRunning = _flowEngine.IsRunning;

            var stats = _flowEngine.Stats;

            long totalPoll  = 0;
            long totalError = 0;
            int  connected  = 0;

            foreach (var card in PlcCards)
            {
                if (stats.TryGetValue(card.PlcId, out var stat))
                {
                    card.Update(stat);
                    totalPoll  += stat.PollCount;
                    totalError += stat.ErrorCount;
                    if (stat.IsConnected) connected++;
                }
            }

            TotalPollCount     = totalPoll;
            TotalErrorCount    = totalError;
            ConnectedPlcCount  = connected;
            LastRefreshedAt    = DateTime.Now.ToString("HH:mm:ss");
        });

        return Task.CompletedTask;
    }
    // §9 ─ 일시정지/재개 커맨드 (C-19 신규) ────────────────

    [RelayCommand]
    private void TogglePause(string plcId)
    {
        var card = PlcCards.FirstOrDefault(c => c.PlcId == plcId);
        if (card is null) return;

        if (card.IsPaused)
            _flowEngine.ResumeCollection(plcId);
        else
            _flowEngine.PauseCollection(plcId);
    }

    // §7 ─ 정리 ────────────────────────────────────────────

    public void Dispose()
    {
        _refreshTask?.Cancel();
    }
}
