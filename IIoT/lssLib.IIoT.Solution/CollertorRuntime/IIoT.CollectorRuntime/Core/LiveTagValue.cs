// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Core/LiveTagValue.cs
//  역할: 실시간 수집 태그 값 — UI 바인딩용 ObservableObject
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IIoT.CollectorRuntime.Core;

/// <summary>수집 태그 품질 상태</summary>
public enum TagQuality { Good, Uncertain, Bad, NotCollecting }

/// <summary>
/// UI 에 실시간으로 표시되는 단일 태그 값 모델.
/// 수집 엔진이 값을 업데이트하면 WPF 바인딩이 자동 갱신됩니다.
/// </summary>
public partial class LiveTagValue : ObservableObject
{
    // §1 ─ 식별 (불변) ─────────────────────────────────────────
    public string TagId      { get; init; } = string.Empty;
    public string TagName    { get; init; } = string.Empty;
    public string Address    { get; init; } = string.Empty;
    public string Unit       { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;

    // §2 ─ 실시간 값 ───────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    [NotifyPropertyChangedFor(nameof(QualityColorKey))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private double _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityColorKey))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsGood))]
    private TagQuality _quality = TagQuality.NotCollecting;

    [ObservableProperty] private DateTime _lastUpdated;
    [ObservableProperty] private double   _minValue;
    [ObservableProperty] private double   _maxValue;
    [ObservableProperty] private int      _updateCount;
    [ObservableProperty] private bool     _isSelected;

    // §3 ─ 알람 ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlarm))]
    private string _alarmLevel = string.Empty; // "", "H", "HH", "L", "LL"

    public bool HasAlarm => !string.IsNullOrEmpty(AlarmLevel);

    // §4 ─ 표시용 프로퍼티 ─────────────────────────────────────
    public bool IsGood => Quality == TagQuality.Good;

    public string ValueText => Quality == TagQuality.Bad
        ? "---"
        : string.IsNullOrEmpty(Unit)
            ? $"{Value:F3}"
            : $"{Value:F3} {Unit}";

    public string StatusText => Quality switch
    {
        TagQuality.Good          => "GOOD",
        TagQuality.Uncertain     => "UNCERTAIN",
        TagQuality.Bad           => "BAD",
        TagQuality.NotCollecting => "대기중",
        _                        => "?"
    };

    /// <summary>WPF DataTrigger 에서 비교할 품질 색상 키 문자열</summary>
    public string QualityColorKey => Quality switch
    {
        TagQuality.Good      => "Good",
        TagQuality.Bad       => "Bad",
        TagQuality.Uncertain => "Uncertain",
        _                    => "None"
    };

    public string ElapsedText
    {
        get
        {
            if (LastUpdated == default) return "-";
            var e = DateTime.Now - LastUpdated;
            if (e.TotalSeconds < 60)  return $"{(int)e.TotalSeconds}초 전";
            if (e.TotalMinutes < 60)  return $"{(int)e.TotalMinutes}분 전";
            return $"{(int)e.TotalHours}시간 전";
        }
    }

    // §5 ─ 트렌드 히스토리 (최근 200개) ──────────────────────
    public ObservableCollection<TrendPoint> TrendHistory { get; } = [];

    /// <summary>수집 엔진이 호출 — UI 스레드에서 실행해야 합니다.</summary>
    public void UpdateValue(double value, TagQuality quality, DateTime? ts = null)
    {
        var now = ts ?? DateTime.Now;
        Value       = value;
        Quality     = quality;
        LastUpdated = now;
        UpdateCount++;

        if (quality == TagQuality.Good)
        {
            if (UpdateCount == 1 || value < MinValue) MinValue = value;
            if (UpdateCount == 1 || value > MaxValue) MaxValue = value;

            TrendHistory.Add(new TrendPoint(now, value));
            while (TrendHistory.Count > 200) TrendHistory.RemoveAt(0);
        }

        OnPropertyChanged(nameof(ElapsedText));
    }
}

/// <summary>트렌드 차트용 단일 포인트</summary>
public record TrendPoint(DateTime Time, double Value);
