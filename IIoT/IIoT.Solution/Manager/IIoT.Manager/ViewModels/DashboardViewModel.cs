// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/DashboardViewModel.cs
//  역할: [대시보드] 탭 ViewModel — 전체 요약 (프로그램 상태 집계 +
//        최근 이벤트 + 시스템 정보)
//  MG-05: 신규 — Monitor DashboardViewModel(2초 타이머 재계산) 패턴
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Manager.Core;
using IIoT.Manager.Core.Config;
using IIoT.Manager.Models;
using System.Windows.Threading;

namespace IIoT.Manager.ViewModels;

/// <summary>
/// 대시보드 ViewModel.
/// <para>
/// ManagerMainViewModel.Processes(카드 상태)를 2초 주기로 집계하고,
/// EventHistoryService.Events 를 그대로 노출한다.
/// (카드 상태 자체는 ManagerMainViewModel 타이머가 갱신 — 여기선 집계만)
/// </para>
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly DispatcherTimer _timer;
    private readonly DateTime        _startedAt = DateTime.Now;

    /// <summary>메인 VM — 카드 목록 직접 바인딩용 (Processes)</summary>
    public ManagerMainViewModel Main { get; }

    /// <summary>이벤트 이력 — Events 직접 바인딩용</summary>
    public EventHistoryService History { get; }

    // §2 ─ 관찰 속성 (집계) ───────────────────────────────────

    /// <summary>전체 프로그램 수</summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>실행 중 수</summary>
    [ObservableProperty]
    private int _runningCount;

    /// <summary>응답 없음 수</summary>
    [ObservableProperty]
    private int _errorCount;

    /// <summary>정지 수</summary>
    [ObservableProperty]
    private int _stoppedCount;

    /// <summary>Manager 가동 시간 문구</summary>
    [ObservableProperty]
    private string _uptimeText = "";

    /// <summary>설정 파일 경로 (시스템 정보)</summary>
    public string SettingsPath => ManagerSettingsLoader.SettingsPath;

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public DashboardViewModel(ManagerMainViewModel main, EventHistoryService history)
    {
        Main    = main;
        History = history;

        // ★ 2초 주기 집계 재계산 (Monitor DashboardViewModel 과 동일 패턴)
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => _Recalculate();
        _timer.Start();

        _Recalculate();
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    private void _Recalculate()
    {
        int running = 0, error = 0, stopped = 0;

        foreach (var card in Main.Processes)
        {
            switch (card.State)
            {
                case ProcessState.Running: running++; break;
                case ProcessState.Error:   error++;   break;
                default:                   stopped++; break;
            }
        }

        TotalCount   = Main.Processes.Count;
        RunningCount = running;
        ErrorCount   = error;
        StoppedCount = stopped;

        var up = DateTime.Now - _startedAt;
        UptimeText = up.TotalHours >= 1
            ? $"{(int)up.TotalHours}시간 {up.Minutes}분"
            : $"{up.Minutes}분 {up.Seconds}초";
    }
}
