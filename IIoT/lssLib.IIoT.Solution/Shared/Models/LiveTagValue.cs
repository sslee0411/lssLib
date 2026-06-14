// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Models/LiveTagValue.cs
//  역할: 실시간 태그 값 — UI 바인딩용 ObservableObject
//        CollectorRuntime MainViewModel·Monitor에서 사용
//        IIoT.UI.Controls StatusIndicator 연동 포함
//  V3 Step2: 신규
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Shared.Contracts;

namespace IIoT.Shared.Models;

/// <summary>
/// UI 바인딩용 실시간 태그 값.
/// CollectionEngine → EventBus → MainViewModel.LiveTags 에 반영.
///
/// <code>
/// // ViewModel에서 업데이트
/// void OnTagUpdated(TagValue v) {
///     var live = LiveTags.FirstOrDefault(t => t.TagId == v.TagId);
///     if (live is null) return;
///     live.Update(v);
/// }
/// </code>
/// </summary>
public sealed partial class LiveTagValue : ObservableObject
{
    // §1 ─ 식별 정보 ──────────────────────────────────────────
    [ObservableProperty] private string _tagId   = "";
    [ObservableProperty] private string _tagName = "";
    [ObservableProperty] private string _deviceName = "";

    // §2 ─ 수집 값 ────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(QualityStatus))]
    private double _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityStatus))]
    private TagQuality _quality = TagQuality.Unknown;

    [ObservableProperty] private DateTime _timestamp = DateTime.MinValue;
    [ObservableProperty] private string   _unit      = "";

    // §3 ─ 표시 설정 ──────────────────────────────────────────
    /// <summary>소수점 자리수 (기본 2)</summary>
    public int DecimalPlaces { get; init; } = 2;

    // §4 ─ 계산 프로퍼티 ──────────────────────────────────────

    /// <summary>UI 표시용 포맷 문자열 (소수점 적용)</summary>
    public string DisplayValue =>
        Quality == TagQuality.Unknown ? "—"
        : Value.ToString("F" + DecimalPlaces);

    /// <summary>
    /// IIoT.UI.Controls.StatusIndicator 연동 품질 상태.
    /// IndicatorStatus는 IIoT.UI.Controls 에 정의되어 있으므로
    /// string 키로 반환하고 각 프로그램에서 변환합니다.
    /// </summary>
    public TagQuality QualityStatus => Quality;

    // §5 ─ 업데이트 메서드 ────────────────────────────────────

    /// <summary>새 수집 값으로 업데이트</summary>
    public void Update(TagValue v)
    {
        Value     = v.Value;
        Quality   = v.Quality;
        Timestamp = v.Timestamp;
    }

    /// <summary>정적 팩토리 — 태그 구성 정보로 초기 생성</summary>
    public static LiveTagValue Create(string tagId, string tagName,
        string deviceName = "", string unit = "", int decimalPlaces = 2) =>
        new()
        {
            TagId       = tagId,
            TagName     = tagName,
            DeviceName  = deviceName,
            Unit        = unit,
            DecimalPlaces = decimalPlaces,
        };
}
