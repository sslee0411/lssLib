// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Models/AlarmRow.cs
//  역할: [알람] 탭 그리드 1행 — Collector "AlarmChanged" 이벤트 1건
//        (IIoT.Monitor Models/AlarmRow.cs 이식 — 필드/이름 동일)
//  HM-14: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.HMI.Models;

/// <summary>
/// 실시간 알람 1건.
/// <para>
/// Collector 의 "AlarmChanged" SignalR 이벤트를 받을 때마다 이 행이 생성/갱신된다.
/// CollectorConnectionManager.AlarmChanged 로 재발행되는 값을 그대로 사용하므로
/// LayoutCanvasViewModel._OnAlarmChanged() 가 파싱하는 payload 스키마와 동일하다.
/// </para>
/// </summary>
public partial class AlarmRow : ObservableObject
{
    /// <summary>발신 Collector ID (연결 기준 태깅)</summary>
    public required string CollectorId { get; init; }

    /// <summary>발신 Collector 표시 이름 (그룹 헤더용)</summary>
    [ObservableProperty] private string _collectorName = string.Empty;

    /// <summary>알람 고유 키 (TagId:Level) — 같은 Collector 내에서 갱신 대상 식별용</summary>
    public required string AlarmKey { get; init; }

    public required string TagId { get; init; }

    public required string PlcId { get; init; }

    [ObservableProperty] private string _tagName = string.Empty;

    /// <summary>HH / H / L / LL</summary>
    [ObservableProperty] private string _level = string.Empty;

    /// <summary>Active / Acked / Recovered</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAcknowledge))]
    private string _status = "Active";

    [ObservableProperty] private string _message = string.Empty;

    [ObservableProperty] private double _engValue;

    [ObservableProperty] private DateTimeOffset _occurredAt;

    /// <summary>ACK 버튼 활성화 조건 — Active 상태일 때만 ACK 가능</summary>
    public bool CanAcknowledge => Status == "Active";
}
