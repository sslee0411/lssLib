// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/AbstractDetector.cs
//  역할: 이상 감지기 추상 기반 클래스 (Template Method 패턴)
//        상속 → OnDetectAsync() 오버라이드로 커스텀 감지 로직 구현
//  Phase 10: 신규
//
//  확장 방법:
//    public class MyDetector : AbstractDetector {
//        public override string DetectorId  => "my-001";
//        public override string TargetTagId => "tag-001";
//        protected override Task<DetectResult> OnDetectAsync(TagValue v, CancellationToken ct)
//            => Task.FromResult(v.Value > 100
//                ? DetectResult.Anomaly(DetectorId, TargetTagId, v.Value, AlarmLevel.H, "상한 초과")
//                : DetectResult.Normal(DetectorId, TargetTagId, v.Value));
//    }
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Messaging;

namespace IIoT.Monitor.Core;

/// <summary>
/// 이상 감지기 추상 기반 클래스.
///
/// 구현 필수:
///   DetectorId  — 감지기 고유 ID
///   TargetTagId — 감시할 태그 ID
///   OnDetectAsync() — 실제 감지 로직
///
/// 선택 오버라이드:
///   OnAnomalyAsync()  — 이상 최초 발생 시 추가 처리
///   OnRecoveryAsync() — 이상 복귀 시 추가 처리
/// </summary>
public abstract class AbstractDetector : IDisposable
{
    // §1 ─ 추상 프로퍼티 ──────────────────────────────────────
    /// <summary>감지기 고유 ID (알람 레코드에 기록됨)</summary>
    public abstract string DetectorId  { get; }

    /// <summary>감시 대상 태그 ID</summary>
    public abstract string TargetTagId { get; }

    /// <summary>감지기 표시 이름 (UI 표시용)</summary>
    public virtual string DisplayName => DetectorId;

    // §2 ─ 상태 ───────────────────────────────────────────────
    private bool   _wasAnomalous;
    private bool   _disposed;

    /// <summary>현재 이상 상태 여부</summary>
    public bool IsAnomalous => _wasAnomalous;

    /// <summary>마지막 처리된 값</summary>
    public double LastValue { get; private set; } = double.NaN;

    /// <summary>마지막 처리 시각</summary>
    public DateTime LastProcessedAt { get; private set; }

    // §3 ─ 추상/가상 메서드 ───────────────────────────────────

    /// <summary>
    /// 실제 이상 감지 로직 — 하위 클래스에서 반드시 구현.
    /// </summary>
    protected abstract Task<DetectResult> OnDetectAsync(
        TagValue value, CancellationToken ct);

    /// <summary>
    /// 이상 최초 발생 시 호출 (선택 오버라이드).
    /// 기본 구현: EventBus 발행 (AlarmFiredEvent)
    /// </summary>
    protected virtual Task OnAnomalyAsync(DetectResult result)
    {
        EventBus.Instance.Publish(new AlarmFiredEvent(new AlarmRecord
        {
            DetectorId   = result.DetectorId,
            TagId        = result.TagId,
            Level        = result.Level,
            Message      = result.Message,
            TriggerValue = result.Value,
        }));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 이상 복귀 시 호출 (선택 오버라이드).
    /// 기본 구현: EventBus 발행 (AlarmClearedEvent 시그널)
    /// </summary>
    protected virtual Task OnRecoveryAsync(string detectorId)
    {
        LogManager.Instance.Info($"Detector.{detectorId}", "이상 복귀");
        return Task.CompletedTask;
    }

    // §4 ─ 프레임워크 내부 처리 ───────────────────────────────

    /// <summary>
    /// MonitorEngine 이 호출하는 처리 진입점.
    /// OnDetectAsync() 결과에 따라 상태 전환 + 훅 호출.
    /// </summary>
    internal async Task ProcessAsync(TagValue value, CancellationToken ct)
    {
        if (_disposed) return;

        try
        {
            LastValue       = value.Value;
            LastProcessedAt = DateTime.Now;

            var result = await OnDetectAsync(value, ct);

            if (result.IsAnomalous && !_wasAnomalous)
            {
                // 정상 → 이상 전환
                _wasAnomalous = true;
                LogManager.Instance.Warn($"Detector.{DetectorId}",
                    $"이상 감지 [{result.Level}]: {result.Message} (값={result.Value:F3})");
                await OnAnomalyAsync(result);
            }
            else if (!result.IsAnomalous && _wasAnomalous)
            {
                // 이상 → 정상 복귀
                _wasAnomalous = false;
                await OnRecoveryAsync(DetectorId);
            }
        }
        catch (OperationCanceledException) { /* 정상 중지 */ }
        catch (Exception ex)
        {
            LogManager.Instance.Error($"Detector.{DetectorId}",
                $"감지 처리 오류: {ex.Message}");
        }
    }

    // §5 ─ IDisposable ────────────────────────────────────────
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) => _disposed = true;
}

// ══════════════════════════════════════════════════════════
//  기본 제공 감지기 구현체 3종
// ══════════════════════════════════════════════════════════

