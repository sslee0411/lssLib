// ══════════════════════════════════════════════════════════
//  Analytics.Engine · Oee/OeeEngine.cs
//  역할: OEE (Overall Equipment Effectiveness) 계산 엔진
//        가용률(Availability) · 성능률(Performance) · 품질률(Quality)
//  Phase 13: 신규
//
//  OEE = 가용률 × 성능률 × 품질률
//  World Class OEE ≥ 85%
// ══════════════════════════════════════════════════════════

using lssLib.Log;

namespace Analytics.Engine.Oee;

// §1 ─ 손실 분류 (6대 손실) ───────────────────────────────
public enum LossType
{
    // 가용률 손실
    PlannedDowntime,    // 계획 정지
    UnplannedDowntime,  // 비계획 정지 (고장)
    Changeover,         // 품종 전환

    // 성능률 손실
    MinorStoppage,      // 소정지
    ReducedSpeed,       // 속도 저하

    // 품질률 손실
    Defects,            // 불량
    Startup,            // 초기 불량
}

// §2 ─ 손실 이벤트 ────────────────────────────────────────
public sealed record LossEvent(
    LossType  Loss,
    DateTime  StartTime,
    TimeSpan  Duration,
    string    Description = "");

// §3 ─ OEE 입력 ───────────────────────────────────────────
public sealed class OeeInput
{
    /// <summary>총 계획 시간 (분)</summary>
    public double PlannedTimeMin  { get; set; }

    /// <summary>계획 정지 시간 (분) — 정기 보전, 식사 시간 등</summary>
    public double PlannedDownMin  { get; set; }

    /// <summary>비계획 정지 시간 (분) — 고장, 비상 등</summary>
    public double UnplannedDownMin { get; set; }

    /// <summary>이상적 사이클 타임 (초/개)</summary>
    public double IdealCycleSec   { get; set; }

    /// <summary>총 생산 수량</summary>
    public int    TotalCount      { get; set; }

    /// <summary>불량 수량</summary>
    public int    DefectCount     { get; set; }

    /// <summary>손실 이벤트 목록 (상세 분석용)</summary>
    public List<LossEvent> LossEvents { get; } = [];
}

// §4 ─ OEE 결과 ───────────────────────────────────────────
public sealed class OeeResult
{
    // §4-1 시간 분석
    public double PlannedTimeMin      { get; init; }
    public double OperatingTimeMin    { get; init; }  // 계획 - 계획정지
    public double NetOperatingTimeMin { get; init; }  // 실제 가동 시간

    // §4-2 3대 지표
    public double Availability  { get; init; }  // 0~1
    public double Performance   { get; init; }  // 0~1
    public double Quality       { get; init; }  // 0~1
    public double OEE           { get; init; }  // 0~1

    // §4-3 백분율 표시
    public string AvailabilityPct => $"{Availability:P1}";
    public string PerformancePct  => $"{Performance:P1}";
    public string QualityPct      => $"{Quality:P1}";
    public string OEEPct          => $"{OEE:P1}";

    // §4-4 수량 분석
    public int    TotalCount   { get; init; }
    public int    DefectCount  { get; init; }
    public int    GoodCount    => TotalCount - DefectCount;

    // §4-5 OEE 등급
    public string Grade => OEE switch
    {
        >= 0.85 => "World Class ★",
        >= 0.70 => "양호",
        >= 0.50 => "보통",
        _       => "개선 필요",
    };

    // §4-6 손실 시간 분석 (폭포수 차트용)
    public Dictionary<LossType, double> LossByType { get; init; } = [];
}

// §5 ─ OEE 엔진 ───────────────────────────────────────────
public static class OeeEngine
{
    private const string LogSrc = "OeeEngine";

    /// <summary>OEE를 계산합니다.</summary>
    public static OeeResult Calculate(OeeInput input)
    {
        if (input.PlannedTimeMin <= 0)
            throw new ArgumentException("계획 시간 > 0 필요");

        // 운영 시간 = 계획 시간 - 계획 정지
        double opTime = input.PlannedTimeMin - input.PlannedDownMin;
        if (opTime <= 0) opTime = input.PlannedTimeMin;

        // 가용률 = (운영시간 - 비계획정지) / 운영시간
        double loadingTime = opTime - input.UnplannedDownMin;
        if (loadingTime < 0) loadingTime = 0;
        double availability = opTime > 0 ? loadingTime / opTime : 0;

        // 성능률 = (이상 사이클타임 × 총생산) / 실제 가동시간
        double theoreticalTimeMin = (input.IdealCycleSec / 60.0) * input.TotalCount;
        double performance = loadingTime > 0
            ? Math.Min(1.0, theoreticalTimeMin / loadingTime)
            : 0;

        // 품질률 = 양품 / 총생산
        double quality = input.TotalCount > 0
            ? (double)(input.TotalCount - input.DefectCount) / input.TotalCount
            : 1.0;

        double oee = availability * performance * quality;

        // 손실 타입별 분류
        var lossByType = input.LossEvents
            .GroupBy(e => e.Loss)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Duration.TotalMinutes));

        var result = new OeeResult
        {
            PlannedTimeMin      = input.PlannedTimeMin,
            OperatingTimeMin    = opTime,
            NetOperatingTimeMin = loadingTime,
            Availability        = availability,
            Performance         = performance,
            Quality             = quality,
            OEE                 = oee,
            TotalCount          = input.TotalCount,
            DefectCount         = input.DefectCount,
            LossByType          = lossByType,
        };

        LogManager.Instance.Debug(LogSrc,
            $"OEE={oee:P1} (A={availability:P1}, P={performance:P1}, Q={quality:P1}) [{result.Grade}]");

        return result;
    }

    /// <summary>기간별 OEE 추이를 계산합니다.</summary>
    public static IReadOnlyList<OeeResult> CalculateTrend(
        IReadOnlyList<OeeInput> dailyInputs)
        => dailyInputs.Select(Calculate).ToList();

    /// <summary>World Class 기준 달성 여부를 반환합니다.</summary>
    public static bool IsWorldClass(OeeResult result)
        => result.OEE >= 0.85;
}
