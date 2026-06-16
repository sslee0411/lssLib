// ══════════════════════════════════════════════════════════
//  Analytics.Engine · Spc/SpcEngine.cs
//  역할: SPC 통계적 공정 관리 엔진
//        Xbar-R 관리도 · I-MR 관리도 · Cpk/Ppk · 관리 이탈 감지
//  Phase 13: 신규
//
//  사용 예:
//    var result = SpcEngine.CalcXbarR(subgroups, subgroupSize: 5);
//    if (result.HasViolation) ... // Western Electric 규칙
// ══════════════════════════════════════════════════════════

using lssLib.Log;

namespace Analytics.Engine.Spc;

// §1 ─ 관리도 종류 ────────────────────────────────────────
public enum ChartType { XbarR, XbarS, IMR, P, C }

// §2 ─ 이탈 규칙 ──────────────────────────────────────────
public enum ViolationRule
{
    Rule1_BeyondSigma3,     // 1점이 3σ 밖
    Rule2_Run9,             // 9점 연속 같은 쪽
    Rule3_Run6Trend,        // 6점 연속 단조 증감
    Rule4_Run14Alternating, // 14점 교대
    Rule5_2of3_Sigma2,      // 3점 중 2점이 2σ 밖
    Rule6_4of5_Sigma1,      // 5점 중 4점이 1σ 밖
}

// §3 ─ 이탈 인스턴스 ──────────────────────────────────────
public sealed record SpcViolation(
    ViolationRule Rule,
    int           PointIndex,
    double        Value,
    string        Description);

// §4 ─ Xbar-R 결과 ─────────────────────────────────────────
public sealed class XbarRResult
{
    // §4-1 중심선·한계
    public double XbarBar   { get; init; }  // X이중바 (전체 평균)
    public double XbarUcl   { get; init; }  // Xbar UCL
    public double XbarLcl   { get; init; }  // Xbar LCL
    public double RBar       { get; init; }  // R-bar (평균 범위)
    public double RUcl       { get; init; }  // R UCL
    public double RLcl       { get; init; }  // R LCL (보통 0)

    // §4-2 점 목록
    public IReadOnlyList<double> XbarPoints { get; init; } = [];
    public IReadOnlyList<double> RPoints     { get; init; } = [];

    // §4-3 공정 능력
    public double Cp  { get; init; }
    public double Cpk { get; init; }
    public double Pp  { get; init; }
    public double Ppk { get; init; }
    public double Sigma { get; init; }  // 단기 σ (R-bar/d2)

    // §4-4 이탈
    public IReadOnlyList<SpcViolation> Violations { get; init; } = [];
    public bool HasViolation => Violations.Count > 0;
}

// §5 ─ I-MR 결과 ──────────────────────────────────────────
public sealed class IMRResult
{
    public double IBar   { get; init; }
    public double IUcl   { get; init; }
    public double ILcl   { get; init; }
    public double MRBar  { get; init; }
    public double MRUcl  { get; init; }

    public IReadOnlyList<double> IPoints  { get; init; } = [];
    public IReadOnlyList<double> MRPoints { get; init; } = [];

    public double Sigma { get; init; }  // MRbar / d2(n=2)
    public IReadOnlyList<SpcViolation> Violations { get; init; } = [];
    public bool HasViolation => Violations.Count > 0;
}

// §6 ─ SPC 엔진 ───────────────────────────────────────────
public static class SpcEngine
{
    private const string LogSrc = "SpcEngine";

    // ── d2 상수표 (서브그룹 크기 2~10) ──
    private static readonly double[] D2 = [0, 0, 1.128, 1.693, 2.059, 2.326, 2.534, 2.704, 2.847, 2.970, 3.078];
    private static readonly double[] D3 = [0, 0, 0,     0,     0,     0,     0,     0.076, 0.136, 0.184, 0.223];
    private static readonly double[] D4 = [0, 0, 3.267, 2.574, 2.282, 2.114, 2.004, 1.924, 1.864, 1.816, 1.777];
    private static readonly double[] A2 = [0, 0, 1.880, 1.023, 0.729, 0.577, 0.483, 0.419, 0.373, 0.337, 0.308];