/// <summary>
/// HH/H/L/LL 4단계 임계값 감지기.
/// alarm.json 의 AlarmRule 을 직접 적용합니다.
/// </summary>
public sealed class ThresholdDetector : AbstractDetector
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private readonly string _detectorId;
    private readonly string _targetTagId;

    // §2 ─ 임계값 설정 ────────────────────────────────────────
    public double? HH       { get; init; }
    public double? H        { get; init; }
    public double? L        { get; init; }
    public double? LL       { get; init; }
    public double  DeadBand { get; init; } = 0.5;

    public override string DetectorId  => _detectorId;
    public override string TargetTagId => _targetTagId;
    public override string DisplayName => $"임계값감지({_targetTagId})";

    // §3 ─ 생성자 ─────────────────────────────────────────────
    public ThresholdDetector(string detectorId, string targetTagId)
    {
        _detectorId  = detectorId;
        _targetTagId = targetTagId;
    }

    // §4 ─ 감지 로직 ──────────────────────────────────────────
    protected override Task<DetectResult> OnDetectAsync(
        TagValue value, CancellationToken ct)
    {
        if (value.Quality == TagQuality.Bad)
            return Task.FromResult(DetectResult.Anomaly(
                DetectorId, TargetTagId, value.Value,
                AlarmLevel.Fault, "통신 오류 (Bad Quality)"));

        double v = value.Value;

        if (HH.HasValue && v >= HH.Value)
            return Task.FromResult(DetectResult.Anomaly(
                DetectorId, TargetTagId, v, AlarmLevel.HH,
                $"HH 상한 초과: {v:F3} ≥ {HH.Value:F3}"));

        if (H.HasValue && v >= H.Value)
            return Task.FromResult(DetectResult.Anomaly(
                DetectorId, TargetTagId, v, AlarmLevel.H,
                $"H 상한 경고: {v:F3} ≥ {H.Value:F3}"));

        if (LL.HasValue && v <= LL.Value)
            return Task.FromResult(DetectResult.Anomaly(
                DetectorId, TargetTagId, v, AlarmLevel.LL,
                $"LL 하한 위험: {v:F3} ≤ {LL.Value:F3}"));

        if (L.HasValue && v <= L.Value)
            return Task.FromResult(DetectResult.Anomaly(
                DetectorId, TargetTagId, v, AlarmLevel.L,
                $"L 하한 경고: {v:F3} ≤ {L.Value:F3}"));

        return Task.FromResult(
            DetectResult.Normal(DetectorId, TargetTagId, v));
    }
}

/// <summary>
/// 변화율(ROC) 감지기 — 단위 시간당 변화량이 임계값 초과 시 알람.
/// 급격한 센서 변화, 설비 이상 조기 감지에 활용합니다.
/// </summary>
public sealed class RateOfChangeDetector : AbstractDetector
{
    private readonly string _detectorId;
    private readonly string _targetTagId;

    public double MaxRatePerSecond { get; init; } = 10.0;
    public override string DetectorId  => _detectorId;
    public override string TargetTagId => _targetTagId;
    public override string DisplayName => $"변화율감지({_targetTagId})";

    private double?  _prevValue;
    private DateTime _prevTime;

    public RateOfChangeDetector(string detectorId, string targetTagId)
    {
        _detectorId  = detectorId;
        _targetTagId = targetTagId;
    }

    protected override Task<DetectResult> OnDetectAsync(
        TagValue value, CancellationToken ct)
    {
        if (_prevValue is null)
        {
            _prevValue = value.Value;
            _prevTime  = value.Timestamp;
            return Task.FromResult(DetectResult.Normal(DetectorId, TargetTagId, value.Value));
        }

        double elapsed = (value.Timestamp - _prevTime).TotalSeconds;
        if (elapsed <= 0)
            return Task.FromResult(DetectResult.Normal(DetectorId, TargetTagId, value.Value));

        double rate = Math.Abs(value.Value - _prevValue.Value) / elapsed;
        _prevValue  = value.Value;
        _prevTime   = value.Timestamp;

        if (rate > MaxRatePerSecond)
            return Task.FromResult(DetectResult.Anomaly(
                DetectorId, TargetTagId, value.Value, AlarmLevel.H,
                $"급격한 변화 감지: {rate:F2}/s (임계: {MaxRatePerSecond:F2}/s)"));

        return Task.FromResult(DetectResult.Normal(DetectorId, TargetTagId, value.Value));
    }
}

/// <summary>
/// 통신 단절 감지기 — 지정 시간 내 수신 없으면 알람.
/// </summary>
public sealed class CommunicationWatchdog : AbstractDetector
{
    private readonly string _detectorId;
    private readonly string _targetTagId;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public override string DetectorId  => _detectorId;
    public override string TargetTagId => _targetTagId;
    public override string DisplayName => $"통신감시({_targetTagId})";

    private DateTime _lastReceived = DateTime.Now;

    public CommunicationWatchdog(string detectorId, string targetTagId)
    {
        _detectorId  = detectorId;
        _targetTagId = targetTagId;
    }

    protected override Task<DetectResult> OnDetectAsync(
        TagValue value, CancellationToken ct)
    {
        if (value.Quality != TagQuality.Bad)
            _lastReceived = DateTime.Now;

        var elapsed = DateTime.Now - _lastReceived;
        if (elapsed > Timeout)
            return Task.FromResult(DetectResult.Anomaly(
                DetectorId, TargetTagId, value.Value, AlarmLevel.Fault,
                $"통신 단절: {elapsed.TotalSeconds:F0}초 무응답"));

        return Task.FromResult(DetectResult.Normal(DetectorId, TargetTagId, value.Value));
    }
}
