// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/AlarmRule.cs
//  역할: HH / H / L / LL 4단계 알람 규칙 정의
//  Phase 1: Core 데이터 모델
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// 4단계 레벨 알람 규칙 (공학 단위 기준 임계값).
/// ConfigManager 의 "AlarmLibrary" 섹션에 JSON 으로 직렬화됩니다.
/// </summary>
public record AlarmRule
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; init; } = string.Empty;

    // §2 ─ 임계값 (공학 단위, null = 비활성) ─────────────────
    /// <summary>High-High 임계값 — 최고 위험</summary>
    public double? HH { get; init; }

    /// <summary>High 임계값 — 경고</summary>
    public double? H { get; init; }

    /// <summary>Low 임계값 — 경고</summary>
    public double? L { get; init; }

    /// <summary>Low-Low 임계값 — 최저 위험</summary>
    public double? LL { get; init; }

    /// <summary>알람 복귀 데드밴드 — 히스테리시스 (공학 단위)</summary>
    public double DeadBand { get; init; } = 0.0;

    /// <summary>알람 발생 메시지 템플릿 ({tagName}, {value}, {level} 치환자 사용 가능)</summary>
    public string Message { get; init; } = "{tagName} {level} 알람: {value}";

    // §3 ─ 검증 ───────────────────────────────────────────────
    /// <summary>최소 1개 이상의 임계값이 활성화되어 있는지 확인합니다.</summary>
    public bool IsValid => HH.HasValue || H.HasValue || L.HasValue || LL.HasValue;

    // §4 ─ ConfigManager 직렬화 헬퍼 ──────────────────────────
    public string SectionKey => $"AlarmLibrary:{Id}";

    public Dictionary<string, string> ToConfigEntries() => new()
    {
        ["name"] = Name,
        ["hh"] = HH?.ToString("G") ?? string.Empty,
        ["h"] = H?.ToString("G") ?? string.Empty,
        ["l"] = L?.ToString("G") ?? string.Empty,
        ["ll"] = LL?.ToString("G") ?? string.Empty,
        ["deadBand"] = DeadBand.ToString("G"),
        ["message"] = Message,
    };

    public static AlarmRule FromConfigEntries(string id,
                                              IReadOnlyDictionary<string, string> e)
    {
        static double? ParseOpt(string? s)
            => !string.IsNullOrEmpty(s) && double.TryParse(s, out var v) ? v : null;

        return new AlarmRule
        {
            Id = id,
            Name = e.GetValueOrDefault("name", string.Empty),
            HH = ParseOpt(e.GetValueOrDefault("hh")),
            H = ParseOpt(e.GetValueOrDefault("h")),
            L = ParseOpt(e.GetValueOrDefault("l")),
            LL = ParseOpt(e.GetValueOrDefault("ll")),
            DeadBand = double.TryParse(e.GetValueOrDefault("deadBand"), out var db) ? db : 0,
            Message = e.GetValueOrDefault("message", "{tagName} {level} 알람: {value}"),
        };
    }
}