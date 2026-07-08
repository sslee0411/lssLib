// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/LiveTagViewModel.cs
//  역할: [태그현황] 탭 ViewModel — LiveTagAggregator(DI 싱글턴)를 View 에 노출
//        집계 로직 자체는 Aggregator 가 담당하므로 이 ViewModel 은 얇게 유지한다.
//  MN-02: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Aggregation;

namespace IIoT.Monitor.ViewModels;

/// <summary>[태그현황] 탭의 ViewModel.</summary>
public sealed class LiveTagViewModel
{
    /// <summary>전체 Collector 통합 실시간 Tag 집계기</summary>
    public LiveTagAggregator Aggregator { get; }

    public LiveTagViewModel(LiveTagAggregator aggregator)
    {
        Aggregator = aggregator;
    }
}
