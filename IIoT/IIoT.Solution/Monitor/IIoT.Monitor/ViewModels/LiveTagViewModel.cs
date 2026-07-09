// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/LiveTagViewModel.cs
//  역할: [태그현황] 탭 ViewModel — LiveTagAggregator(DI 싱글턴)를 View 에 노출
//        집계 로직 자체는 Aggregator 가 담당하므로 이 ViewModel 은 얇게 유지한다.
//  MN-02: 신규
//  MN-EX-05: ToggleFavoriteCommand 추가 — ⭐ 클릭 시 즐겨찾기 토글
//  MN-EX-07: ExportCsvCommand 추가 — 현재 전체 Tag 값 스냅샷을 CSV로 저장
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-07)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Export;
using IIoT.Monitor.Core.Favorites;
using IIoT.Monitor.Models;
using lssLib.Log;
using Microsoft.Win32;

namespace IIoT.Monitor.ViewModels;

/// <summary>[태그현황] 탭의 ViewModel.</summary>
public partial class LiveTagViewModel : ObservableObject
{
    private readonly FavoriteTagService       _favoriteService;
    private readonly SnapshotCsvExportService _csvExport;

    /// <summary>전체 Collector 통합 실시간 Tag 집계기</summary>
    public LiveTagAggregator Aggregator { get; }

    /// <summary>CSV 내보내기 진행/완료 상태 (툴바 하단 표시용)</summary>
    [ObservableProperty] private string _exportStatus = string.Empty;

    public LiveTagViewModel(
        LiveTagAggregator          aggregator,
        FavoriteTagService         favoriteService,
        SnapshotCsvExportService   csvExport)
    {
        Aggregator       = aggregator;
        _favoriteService = favoriteService;
        _csvExport       = csvExport;
    }

    /// <summary>★ MN-EX-05: ⭐ 클릭 시 즐겨찾기 토글 + monitor.json 저장</summary>
    [RelayCommand]
    private async Task ToggleFavoriteAsync(LiveTagRow row)
    {
        if (row is null) return;
        await _favoriteService.ToggleAsync(row);
    }

    /// <summary>★ MN-EX-07: 현재 전체 Tag 값 스냅샷을 CSV 파일로 저장</summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Tag 현재값 스냅샷 CSV 내보내기",
            Filter     = "CSV 파일 (*.csv)|*.csv",
            FileName   = $"TagSnapshot_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            ExportStatus = "CSV 저장 중...";
            await _csvExport.ExportAsync(Aggregator.Rows, dlg.FileName);
            ExportStatus = $"✔ 저장 완료 ({Aggregator.Rows.Count}건) → {System.IO.Path.GetFileName(dlg.FileName)}";
            LogManager.Instance.Info("LiveTag", ExportStatus);
        }
        catch (Exception ex)
        {
            ExportStatus = $"✖ 저장 실패: {ex.Message}";
            LogManager.Instance.Warn("LiveTag", ExportStatus);
        }
    }
}
