// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/DashboardViewModel.cs
//  역할: [대시보드] 탭 ViewModel — 카드형 요약 화면(D 스타일)
//        CollectorManageViewModel.Collectors / LiveTagAggregator.Rows 를
//        그대로 노출하고, KPI(연결수/전체Tag/Good품질수)는 2초 주기로 재계산한다.
//        (개별 항목의 StatusText/Quality 변경은 CollectionChanged 를 발생시키지
//         않으므로 타이머 기반 재계산이 가장 단순하고 안전함 — MN-04 이후
//         이벤트 기반으로 고도화 가능)
//  MN-02B: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace IIoT.Monitor.ViewModels;

/// <summary>[대시보드] 탭의 ViewModel.</summary>
public partial class DashboardViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly DispatcherTimer _refreshTimer;

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>등록된 전체 Collector 목록 (CollectorManageViewModel 과 동일 참조)</summary>
    public ObservableCollection<CollectorEndpoint> Collectors { get; }

    /// <summary>전체 Collector 통합 실시간 Tag 목록 (LiveTagAggregator 와 동일 참조)</summary>
    public ObservableCollection<LiveTagRow> Tags { get; }

    [ObservableProperty] private int _connectedCollectorCount;
    [ObservableProperty] private int _totalCollectorCount;
    [ObservableProperty] private int _totalTagCount;
    [ObservableProperty] private int _goodQualityCount;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public DashboardViewModel(CollectorManageViewModel collectorVm, LiveTagAggregator tagAggregator)
    {
        Collectors = collectorVm.Collectors;
        Tags       = tagAggregator.Rows;

        Collectors.CollectionChanged += (_, _) => _Recalculate();
        Tags.CollectionChanged       += (_, _) => _Recalculate();

        // ★ 개별 항목의 StatusText/Quality 실시간 변경 반영용 — 2초 주기 재계산
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) => _Recalculate();
        _refreshTimer.Start();

        _Recalculate();
    }

    // §4 ─ 재계산 ──────────────────────────────────────────

    private void _Recalculate()
    {
        TotalCollectorCount     = Collectors.Count;
        ConnectedCollectorCount = Collectors.Count(c => c.StatusText == "연결됨");
        TotalTagCount           = Tags.Count;
        GoodQualityCount        = Tags.Count(t => t.Quality == "Good");
    }
}
