// ══════════════════════════════════════════════════════════
//  Analytics.Engine · AnalyticsService.cs
//  역할: SPC · OEE · Trend 통합 진입점
//        CollectorRuntime / Monitor 에서 DI로 주입하여 사용
//  Phase 13: 신규
//
//  사용 예 (CollectorRuntime):
//    var svc = new AnalyticsService();
//    var spc = await svc.RunSpcAsync(tagId, data, subgroupSize: 5);
//    var oee = await svc.RunOeeAsync(shift);
//    var trend = await svc.RunTrendAsync(tagId, data);
// ══════════════════════════════════════════════════════════

using Analytics.Engine.Oee;
using Analytics.Engine.Spc;
using Analytics.Engine.Trend;
using lssLib.Log;

namespace Analytics.Engine;

/// <summary>
/// Analytics.Engine 통합 서비스.
/// 모든 분석 메서드는 비동기로 제공하여 UI 스레드 블로킹을 방지합니다.
/// </summary>
public sealed class AnalyticsService
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private const string LogSrc = "AnalyticsService";

    // §2 ─ SPC ────────────────────────────────────────────────

    /// <summary>Xbar-R 관리도를 계산합니다.</summary>
    public Task<XbarRResult> RunSpcXbarRAsync(
        string                tagId,
        IReadOnlyList<double> data,
        int                   subgroupSize,
        double lsl = double.NaN,
        double usl = double.NaN,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            LogManager.Instance.Debug(LogSrc,
                $"SPC Xbar-R 시작: tag={tagId}, n={data.Count}, sg={subgroupSize}");
            return SpcEngine.CalcXbarR(data, subgroupSize, lsl, usl);
        }, ct);

    /// <summary>I-MR 관리도를 계산합니다.</summary>
    public Task<IMRResult> RunSpcIMRAsync(
        string                tagId,
        IReadOnlyList<double> data,
        double lsl = double.NaN,
        double usl = double.NaN,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            LogManager.Instance.Debug(LogSrc,
                $"SPC I-MR 시작: tag={tagId}, n={data.Count}");
            return SpcEngine.CalcIMR(data, lsl, usl);
        }, ct);

    // §3 ─ OEE ────────────────────────────────────────────────

    /// <summary>OEE를 계산합니다.</summary>
    public Task<OeeResult> RunOeeAsync(
        OeeInput input,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            LogManager.Instance.Debug(LogSrc,
                $"OEE 계산: plannedTime={input.PlannedTimeMin}분");
            return OeeEngine.Calculate(input);
        }, ct);

    /// <summary>기간별 OEE 추이를 계산합니다.</summary>
    public Task<IReadOnlyList<OeeResult>> RunOeeTrendAsync(
        IReadOnlyList<OeeInput> dailyInputs,
        CancellationToken ct = default)
        => Task.Run<IReadOnlyList<OeeResult>>(() =>
        {
            LogManager.Instance.Debug(LogSrc,
                $"OEE 추이 계산: {dailyInputs.Count}일");
            return OeeEngine.CalculateTrend(dailyInputs);
        }, ct);

    // §4 ─ Trend ──────────────────────────────────────────────

    /// <summary>추세 분석(선형회귀 + 지수평활 + 이상 조기경보)을 수행합니다.</summary>
    public Task<TrendAnalysisResult> RunTrendAsync(
        string                tagId,
        IReadOnlyList<double> data,
        double alpha        = 0.3,
        double warningSigma = 3.0,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            LogManager.Instance.Debug(LogSrc,
                $"Trend 분석 시작: tag={tagId}, n={data.Count}");
            return TrendEngine.Analyze(data, alpha, warningSigma);
        }, ct);

    /// <summary>PM 잔여 수명을 예측합니다.</summary>
    public Task<int?> EstimateRemainingLifeAsync(
        string                tagId,
        IReadOnlyList<double> data,
        double                threshold,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            var reg = TrendEngine.LinearRegression(data);
            int? steps = TrendEngine.EstimateRemainingLife(reg, data.Count, threshold);
            LogManager.Instance.Info(LogSrc,
                steps.HasValue
                    ? $"PM 예측: tag={tagId}, 임계={threshold}, 잔여≈{steps}스텝"
                    : $"PM 예측: tag={tagId}, 임계={threshold}에 도달하지 않음");
            return steps;
        }, ct);

    // §5 ─ 배치 분석 ──────────────────────────────────────────

    /// <summary>
    /// 여러 태그에 대해 I-MR 분석을 병렬 수행합니다.
    /// CollectorRuntime 야간 배치 분석에 활용합니다.
    /// </summary>
    public async Task<Dictionary<string, IMRResult>> BatchIMRAsync(
        Dictionary<string, IReadOnlyList<double>> tagData,
        CancellationToken ct = default)
    {
        var tasks = tagData.Select(kv =>
            RunSpcIMRAsync(kv.Key, kv.Value, ct: ct)
                .ContinueWith(t => (kv.Key, t.Result), ct));

        var results = await Task.WhenAll(tasks);
        LogManager.Instance.Info(LogSrc,
            $"배치 I-MR 완료: {results.Length}개 태그");

        return results.ToDictionary(r => r.Key, r => r.Result);
    }

    /// <summary>
    /// 여러 태그에 대해 추세 분석을 병렬 수행합니다.
    /// </summary>
    public async Task<Dictionary<string, TrendAnalysisResult>> BatchTrendAsync(
        Dictionary<string, IReadOnlyList<double>> tagData,
        double alpha        = 0.3,
        double warningSigma = 3.0,
        CancellationToken ct = default)
    {
        var tasks = tagData.Select(kv =>
            RunTrendAsync(kv.Key, kv.Value, alpha, warningSigma, ct)
                .ContinueWith(t => (kv.Key, t.Result), ct));

        var results = await Task.WhenAll(tasks);
        LogManager.Instance.Info(LogSrc,
            $"배치 Trend 완료: {results.Length}개 태그");

        return results.ToDictionary(r => r.Key, r => r.Result);
    }
}
