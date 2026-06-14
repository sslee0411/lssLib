// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Models/TagValue.cs
//  역할: 수집 태그값 · AlarmRecord · EventBus 이벤트
//        Studio·Collector·Manager·Controls 공통 공유
//  Fix: LiveTagValue 중복 제거 → LiveTagValue.cs 단일 파일로 분리
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Shared.Models;

// §1 ─ 품질 열거형 ────────────────────────────────────────
public enum TagQuality
{
    Good,       // 정상 수집
    Uncertain,  // 불확실 (통신 불안정)
    Bad,        // 오류
    Unknown,    // 초기화 전
}

// §2 ─ StatusIndicator 상태 ───────────────────────────────
/// <summary>IIoT.UI.Controls.StatusIndicator 연동용 상태</summary>
public enum IndicatorStatus { Good, Warn, Bad, Unknown }

// §3 ─ 불변 수집값 ────────────────────────────────────────
/// <summary>
/// 단일 태그 수집값 (불변 record).
/// CollectionEngine 발행 → MonitorEngine 동일 프로세스 구독.
/// </summary>
public sealed record TagValue(
    string TagId,
    double Value,
    DateTime Timestamp,
    TagQuality Quality)
{
    // §3-1 ─ 팩토리 ──────────────────────────────────────
    public static TagValue Good(string tagId, double value) =>
        new(tagId, value, DateTime.UtcNow, TagQuality.Good);

    public static TagValue Bad(string tagId) =>
        new(tagId, double.NaN, DateTime.UtcNow, TagQuality.Bad);

    public static TagValue Uncertain(string tagId, double value) =>
        new(tagId, value, DateTime.UtcNow, TagQuality.Uncertain);

    // §3-2 ─ 헬퍼 ────────────────────────────────────────
    public bool IsGood => Quality == TagQuality.Good;

    /// <summary>SDT 압축 판단용 — Bad/Unknown 이면 NaN</summary>
    public double CompressibleValue =>
        Quality is TagQuality.Good or TagQuality.Uncertain ? Value : double.NaN;
}

// §4 ─ 알람 레코드 ────────────────────────────────────────
/// <summary>알람 상태 전이 레코드 (Fired → Acked → Cleared)</summary>
public sealed record AlarmRecord(
    string AlarmId,
    string TagId,
    string AlarmName,
    string Message,
    string Level,           // "HH"|"H"|"L"|"LL"|"Fault"
    double TriggerValue,
    DateTime OccurredAt,
    bool IsActive = true,
    DateTime? AcknowledgedAt = null,
    string? AckedBy = null,
    DateTime? ClearedAt = null)
{
    public bool IsAcknowledged => AcknowledgedAt.HasValue;
    public bool IsCleared => ClearedAt.HasValue;

    public string StateText =>
        IsCleared ? "복귀" : IsAcknowledged ? "확인됨" : "발생";

    public string StateColorKey =>
        IsCleared ? "Text3Brush" : IsAcknowledged ? "YellowBrush" : "RedBrush";
}

// §5 ─ EventBus 공유 이벤트 ──────────────────────────────
/// <summary>
/// CollectionEngine → MonitorEngine 태그값 전달 이벤트.
/// ★ in-process EventBus 전용
/// </summary>
public sealed record TagValueUpdatedEvent(TagValue Value)
    : lssLib.Messaging.EventMessage;

public sealed record AlarmFiredEvent(AlarmRecord Alarm)
    : lssLib.Messaging.EventMessage;

public sealed record AlarmAckedEvent(string AlarmId, string AckedBy)
    : lssLib.Messaging.EventMessage;

public sealed record AlarmClearedEvent(string AlarmId)
    : lssLib.Messaging.EventMessage;