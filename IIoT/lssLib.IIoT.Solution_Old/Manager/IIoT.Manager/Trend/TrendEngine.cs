// ══════════════════════════════════════════════════════════
//  Analytics.Engine · Trend/TrendEngine.cs
//  역할: 추세 예측 엔진
//        선형회귀(Linear Regression) · 지수평활(Exponential Smoothing)
//        이상 조기경보 (예측 vs 실측 편차 임계값)
//  Phase 13: 신규
// ══════════════════════════════════════════════════════════

using lssLib.Log;

namespace Analytics.Engine.Trend;

// §1 ─ 예측 모델 결과 ─────────────────────────────────────
public sealed class LinearRegressionResult
{
    public double Slope       { get; init; }  // 기울기
    public double Intercept   { get; init; }  // 절편
    public double R2          { get; init; }  // 결정계수 (0~1)
    public double Rmse        { get; init; }  // 평균 제곱근 오차
    public bool   IsTrending  => Math.Abs(Slope) > 1e-9;

    /// <summary>x 지점의 예측값을 반환합니다.</summary>
    public double Predict(double x) => Slope * x + Intercept;

    /// <summary>n 스텝 후 예측값 목록을 반환합니다.</summary>
    public IReadOnlyList<double> Forecast(int dataLength, int steps)
    {
        var result = new double[steps];
        for (int i = 0; i < steps; i++)
            result[i] = Predict(dataLength + i);
        return result;
    }
}

public sealed class ExponentialSmoothingResult
{
    public double Alpha         { get; init; }  // 평활 계수 (0 < α < 1)
    public IReadOnlyList<double> Smoothed { get; init; } = [];
    public double LastSmoothed  => Smoothed.Count > 0 ? Smoothed[^1] : 0;

    /// <summary>n 스텝 단순 예측 (마지막 평활값 유지)</summary>
    public IReadOnlyList<double> Forecast(int steps)
        => Enumerable.Repeat(LastSmoothed, steps).ToArray();
}

// §2 ─ 이상 조기경보 결과 ──────────────────────────────────
public sealed record EarlyWarning(
    int    PointIndex,
    double Actual,
    double Predicted,
    double Deviation,
    string Message);

// §3 ─ 추세 분석 결과 (통합) ──────────────────────────────
public sealed class TrendAnalysisResult
{
    public LinearRegressionResult    Regression  { get; init; } = new();
    public ExponentialSmoothingResult Smoothing  { get; init; } = new();
    public IReadOnlyList<EarlyWarning> Warnings  { get; init; } = [];

    public bool HasWarnings  => Warnings.Count > 0;
    public string TrendText  => Regression.Slope switch
    {
        > 0.01  => "↗ 상승 추세",
        < -0.01 => "↘ 하락 추세",
        _       => "→ 안정",
    };
}

// §4 ─ 추세 엔진 ───────────────────────────────────────────
public static class TrendEngine
{
    private const string LogSrc = "TrendEngine";

    // §4-1 선형회귀 ──────────────────────────────────────
    /// <summary>
    /// 최소제곱법 선형회귀를 수행합니다.
    /// x = 0,1,2,...,n-1 (인덱스), y = 측정값
    /// </summary>
    public static LinearRegressionResult LinearRegression(
        IReadOnlyList<double> data)
    {
        int n = data.Count;
        if (n < 2) throw new ArgumentException("선형회귀: 최소 2개 필요");

        double sumX  = 0, sumY  = 0;
        double sumXY = 0, sumX2 = 0;

        for (int i = 0; i < n; i++)
        {
            sumX  += i;
            sumY  += data[i];
            sumXY += i * data[i];
            sumX2 += i * i;
        }

        double slope     = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        double intercept = (sumY - slope * sumX) / n;

        // R² 계산
        double mean   = sumY / n;
        double ssTot  = data.Sum(v => (v - mean) * (v - mean));
        double ssRes  = 0;
        for (int i = 0; i < n; i++)
        {
            double pred = slope * i + intercept;
            ssRes += (data[i] - pred) * (data[i] - pred);
        }
        double r2   = ssTot > 0 ? 1.0 - ssRes / ssTot : 1.0;
        double rmse = Math.Sqrt(ssRes / n);

        LogManager.Instance.Debug(LogSrc,
            $"LinearRegression: slope={slope:F4}, R²={r2:F3}, RMSE={rmse:F4}");

        return new LinearRegressionResult
        {
            Slope     = slope,
            Intercept = intercept,
            R2        = r2,
            Rmse      = rmse,
        };
    }