    // §6-1 Xbar-R 관리도 ─────────────────────────────────
    /// <summary>
    /// Xbar-R 관리도를 계산합니다.
    /// </summary>
    /// <param name="data">전체 측정값 (서브그룹 순서)</param>
    /// <param name="subgroupSize">서브그룹 크기 (2~10)</param>
    /// <param name="lsl">규격 하한 (Cpk 계산용)</param>
    /// <param name="usl">규격 상한 (Cpk 계산용)</param>
    public static XbarRResult CalcXbarR(
        IReadOnlyList<double> data,
        int    subgroupSize,
        double lsl = double.NaN,
        double usl = double.NaN)
    {
        if (data.Count < subgroupSize * 2)
            throw new ArgumentException("데이터 부족: 최소 2개 서브그룹 필요");

        subgroupSize = Math.Clamp(subgroupSize, 2, 10);

        // 서브그룹 분할
        int n          = data.Count / subgroupSize;
        var xbarPts    = new double[n];
        var rPts       = new double[n];

        for (int i = 0; i < n; i++)
        {
            var sub   = data.Skip(i * subgroupSize).Take(subgroupSize).ToArray();
            xbarPts[i] = sub.Average();
            rPts[i]    = sub.Max() - sub.Min();
        }

        double xbarBar = xbarPts.Average();
        double rBar    = rPts.Average();

        double a2   = A2[subgroupSize];
        double d2   = D2[subgroupSize];
        double d3   = D3[subgroupSize];
        double d4   = D4[subgroupSize];

        double xUcl = xbarBar + a2 * rBar;
        double xLcl = xbarBar - a2 * rBar;
        double rUcl = d4 * rBar;
        double rLcl = d3 * rBar;

        double sigma = rBar / d2;

        // Cpk 계산 (USL·LSL 주어진 경우)
        double cp = double.NaN, cpk = double.NaN, pp = double.NaN, ppk = double.NaN;
        if (!double.IsNaN(usl) && !double.IsNaN(lsl))
        {
            double sigmaLt = sigma;  // 단기
            double sigmaLl = _StdDev(data);  // 장기

            cp  = (usl - lsl) / (6 * sigmaLt);
            cpk = Math.Min((usl - xbarBar) / (3 * sigmaLt),
                           (xbarBar - lsl) / (3 * sigmaLt));
            pp  = (usl - lsl) / (6 * sigmaLl);
            ppk = Math.Min((usl - xbarBar) / (3 * sigmaLl),
                           (xbarBar - lsl) / (3 * sigmaLl));
        }

        var violations = _DetectWesternElectric(xbarPts, xbarBar, sigma);

        LogManager.Instance.Debug(LogSrc,
            $"Xbar-R: n={n}, Xbar={xbarBar:F4}, Rbar={rBar:F4}, σ={sigma:F4}, Cpk={cpk:F3}");

        return new XbarRResult
        {
            XbarBar    = xbarBar,
            XbarUcl    = xUcl,
            XbarLcl    = xLcl,
            RBar        = rBar,
            RUcl        = rUcl,
            RLcl        = rLcl,
            XbarPoints  = xbarPts,
            RPoints     = rPts,
            Cp          = cp,
            Cpk         = cpk,
            Pp          = pp,
            Ppk         = ppk,
            Sigma       = sigma,
            Violations  = violations,
        };
    }

    // §6-2 I-MR 관리도 ───────────────────────────────────
    /// <summary>
    /// I-MR 관리도를 계산합니다 (개별값·이동범위).
    /// 서브그룹 크기가 1인 경우 사용합니다.
    /// </summary>
    public static IMRResult CalcIMR(
        IReadOnlyList<double> data,
        double lsl = double.NaN,
        double usl = double.NaN)
    {
        if (data.Count < 3)
            throw new ArgumentException("I-MR: 최소 3개 데이터 필요");

        // 이동범위 계산
        var mrPts = new double[data.Count - 1];
        for (int i = 0; i < mrPts.Length; i++)
            mrPts[i] = Math.Abs(data[i + 1] - data[i]);

        double iBar  = data.Average();
        double mrBar = mrPts.Average();

        const double d2n2 = 1.128;  // d2(n=2)
        const double d4n2 = 3.267;  // D4(n=2)
        double sigma = mrBar / d2n2;

        double iUcl  = iBar + 3 * sigma;
        double iLcl  = iBar - 3 * sigma;
        double mrUcl = d4n2 * mrBar;

        var violations = _DetectWesternElectric(data.ToArray(), iBar, sigma);

        return new IMRResult
        {
            IBar      = iBar,
            IUcl      = iUcl,
            ILcl      = iLcl,
            MRBar     = mrBar,
            MRUcl     = mrUcl,
            IPoints   = data.ToArray(),
            MRPoints  = mrPts,
            Sigma     = sigma,
            Violations = violations,
        };
    }

    // §6-3 Western Electric 규칙 이탈 감지 ───────────────
    private static List<SpcViolation> _DetectWesternElectric(
        double[] pts, double center, double sigma)
    {
        var violations = new List<SpcViolation>();

        for (int i = 0; i < pts.Length; i++)
        {
            // 규칙 1: 3σ 초과
            if (Math.Abs(pts[i] - center) > 3 * sigma)
                violations.Add(new SpcViolation(ViolationRule.Rule1_BeyondSigma3,
                    i, pts[i], $"점[{i}] = {pts[i]:F3} (3σ 초과)"));

            // 규칙 2: 9점 연속 같은 쪽
            if (i >= 8)
            {
                bool allAbove = pts[(i - 8)..(i + 1)].All(v => v > center);
                bool allBelow = pts[(i - 8)..(i + 1)].All(v => v < center);
                if (allAbove || allBelow)
                    violations.Add(new SpcViolation(ViolationRule.Rule2_Run9,
                        i, pts[i], $"점[{i - 8}..{i}] 9점 연속 {(allAbove ? "상" : "하")}"));
            }

            // 규칙 3: 6점 연속 단조
            if (i >= 5)
            {
                var seg   = pts[(i - 5)..(i + 1)];
                bool trend = seg.Zip(seg.Skip(1)).All(p => p.First < p.Second) ||
                             seg.Zip(seg.Skip(1)).All(p => p.First > p.Second);
                if (trend)
                    violations.Add(new SpcViolation(ViolationRule.Rule3_Run6Trend,
                        i, pts[i], $"점[{i - 5}..{i}] 6점 단조 추세"));
            }

            // 규칙 5: 3점 중 2점이 2σ 밖
            if (i >= 2)
            {
                int outside2 = pts[(i - 2)..(i + 1)]
                    .Count(v => Math.Abs(v - center) > 2 * sigma);
                if (outside2 >= 2)
                    violations.Add(new SpcViolation(ViolationRule.Rule5_2of3_Sigma2,
                        i, pts[i], $"점[{i - 2}..{i}] 3점 중 {outside2}점이 2σ 밖"));
            }
        }

        return violations.DistinctBy(v => (v.Rule, v.PointIndex)).ToList();
    }

    // §7 ─ 내부 통계 헬퍼 ─────────────────────────────────
    private static double _StdDev(IReadOnlyList<double> data)
    {
        double avg = data.Average();
        double sum = data.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sum / (data.Count - 1));
    }
}
