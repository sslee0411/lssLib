// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/DashboardViewModel.cs
//  역할: [대시보드] 탭 ViewModel — 전체 요약 (프로그램 상태 집계 +
//        최근 이벤트 + 시스템 정보)
//  MG-05: 신규 — Monitor DashboardViewModel(2초 타이머 재계산) 패턴
//  MG-EX-06: 헬스체크 응답시간(ms) 추세 스파크라인 추가 —
//        프로그램별 PingTrendItem(OxyPlot PlotModel), 최근 150샘플(약 5분)
//        링버퍼 유지. 핑 실패 구간은 NaN → 선 끊김으로 표시.
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-06)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Manager.Core;
using IIoT.Manager.Core.Config;
using IIoT.Manager.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace IIoT.Manager.ViewModels;

/// <summary>
/// ★ MG-EX-06: 프로그램 1개의 응답시간 추세 스파크라인.
/// 축·테두리를 숨긴 미니 차트 — 최근 150샘플(2초 주기 ≒ 5분) 유지.
/// </summary>
public partial class PingTrendItem : ObservableObject
{
    // §1 ─ 상수/필드 ────────────────────────────────────────

    private const int _maxPoints = 150;

    private readonly LineSeries _series;
    private int _x;

    /// <summary>프로그램 이름 (차트 라벨)</summary>
    public string Name { get; }

    /// <summary>스파크라인 PlotModel (PlotView 바인딩)</summary>
    public PlotModel Plot { get; }

    /// <summary>현재값 문구 (예: "12 ms" / "—")</summary>
    [ObservableProperty]
    private string _currentText = "—";

    // §2 ─ 생성자 ───────────────────────────────────────────

    public PingTrendItem(string name)
    {
        Name = name;

        Plot = new PlotModel
        {
            PlotAreaBorderThickness = new OxyThickness(0),
            Padding                 = new OxyThickness(2),
            Background              = OxyColors.Transparent,
        };
        Plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, IsAxisVisible = false });
        Plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left,   IsAxisVisible = false, Minimum = 0 });

        _series = new LineSeries
        {
            StrokeThickness = 1.5,
            Color           = OxyColor.FromRgb(0x4F, 0xC3, 0xF7),   // 하늘색 — 테마 무관 고정
        };
        Plot.Series.Add(_series);
    }

    // §3 ─ 공개 메서드 ──────────────────────────────────────

    /// <summary>샘플 1개 추가 (null = 핑 실패/정지 → NaN 으로 선 끊김 표시).</summary>
    public void Append(long? pingMs)
    {
        _series.Points.Add(new DataPoint(_x++, pingMs ?? double.NaN));

        while (_series.Points.Count > _maxPoints)
            _series.Points.RemoveAt(0);

        CurrentText = pingMs is long ms ? $"{ms} ms" : "—";
        Plot.InvalidatePlot(true);
    }
}

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

    /// <summary>★ MG-EX-06: 프로그램 Id → 추세 차트 (생성 1회 후 재사용)</summary>
    private readonly Dictionary<string, PingTrendItem> _trendMap = new();

    /// <summary>메인 VM — 카드 목록 직접 바인딩용 (Processes)</summary>
    public ManagerMainViewModel Main { get; }

    /// <summary>이벤트 이력 — Events 직접 바인딩용</summary>
    public EventHistoryService History { get; }

    /// <summary>★ MG-EX-06: 응답시간 추세 차트 목록 (프로그램별)</summary>
    public ObservableCollection<PingTrendItem> PingTrends { get; } = [];

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

            // ★ MG-EX-06: 응답시간 샘플을 추세 차트에 누적
            //   (카드가 생기면 차트 자동 생성 — manager.json 변경에도 대응)
            if (!_trendMap.TryGetValue(card.Info.Id, out var trend))
            {
                trend = new PingTrendItem(card.Info.Name);
                _trendMap[card.Info.Id] = trend;
                PingTrends.Add(trend);
            }
            trend.Append(card.PingMs);
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
