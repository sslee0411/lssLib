// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/AlarmModels.cs
//  역할: 알람 시스템 핵심 모델 정의
//        AlarmLevel / AlarmRecord / DetectResult / TagValue
//  Phase 10: 신규
// ══════════════════════════════════════════════════════════

namespace IIoT.Monitor.Core;

// §1 ─ 알람 레벨 ──────────────────────────────────────────
/// <summary>알람 심각도 레벨</summary>
public enum AlarmLevel
{
    None  = 0,   // 정상
    L     = 1,   // 하한 경고
    LL    = 2,   // 하한 위험
    H     = 3,   // 상한 경고
    HH    = 4,   // 상한 위험
    Fault = 9,   // 통신 오류 / Bad Quality
}

// §2 ─ 알람 상태 ──────────────────────────────────────────
/// <summary>알람 생명주기 상태머신: Fired → Acked → Cleared</summary>
public enum AlarmState
{
    Fired   = 0,  // 발생 (미확인)
    Acked   = 1,  // 확인됨 (아직 복귀 안 됨)
    Cleared = 2,  // 복귀 완료
}

// §3 ─ 태그 품질 ──────────────────────────────────────────
/// <summary>수집 품질 코드 (CollectorRuntime 호환)</summary>
public enum TagQuality { Good, Uncertain, Bad, NotCollecting }

// §4 ─ 태그값 ─────────────────────────────────────────────
/// <summary>
/// 런타임 태그 순간값 (CollectorRuntime → Monitor EventBus 전달).
/// readonly record struct 로 힙 할당 최소화.
/// </summary>
public readonly record struct TagValue(
    string     TagId,
    double     Value,
    DateTime   Timestamp,
    TagQuality Quality)
{
    public static TagValue Good(string tagId, double value) =>
        new(tagId, value, DateTime.UtcNow, TagQuality.Good);

    public static TagValue Bad(string tagId) =>
        new(tagId, double.NaN, DateTime.UtcNow, TagQuality.Bad);
}

// §5 ─ 감지 결과 ──────────────────────────────────────────
/// <summary>AbstractDetector.OnDetectAsync() 반환값</summary>
public sealed record DetectResult(
    bool        IsAnomalous,
    AlarmLevel  Level,
    string      DetectorId,
    string      TagId,
    double      Value,
    string      Message,
    DateTime    DetectedAt)
{
    public static DetectResult Normal(string detectorId, string tagId, double value) =>
        new(false, AlarmLevel.None, detectorId, tagId, value, string.Empty, DateTime.UtcNow);

    public static DetectResult Anomaly(
        string detectorId, string tagId, double value,
        AlarmLevel level, string message) =>
        new(true, level, detectorId, tagId, value, message, DateTime.UtcNow);
}

// §6 ─ 알람 레코드 ────────────────────────────────────────
/// <summary>
/// 단일 알람 인스턴스.
/// AlarmStateManager 가 관리하며 WPF UI 에 바인딩됩니다.
/// </summary>
public sealed class AlarmRecord
{
    public string     AlarmId    { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string     DetectorId { get; init; } = string.Empty;
    public string     TagId      { get; init; } = string.Empty;
    public string     TagName    { get; set;  } = string.Empty;
    public AlarmLevel Level      { get; init; }
    public AlarmState State      { get; set;  } = AlarmState.Fired;
    public string     Message    { get; init; } = string.Empty;
    public double     TriggerValue { get; init; }
    public DateTime   FiredAt    { get; init; } = DateTime.Now;
    public DateTime?  AckedAt    { get; set;  }
    public DateTime?  ClearedAt  { get; set;  }
    public string?    AckedBy    { get; set;  }

    /// <summary>UI 색상 키 (DynamicResource 바인딩용)</summary>
    public string LevelColorKey => Level switch
    {
        AlarmLevel.HH    => "RedBrush",
        AlarmLevel.H     => "OrangeBrush",
        AlarmLevel.L     => "AccBrush",
        AlarmLevel.LL    => "Acc2Brush",
        AlarmLevel.Fault => "YellowBrush",
        _                => "Text3Brush",
    };

    /// <summary>UI 상태 텍스트</summary>
    public string StateText => State switch
    {
        AlarmState.Fired   => "발생",
        AlarmState.Acked   => "확인",
        AlarmState.Cleared => "복귀",
        _                  => "?",
    };

    /// <summary>경과 시간 (UI 표시용)</summary>
    public string ElapsedText
    {
        get
        {
            var e = DateTime.Now - FiredAt;
            if (e.TotalSeconds < 60)  return $"{(int)e.TotalSeconds}초";
            if (e.TotalMinutes < 60)  return $"{(int)e.TotalMinutes}분";
            return $"{(int)e.TotalHours}시간";
        }
    }
}

// §7 ─ EventBus 이벤트 ────────────────────────────────────
/// <summary>CollectorRuntime → Monitor 실시간 태그값 전달 이벤트</summary>
public sealed record TagValueUpdatedEvent(
    string     TagId,
    double     Value,
    TagQuality Quality) : lssLib.Messaging.EventMessage;

/// <summary>알람 발생 이벤트 (Monitor 내부 → SignalR Hub 등)</summary>
public sealed record AlarmFiredEvent(AlarmRecord Alarm)
    : lssLib.Messaging.EventMessage;

/// <summary>알람 ACK 이벤트</summary>
public sealed record AlarmAckedEvent(string AlarmId, string AckedBy)
    : lssLib.Messaging.EventMessage;

/// <summary>알람 복귀 이벤트</summary>
public sealed record AlarmClearedEvent(string AlarmId)
    : lssLib.Messaging.EventMessage;
