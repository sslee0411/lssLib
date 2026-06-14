// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Models/TagValue.cs
//  역할: 수집 태그값 · LiveTagValue · AlarmRecord · EventBus 이벤트
//        Studio·Collector·Manager·Controls 공통 공유
//  V3: 신규 (구 DeviceManager·Monitor·CollectorRuntime 중복 제거)
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
/// 단일 태그 수집값 (불변 레코드).
/// CollectionEngine 발행 → MonitorEngine 동일 프로세스 구독.
/// </summary>
public sealed record TagValue(
    string TagId,
    double Value,
    DateTime Timestamp,
    TagQuality Quality)
{
    public bool IsGood => Quality == TagQuality.Good;

    /// <summary>SDT 압축 판단용 — Bad/Unknown 이면 NaN</summary>
    public double CompressibleValue =>
        Quality is TagQuality.Good or TagQuality.Uncertain ? Value : double.NaN;

    public static TagValue Good(string tagId, double value) =>
        new(tagId, value, DateTime.UtcNow, TagQuality.Good);

    public static TagValue Bad(string tagId) =>
        new(tagId, double.NaN, DateTime.UtcNow, TagQuality.Bad);

    public static TagValue Uncertain(string tagId, double value) =>
        new(tagId, value, DateTime.UtcNow, TagQuality.Uncertain);
}

// §4 ─ UI 바인딩용 LiveTagValue ───────────────────────────
/// <summary>
/// WPF DataGrid·TagValueCell 바인딩용 Observable 모델.
/// CollectionEngine → UI 스레드 갱신 시 기존 인스턴스 재사용.
/// </summary>
public sealed partial class LiveTagValue : ObservableObject
{
    [ObservableProperty] private string _tagId = string.Empty;
    [ObservableProperty] private string _tagName = string.Empty;
    [ObservableProperty] private double _rawValue;
    [ObservableProperty] private double _engValue;
    [ObservableProperty] private string _unit = string.Empty;
    [ObservableProperty] private TagQuality _quality = TagQuality.Unknown;
    [ObservableProperty] private DateTime _lastUpdated;
    [ObservableProperty] private int _decimalPlaces = 2;

    /// <summary>표시용 공학값 문자열</summary>
    public string DisplayValue =>
        Quality is TagQuality.Bad or TagQuality.Unknown
            ? "—"
            : EngValue.ToString("F" + DecimalPlaces);

    partial void OnEngValueChanged(double _) => OnPropertyChanged(nameof(DisplayValue));
    partial void OnDecimalPlacesChanged(int _) => OnPropertyChanged(nameof(DisplayValue));
    partial void OnQualityChanged(TagQuality _)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(QualityStatus));
    }

    public IndicatorStatus QualityStatus => Quality switch
    {
        TagQuality.Good => IndicatorStatus.Good,
        TagQuality.Uncertain => IndicatorStatus.Warn,
        TagQuality.Bad => IndicatorStatus.Bad,
        _ => IndicatorStatus.Unknown,
    };
}

// §5 ─ 알람 레코드 ────────────────────────────────────────
/// <summary>
/// 알람 상태 전이 레코드 (Fired → Acked → Cleared).
/// </summary>
public sealed record AlarmRecord(
    string AlarmId,
    string TagId,
    string AlarmName,
    string Message,
    string Level,            // "HH" | "H" | "L" | "LL" | "Fault"
    double TriggerValue,
    DateTime OccurredAt,
    bool IsActive = true,
    DateTime? AcknowledgedAt = null,
    string? AckedBy = null,
    DateTime? ClearedAt = null)
{
    public bool IsAcknowledged => AcknowledgedAt.HasValue;
    public bool IsCleared => ClearedAt.HasValue;

    public string StateText => IsCleared
        ? "복귀" : IsAcknowledged ? "확인됨" : "발생";

    public string StateColorKey => IsCleared
        ? "Text3Brush" : IsAcknowledged ? "YellowBrush" : "RedBrush";
}

// §6 ─ EventBus 공유 이벤트 ──────────────────────────────
/// <summary>
/// CollectionEngine → MonitorEngine 태그값 전달 이벤트.
/// ★ in-process EventBus 전용 (IIoT.Collector 단일 프로세스 통합으로 정상 동작)
/// </summary>
public sealed record TagValueUpdatedEvent(TagValue Value)
    : lssLib.Messaging.EventMessage;

public sealed record AlarmFiredEvent(AlarmRecord Alarm)
    : lssLib.Messaging.EventMessage;

public sealed record AlarmAckedEvent(string AlarmId, string AckedBy)
    : lssLib.Messaging.EventMessage;

public sealed record AlarmClearedEvent(string AlarmId)
    : lssLib.Messaging.EventMessage;