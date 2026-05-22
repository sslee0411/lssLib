// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/TagValue.cs
//  역할: 수집된 태그 단일 값 — 런타임 전용 (설정 파일 저장 안 함)
//  Phase 1: Core 데이터 모델
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// 수집 런타임에서 발행·소비되는 태그 순간 값.
/// readonly record struct 로 힙 할당 없이 채널을 통해 전달됩니다.
/// </summary>
public readonly record struct TagValue
{
    // §1 ─ 필드 ───────────────────────────────────────────────
    /// <summary>태그 식별자</summary>
    public string TagId { get; init; }

    /// <summary>공학 단위 변환 후 값 (스케일 적용 완료)</summary>
    public double Value { get; init; }

    /// <summary>수집 시각 (UTC)</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>품질 코드</summary>
    public TagQuality Quality { get; init; }

    // §2 ─ 팩토리 ─────────────────────────────────────────────
    public static TagValue Good(string tagId, double value)
        => new()
        {
            TagId = tagId,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Quality = TagQuality.Good
        };

    public static TagValue Bad(string tagId)
        => new()
        {
            TagId = tagId,
            Value = double.NaN,
            Timestamp = DateTime.UtcNow,
            Quality = TagQuality.Bad
        };

    public static TagValue Uncertain(string tagId, double value)
        => new()
        {
            TagId = tagId,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Quality = TagQuality.Uncertain
        };
}

/// <summary>태그 품질 코드 (OPC-UA Quality 기반)</summary>
public enum TagQuality : byte
{
    Good = 0,
    Uncertain = 1,
    Bad = 2,
}