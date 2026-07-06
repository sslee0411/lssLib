// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Events/TagForceWriteEvent.cs
//  역할: Tag 강제값 쓰기(Force Write) 실행 결과를 EventBus 로 발행
//        UI(StatusView 등)가 구독하여 성공/실패 토스트·이력 표시에 사용
//  C-15: 신규
//  C-15 버그 수정: 파라미터명 Timestamp → OccurredAt
//    (EventMessage.Timestamp 는 DateTime 타입 기존 프로퍼티 — 동일 이름 재사용 시
//     타입 불일치로 CS8866 컴파일 오류 발생. 코드베이스 관례에 맞춰 이름 변경)
//  생성: 2026-07-05 / 수정: 2026-07-06
// ══════════════════════════════════════════════════════════

using lssLib.Messaging;

namespace IIoT.Collector.Core.Events;

/// <summary>
/// Tag 강제값 쓰기(Force Write) 실행 결과 이벤트.
/// <para>
/// ForceWriteService.WriteAsync() 호출 결과(성공/실패 모두)를 발행한다.
/// SignalR/MQTT 로 확장 발행할 경우 이 이벤트를 추가 구독하면 된다.
/// </para>
/// </summary>
/// <param name="PlcId">대상 PLC/Device ID</param>
/// <param name="TagId">대상 Tag ID</param>
/// <param name="TagName">Tag 표시 이름 (로그·UI 표시용)</param>
/// <param name="Address">레지스터 주소</param>
/// <param name="Value">쓴 값 (문자열 표현, Raw 값 기준)</param>
/// <param name="IsSuccess">성공 여부</param>
/// <param name="Error">실패 사유 (성공 시 null)</param>
/// <param name="OccurredAt">
/// 실행 시각 (UTC).
/// ★ 이름 주의: EventMessage 가 이미 "Timestamp"(DateTime, get-only) 프로퍼티를 소유하고 있으므로
///   여기서 "Timestamp" 라는 이름을 재사용하면 타입 불일치(DateTime vs DateTimeOffset)로
///   CS8866 컴파일 오류가 발생한다. 코드베이스 관례(AlarmChangedEvent.OccurredAt 등)를 따라
///   "OccurredAt" 이라는 별도 이름을 사용한다.
/// </param>
public sealed record TagForceWriteEvent(
    string         PlcId,
    string         TagId,
    string         TagName,
    string         Address,
    string         Value,
    bool           IsSuccess,
    string?        Error,
    DateTimeOffset OccurredAt
) : EventMessage;
