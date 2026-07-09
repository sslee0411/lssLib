// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/AlarmViewModel.cs
//  역할: [알람] 탭 ViewModel — AlarmAggregator 를 View 에 노출하고
//        ACK 명령을 CollectorConnectionManager 로 라우팅한다.
//  MN-03: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Connection;
using IIoT.Monitor.Models;

namespace IIoT.Monitor.ViewModels;

/// <summary>[알람] 탭의 ViewModel.</summary>
public partial class AlarmViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConnectionManager _connectionManager;

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>전체 Collector 통합 실시간 알람 집계기</summary>
    public AlarmAggregator Aggregator { get; }

    // §3 ─ 생성자 ──────────────────────────────────────────

    public AlarmViewModel(AlarmAggregator aggregator, CollectorConnectionManager connectionManager)
    {
        Aggregator         = aggregator;
        _connectionManager = connectionManager;
    }

    // §4 ─ 명령 ────────────────────────────────────────────

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
