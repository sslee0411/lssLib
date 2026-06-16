// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/ScaleConfig.cs
//  역할: Raw → 공학 단위 선형 변환 정의
//        lssLib.Extensions.MapTo() 와 연계하여 사용
//  Phase 1: Core 데이터 모델
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// Raw 값 → 공학 단위 선형 변환 설정.
/// ConfigManager 의 "ScaleLibrary" 섹션에 JSON 으로 직렬화됩니다.
/// </summary>
/// <example><code>
/// var sc = new ScaleConfig
/// {
///     Id = "sc-temp", Name = "온도 4-20mA",
///     RawMin = 4, RawMax = 20, EngMin = 0, EngMax = 100, Unit = "°C"
/// };
/// // 변환: lssLib.Extensions.ScaleExtensions.MapTo()
/// double eng = rawValue.MapTo(sc.RawMin, sc.RawMax, sc.EngMin, sc.EngMax);
/// </code></example>
public record ScaleConfig
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; init; } = string.Empty;

    // §2 ─ 변환 범위 ──────────────────────────────────────────
    /// <summary>Raw 최솟값 (예: 4mA, 0, -32768)</summary>
    public double RawMin { get; init; } = 0;

    /// <summary>Raw 최댓값 (예: 20mA, 4095, 32767)</summary>
    public double RawMax { get; init; } = 100;

    /// <summary>공학 단위 최솟값</summary>
    public double EngMin { get; init; } = 0;

    /// <summary>공학 단위 최댓값</summary>
    public double EngMax { get; init; } = 100;

    /// <summary>공학 단위 (예: "°C", "bar", "m³/h")</summary>
    public string Unit { get; init; } = string.Empty;

    // §3 ─ 검증 ───────────────────────────────────────────────
    /// <summary>Raw 범위가 유효한지 확인합니다 (RawMin != RawMax)</summary>
    public bool IsValid => RawMax != RawMin;

    // §4 ─ ConfigManager 직렬화 헬퍼 ──────────────────────────
    /// <summary>ConfigManager 섹션명 (ScaleLibrary:[Id])</summary>
    public string SectionKey => $"ScaleLibrary:{Id}";

    public Dictionary<string, string> ToConfigEntries() => new()
    {
        ["name"] = Name,
        ["rawMin"] = RawMin.ToString("G"),
        ["rawMax"] = RawMax.ToString("G"),
        ["engMin"] = EngMin.ToString("G"),
        ["engMax"] = EngMax.ToString("G"),
        ["unit"] = Unit,
    };

    public static ScaleConfig FromConfigEntries(string id,
                                                IReadOnlyDictionary<string, string> e)
        => new()
        {
            Id = id,
            Name = e.GetValueOrDefault("name", string.Empty),
            RawMin = double.TryParse(e.GetValueOrDefault("rawMin"), out var rn) ? rn : 0,
            RawMax = double.TryParse(e.GetValueOrDefault("rawMax"), out var rx) ? rx : 100,
            EngMin = double.TryParse(e.GetValueOrDefault("engMin"), out var en) ? en : 0,
            EngMax = double.TryParse(e.GetValueOrDefault("engMax"), out var ex) ? ex : 100,
            Unit = e.GetValueOrDefault("unit", string.Empty),
        };
}