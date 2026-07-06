// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/SpikeFilter.cs
//  역할: 단발성 이상값(스파이크) 판정 — "2회 연속 확인" 방식
//        직전 값 대비 급변 시 즉시 폐기하지 않고, 다음 샘플에서도
//        비슷한 값이 반복되면 실제 계단 변화로 인정하여 수용한다.
//  C-16: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 스파이크 필터. Tag 당 1개씩 생성·보관한다.
/// <para>
/// 판정 로직:
/// <list type="number">
///   <item><description>새 값이 직전 수용값과 MaxDelta 이내면 정상 → 즉시 수용</description></item>
///   <item><description>MaxDelta 초과 시, 직전 "의심 후보"와 비교하여
///     또다시 비슷한 값이면(2회 연속) 실제 변화로 인정 → 수용</description></item>
///   <item><description>그 외에는 스파이크로 판정 → 폐기, 이전 수용값 유지,
///     이번 값은 다음 비교를 위한 후보로만 기록</description></item>
/// </list>
/// </para>
/// </summary>
internal sealed class SpikeFilter
{
    // §1 ─ 필드 ────────────────────────────────────────────

    /// <summary>스파이크 판정 임계값 (공학단위 절대값). 0 이하면 필터 비활성.</summary>
    private readonly double _maxDelta;

    private double? _lastAccepted;
    private double? _pendingCandidate;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public SpikeFilter(double maxDelta)
    {
        _maxDelta = Math.Max(maxDelta, 0.0);
    }

    // §3 ─ 판정 ────────────────────────────────────────────

    /// <summary>
    /// 새 값이 스파이크인지 판정합니다.
    /// </summary>
    /// <param name="newValue">이번에 수신한 공학값</param>
    /// <param name="acceptedValue">
    /// 스파이크로 판정되면 이전 정상값(유지할 값), 아니면 newValue 그대로.
    /// </param>
    /// <returns>true = 스파이크로 판정되어 폐기해야 함</returns>
    public bool IsSpike(double newValue, out double acceptedValue)
    {
        // 필터 비활성 또는 첫 수신 — 무조건 수용
        if (_maxDelta <= 0.0 || _lastAccepted is null)
        {
            _lastAccepted     = newValue;
            _pendingCandidate = null;
            acceptedValue     = newValue;
            return false;
        }

        var delta = Math.Abs(newValue - _lastAccepted.Value);

        // 정상 범위 — 즉시 수용
        if (delta <= _maxDelta)
        {
            _lastAccepted     = newValue;
            _pendingCandidate = null;
            acceptedValue     = newValue;
            return false;
        }

        // 급변 감지 — 직전 의심 후보와 비교해 "실제 변화"인지 판별
        if (_pendingCandidate is not null &&
            Math.Abs(newValue - _pendingCandidate.Value) <= _maxDelta)
        {
            // 2회 연속 유사값 → 실제 계단 변화로 인정
            _lastAccepted     = newValue;
            _pendingCandidate = null;
            acceptedValue     = newValue;
            return false;
        }

        // 스파이크로 판정 — 이전 수용값 유지, 이번 값은 후보로만 기록
        _pendingCandidate = newValue;
        acceptedValue     = _lastAccepted.Value;
        return true;
    }

    /// <summary>필터 상태를 초기화합니다 (재시작 시 호출).</summary>
    public void Reset()
    {
        _lastAccepted     = null;
        _pendingCandidate = null;
    }
}
