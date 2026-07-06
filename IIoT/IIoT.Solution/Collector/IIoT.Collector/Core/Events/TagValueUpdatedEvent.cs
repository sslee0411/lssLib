// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Events/TagValueUpdatedEvent.cs
//  역할: FlowEngine 폴링 결과를 EventBus 로 발행하는 메시지 정의
//        구독자(C-04 LiveTagViewModel 등)는 EventBus.Instance.Subscribe<T>() 로 수신
//  C-03: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using lssLib.Messaging;

namespace IIoT.Collector.Core.Events;

/// <summary>
/// Tag 값 갱신 이벤트.
/// <para>
/// FlowEngine 이 ReadTagsAsync() 결과를 받을 때마다 Tag 1개당 1건씩 발행한다.
/// C-04 수집 현황 UI 가 이 이벤트를 구독하여 화면을 갱신한다.
/// </para>
/// </summary>
/// <param name="Value">드라이버가 반환한 원시 Tag 값 (TagId/RawValue/Quality/Timestamp)</param>
/// <param name="PlcId">이 Tag 가 속한 PLC/Device ID (역참조 — UI 그룹핑용)</param>
/// <param name="EngValue">
/// C-05 ScaleEngine 적용 후 공학값.
/// ScaleEntryId 미설정 Tag 는 Raw 값을 double 로 캐스팅한 값과 동일.
/// </param>
/// <param name="Unit">공학 단위 (ScaleEntry.Unit 또는 Tag.Unit)</param>
/// <param name="DecimalPlaces">표시 소수 자릿수</param>
/// <param name="WasScaled">실제 스케일 변환 적용 여부</param>
public sealed record TagValueUpdatedEvent(
    TagValue Value,
    string   PlcId,
    double   EngValue,
    string   Unit,
    int      DecimalPlaces,
    bool     WasScaled
) : EventMessage;

/// <summary>
/// PLC 연결 상태 변경 이벤트.
/// <para>
/// FlowEngine 이 드라이버의 OnConnected/OnError 이벤트를 받을 때 발행한다.
/// C-04 수집 통계 패널의 "드라이버 상태" 카드가 이 이벤트를 구독한다.
/// </para>
/// </summary>
/// <param name="PlcId">PLC/Device ID</param>
/// <param name="DriverId">사용 중인 드라이버 ID</param>
/// <param name="IsConnected">연결 성공 여부</param>
/// <param name="Message">오류 메시지 (연결 성공 시 null)</param>
public sealed record PlcConnectionChangedEvent(
    string  PlcId,
    string  DriverId,
    bool    IsConnected,
    string? Message = null
) : EventMessage;

/// <summary>
/// PLC 수집 일시정지/재개 이벤트 (C-19 신규).
/// </summary>
/// <param name="PlcId">대상 PLC/Device ID</param>
/// <param name="IsPaused">true=일시정지됨, false=재개됨</param>
public sealed record PlcPauseChangedEvent(
    string PlcId,
    bool IsPaused
) : EventMessage;