    // §4-2 지수평활 ──────────────────────────────────────
    /// <summary>
    /// 단순 지수평활을 수행합니다.
    /// α 값이 클수록 최근 데이터에 민감하게 반응합니다.
    /// 권장 α: 변동이 클 경우 0.1~0.2, 안정된 경우 0.3~0.5
    /// </summary>
    public static ExponentialSmoothingResult ExponentialSmoothing(
        IReadOnlyList<double> data,
        double alpha = 0.3)
    {
        if (data.Count < 1) throw new ArgumentException("지수평활: 최소 1개 필요");
        alpha = Math.Clamp(alpha, 0.01, 0.99);

        var smoothed = new double[data.Count];
        smoothed[0]  = data[0];  // 초기값 = 첫 번째 데이터

        for (int i = 1; i < data.Count; i++)
            smoothed[i] = alpha * data[i] + (1 - alpha) * smoothed[i - 1];

        return new ExponentialSmoothingResult
        {
            Alpha    = alpha,
            Smoothed = smoothed,
        };
    }

    // §4-3 통합 분석 ─────────────────────────────────────
    /// <summary>
    /// 선형회귀 + 지수평활 + 이상 조기경보를 통합 수행합니다.
    /// </summary>
    /// <param name="data">분석 대상 데이터</param>
    /// <param name="alpha">지수평활 계수 (기본 0.3)</param>
    /// <param name="warningSigma">이상 경보 임계값 (σ 배수, 기본 3σ)</param>
    public static TrendAnalysisResult Analyze(
        IReadOnlyList<double> data,
        double alpha        = 0.3,
        double warningSigma = 3.0)
    {
        var regression = LinearRegression(data);
        var smoothing  = ExponentialSmoothing(data, alpha);
        var warnings   = _DetectEarlyWarnings(data, regression, warningSigma);

        LogManager.Instance.Debug(LogSrc,
            $"Analyze: {data.Count}점, {(regression.IsTrending ? regression.TrendText : "안정")}, 경보 {warnings.Count}건");

        return new TrendAnalysisResult
        {
            Regression = regression,
            Smoothing  = smoothing,
            Warnings   = warnings,
        };
    }

    // §4-4 PM 잔여 수명 예측 ─────────────────────────────
    /// <summary>
    /// 현재 추세로 임계값에 도달하는 예상 스텝 수를 계산합니다.
    /// 예방 보전(PM) 일정 계산에 활용합니다.
    /// </summary>
    /// <param name="regression">선형회귀 결과</param>
    /// <param name="dataLength">현재 데이터 길이</param>
    /// <param name="threshold">임계값 (경보 발생 예상치)</param>
    /// <returns>임계값 도달까지 남은 스텝 수 (null = 도달 안 함)</returns>
    public static int? EstimateRemainingLife(
        LinearRegressionResult regression,
        int    dataLength,
        double threshold)
    {
        if (!regression.IsTrending) return null;

        // 기울기 방향과 임계값 방향이 같아야 의미 있음
        double current = regression.Predict(dataLength);
        bool   rising  = regression.Slope > 0;

        if (rising && current >= threshold) return 0;    // 이미 초과
        if (!rising && current <= threshold) return 0;

        if (rising  && threshold <= current) return null;
        if (!rising && threshold >= current) return null;

        // x = (threshold - intercept) / slope
        double xReach = (threshold - regression.Intercept) / regression.Slope;
        int steps     = (int)Math.Ceiling(xReach - dataLength);
        return steps > 0 ? steps : 0;
    }

    // §5 ─ 내부 헬퍼 ───────────────────────────────────────
    private static List<EarlyWarning> _DetectEarlyWarnings(
        IReadOnlyList<double>  data,
        LinearRegressionResult reg,
        double sigma)
    {
        var warnings = new List<EarlyWarning>();

        // 잔차 표준편차
        var residuals = data.Select((v, i) => v - reg.Predict(i)).ToArray();
        double resStd = _Std(residuals);
        double limit  = sigma * resStd;

        for (int i = 0; i < data.Count; i++)
        {
            double pred = reg.Predict(i);
            double dev  = Math.Abs(data[i] - pred);
            if (dev > limit)
                warnings.Add(new EarlyWarning(i, data[i], pred, dev,
                    $"점[{i}] 편차={dev:F3} ({dev / resStd:F1}σ)"));
        }
        return warnings;
    }

    private static double _Std(double[] data)
    {
        if (data.Length <= 1) return 0;
        double avg = data.Average();
        double sum = data.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sum / (data.Length - 1));
    }
}
