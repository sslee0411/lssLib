// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Models/LiveTagValue.cs
//  역할: UI 바인딩용 실시간 태그값 (ObservableObject)
//        CollectionEngine → EventBus → ViewModel.LiveTags 반영
//  Fix: TagValue.cs의 중복 LiveTagValue 제거 후 이 파일이 단일 정의
//       IIoT.Shared.Contracts 참조 제거 (순환 의존 방지)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Shared.Models;

/// <summary>
/// WPF DataGrid / TagValueCell 바인딩용 Observable 모델.
/// CollectionEngine → UI 스레드에서 기존 인스턴스 재사용(Update).
/// </summary>
public sealed partial class LiveTagValue : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    [ObservableProperty] private string _tagId = string.Empty;
    [ObservableProperty] private string _tagName = string.Empty;
    [ObservableProperty] private string _deviceName = string.Empty;

    // §2 ─ 수집값 ─────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(QualityStatus))]
    private double _engValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(QualityStatus))]
    private TagQuality _quality = TagQuality.Unknown;

    [ObservableProperty] private DateTime _lastUpdated;
    [ObservableProperty] private string _unit = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    private int _decimalPlaces = 2;

    // §3 ─ 계산 프로퍼티 ──────────────────────────────────────
    /// <summary>UI 표시용 문자열 (소수점 적용, Bad/Unknown → "—")</summary>
    public string DisplayValue =>
        Quality is TagQuality.Bad or TagQuality.Unknown
            ? "—"
            : EngValue.ToString("F" + DecimalPlaces);

    /// <summary>StatusIndicator 연동 상태</summary>
    public IndicatorStatus QualityStatus => Quality switch
    {
        TagQuality.Good => IndicatorStatus.Good,
        TagQuality.Uncertain => IndicatorStatus.Warn,
        TagQuality.Bad => IndicatorStatus.Bad,
        _ => IndicatorStatus.Unknown,
    };

    // §4 ─ 업데이트 ───────────────────────────────────────────
    /// <summary>새 수집값으로 UI 갱신 (기존 인스턴스 재사용)</summary>
    public void Update(TagValue v)
    {
        EngValue = v.Value;
        Quality = v.Quality;
        LastUpdated = v.Timestamp;
    }

    // §5 ─ 팩토리 ─────────────────────────────────────────────
    public static LiveTagValue Create(
        string tagId,
        string tagName,
        string deviceName = "",
        string unit = "",
        int decimalPlaces = 2) =>
        new()
        {
            TagId = tagId,
            TagName = tagName,
            DeviceName = deviceName,
            Unit = unit,
            DecimalPlaces = decimalPlaces,
        };
}