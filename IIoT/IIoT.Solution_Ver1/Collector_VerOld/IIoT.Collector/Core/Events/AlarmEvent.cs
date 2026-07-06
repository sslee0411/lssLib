// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Events/AlarmEvent.cs
//  역할: 알람 상태 변경 이벤트 (EventBus 발행)
//        AlarmView 가 구독하여 실시간 UI 갱신
//  C-06: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using lssLib.Messaging;

namespace IIoT.Collector.Core.Events;

/// <summary>알람 레벨 (심각도 순서)</summary>
public enum AlarmLevel { LL, L, H, HH }

/// <summary>알람 상태</summary>
public enum AlarmStatus
{
    /// <summary>알람 발생 (미확인)</summary>
    Active,
    /// <summary>알람 확인됨 (ACK 처리)</summary>
    Acked,
    /// <summary>알람 복귀 (값이 임계값 이하로 돌아옴)</summary>
    Recovered
}

/// <summary>
/// 알람 상태 변경 이벤트.
/// ThresholdDetector → AlarmStateManager → EventBus 순으로 발행.
/// AlarmViewModel 이 구독하여 활성 알람 목록과 이력을 갱신한다.
/// </summary>
public sealed record AlarmChangedEvent(
    /// <summary>알람 고유 키 (TagId + AlarmLevel 조합)</summary>
    string      AlarmKey,
    /// <summary>이 알람이 속한 Tag ID</summary>
    string      TagId,
    /// <summary>Tag 표시 이름</summary>
    string      TagName,
    /// <summary>PLC/Device ID (그룹핑용)</summary>
    string      PlcId,
    /// <summary>알람 레벨 (HH/H/L/LL)</summary>
    AlarmLevel  Level,
    /// <summary>알람 상태 (Active/Acked/Recovered)</summary>
    AlarmStatus Status,
    /// <summary>알람 메시지 (Studio 알람 라이브러리에서 가져옴)</summary>
    string      Message,
    /// <summary>발생 시각 (UTC)</summary>
    DateTimeOffset OccurredAt,
    /// <summary>현재 공학값 (임계값 비교에 사용된 값)</summary>
    double      CurrentEngValue
) : EventMessage;
