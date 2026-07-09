// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/AlarmViewModel.cs
//  역할: [알람] 탭 ViewModel — AlarmAggregator 를 View 에 노출하고
//        ACK 명령을 CollectorConnectionManager 로 라우팅한다.
//  MN-03: 신규
//  MN-EX-06: Collector/레벨/상태 필터 + Tag/메시지 검색 상태 추가.
//            실제 필터링(CollectionViewSource.Filter)은 AlarmView.xaml.cs 에서
//            이 ViewModel의 PropertyChanged 를 구독해 수행한다.
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-06)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Connection;
using IIoT.Monitor.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace IIoT.Monitor.ViewModels;

/// <summary>[알람] 탭의 ViewModel.</summary>
public partial class AlarmViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConnectionManager _connectionManager;

    private const string AllOption = "전체";

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>전체 Collector 통합 실시간 알람 집계기</summary>
    public AlarmAggregator Aggregator { get; }

    /// <summary>Collector 필터 콤보박스 목록 ("전체" + 등록된 Collector 이름들)</summary>
    public ObservableCollection<string> CollectorOptions { get; } = new() { AllOption };

    /// <summary>레벨 필터 옵션 (고정)</summary>
    public ObservableCollection<string> LevelOptions { get; } = new() { AllOption, "HH", "H", "L", "LL" };

    /// <summary>상태 필터 옵션 (고정)</summary>
    public ObservableCollection<string> StatusOptions { get; } = new() { AllOption, "Active", "Acked", "Recovered" };

    [ObservableProperty] private string _selectedCollector = AllOption;
    [ObservableProperty] private string _selectedLevel = AllOption;
    [ObservableProperty] private string _selectedStatus = AllOption;

    /// <summary>Tag ID / Tag 이름 / 메시지 대상 자유 검색어</summary>
    [ObservableProperty] private string _searchText = string.Empty;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public AlarmViewModel(AlarmAggregator aggregator, CollectorConnectionManager connectionManager)
    {
        Aggregator         = aggregator;
        _connectionManager = connectionManager;

        // Collector 필터 옵션을 실시간 알람 발생 Collector 기준으로 채움
        Aggregator.Rows.CollectionChanged += (_, _) => _RefreshCollectorOptions();
        _RefreshCollectorOptions();
    }

    private void _RefreshCollectorOptions()
    {
        foreach (var name in Aggregator.Rows.Select(r => r.CollectorName).Distinct())
            if (!CollectorOptions.Contains(name))
                CollectorOptions.Add(name);
    }

    // §4 ─ 필터 매칭 (AlarmView.xaml.cs 에서 호출) ─────────

    /// <summary>현재 필터 조건에 행이 부합하는지 검사한다.</summary>
    public bool MatchesFilter(AlarmRow row)
    {
        if (SelectedCollector != AllOption && row.CollectorName != SelectedCollector)
            return false;

        if (SelectedLevel != AllOption && row.Level != SelectedLevel)
            return false;

        if (SelectedStatus != AllOption && row.Status != SelectedStatus)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText;
            var hit = row.TagId.Contains(s, StringComparison.OrdinalIgnoreCase)
                   || row.TagName.Contains(s, StringComparison.OrdinalIgnoreCase)
                   || row.Message.Contains(s, StringComparison.OrdinalIgnoreCase);
            if (!hit) return false;
        }

        return true;
    }

    // §5 ─ 명령 ────────────────────────────────────────────

    /// <summary>
    /// 선택된 알람에 ACK 요청을 전송한다.
    /// ★ 발생 출처 Collector(row.CollectorId)로만 전송한다 — 오발송 방지(MN-03 설계 원칙).
    /// </summary>
    [RelayCommand]
    private async Task AcknowledgeAsync(AlarmRow row)
    {
        if (row is null || !row.CanAcknowledge)
            return;

        await _connectionManager.AcknowledgeAlarmAsync(row.CollectorId, row.AlarmKey);
    }
}
