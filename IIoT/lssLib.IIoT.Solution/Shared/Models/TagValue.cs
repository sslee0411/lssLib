// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Models/TagValue.cs
//  역할: 수집 태그 값 공유 모델
//        CollectorRuntime·Monitor·Manager 공통 사용
//  V3 Step2: 신규 (IIoT.Shared 프로젝트)
// ══════════════════════════════════════════════════════════

namespace IIoT.Shared.Models;

// §1 ─ 품질 열거형 ────────────────────────────────────────

/// <summary>태그 수집 값 품질 상태</summary>
public enum TagQuality
{
    /// <summary>정상 수집</summary>
    Good,
    /// <summary>통신 불안정 등 불확실</summary>
    Uncertain,
    /// <summary>통신 오류 / 드라이버 오류</summary>
    Bad,
    /// <summary>초기화 전 / 미수집</summary>
    Unknown,
}

// §2 ─ 불변 수집 값 ──────────────────────────────────────

/// <summary>
/// 단일 태그 수집 값 (불변 레코드).
/// CollectionEngine이 발행하고 MonitorEngine·DB가 구독합니다.
/// </summary>
/// <param name="TagId">태그 고유 ID</param>
/// <param name="Value">수집 원시값 (공학단위 변환 전)</param>
/// <param name="Timestamp">수집 시각 (UTC)</param>
/// <param name="Quality">수집 품질</param>
public sealed record TagValue(
    string     TagId,
    double     Value,
    DateTime   Timestamp,
    TagQuality Quality)
{
    /// <summary>정상 수집 여부</summary>
    public bool IsGood => Quality == TagQuality.Good;

    /// <summary>
    /// SDT 압축 판단에 사용할 값 (Bad·Unknown 이면 NaN 반환)
    /// </summary>
    public double CompressibleValue =>
        IsGood || Quality == TagQuality.Uncertain ? Value : double.NaN;
}

// §3 ─ 알람 레코드 ────────────────────────────────────────

/// <summary>알람 상태 전이 레코드</summary>
public sealed record AlarmRecord(
    string   AlarmId,
    string   TagId,
    string   Message,
    double   TriggerValue,
    DateTime OccurredAt,
    bool     IsActive       = true,
    DateTime? AcknowledgedAt = null,
    DateTime? ClearedAt      = null)
{
    public bool IsAcknowledged => AcknowledgedAt.HasValue;
    public bool IsCleared      => ClearedAt.HasValue;
}
