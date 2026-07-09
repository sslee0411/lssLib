// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Detection/Detectors/RateOfChangeDetector.cs
//  역할: AbstractDetector 예시 구현체 — 지정된 Tag의 값이 초당 임계치
//        이상으로 급변하면 트리거된다. (Collector의 ThresholdDetector가
//        커버하지 못하는 "변화 속도" 기반 이상 감지 — 센서 튐/급격한
//        공정 변화 등에 유용)
//  MN-04: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;

namespace IIoT.Monitor.Core.Detection.Detectors;

/// <summary>
/// 지정된 TagId 의 값 변화율(|Δ값| / Δ초)이 <see cref="_maxRatePerSec"/> 을
/// 초과하면 트리거되는 감지기.
/// </summary>
public sealed class RateOfChangeDetector : AbstractDetector
{
    private readonly string _tagId;
    private readonly double _maxRatePerSec;

    private double?         _lastValue;
    private DateTimeOffset  _lastTime;

    /// <param name="tagId">감시 대상 Tag ID</param>
    /// <param name="maxRatePerSec">허용 최대 변화율 (공학단위/초). 초과 시 트리거</param>
    public RateOfChangeDetector(string tagId, double maxRatePerSec)
        : base($"RateOfChange[{tagId}]")
    {
        _tagId         = tagId;
        _maxRatePerSec = maxRatePerSec;
    }

    protected override DetectionResult? Evaluate(LiveTagRow tag)
    {
        if (tag.TagId != _tagId)
            return null; // 이 감지기의 대상 Tag 가 아님 — 무시

        var now = tag.UpdatedAt;

        // 최초 1회는 기준값만 저장하고 판정하지 않음
        if (_lastValue is null)
        {
            _lastValue = tag.EngValue;
            _lastTime  = now;
            return new DetectionResult(false, "", DetectionSeverity.Info, tag.TagId, now);
        }

        var dtSec = (now - _lastTime).TotalSeconds;
        if (dtSec <= 0)
            return null; // 동일/역행 타임스탬프 — 판정 보류

        var rate = Math.Abs(tag.EngValue - _lastValue.Value) / dtSec;

        _lastValue = tag.EngValue;
        _lastTime  = now;

        var triggered = rate > _maxRatePerSec;
        var reason = triggered
            ? $"{tag.TagId} 변화율 {rate:F2}/s 가 임계 {_maxRatePerSec:F2}/s 초과"
            : "";

        return new DetectionResult(triggered, reason, DetectionSeverity.Warning, tag.TagId, now);
    }
}
