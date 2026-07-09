// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/LiveTagViewModel.cs
//  역할: [태그현황] 탭 ViewModel — LiveTagAggregator(DI 싱글턴)를 View 에 노출
//        집계 로직 자체는 Aggregator 가 담당하므로 이 ViewModel 은 얇게 유지한다.
//  MN-02: 신규
//  MN-EX-05: ToggleFavoriteCommand 추가 — ⭐ 클릭 시 즐겨찾기 토글
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-05)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Favorites;
using IIoT.Monitor.Models;

namespace IIoT.Monitor.ViewModels;

/// <summary>[태그현황] 탭의 ViewModel.</summary>
public partial class LiveTagViewModel : ObservableObject
{
    private readonly FavoriteTagService _favoriteService;

    /// <summary>전체 Collector 통합 실시간 Tag 집계기</summary>
    public LiveTagAggregator Aggregator { get; }

    public LiveTagViewModel(LiveTagAggregator aggregator, FavoriteTagService favoriteService)
    {
        Aggregator       = aggregator;
        _favoriteService = favoriteService;
    }

    /// <summary>★ MN-EX-05: ⭐ 클릭 시 즐겨찾기 토글 + monitor.json 저장</summary>
    [RelayCommand]
    private async Task ToggleFavoriteAsync(LiveTagRow row)
    {
        if (row is null) return;
        await _favoriteService.ToggleAsync(row);
    }
}
