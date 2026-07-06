// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/DeadbandFilter.cs
//  역할: 노이즈 수준의 미세 변화를 판별해 이벤트 발행 자체를 억제
//        (C-07 SDT 는 "저장"만 억제하지만, 이 필터는 EventBus 발행을 억제하여
//         Alarm 판정·UI 표시·MQTT/SignalR 등 시스템 전체에 영향을 준다)
//  C-16: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 데드밴드 필터. Tag 당 1개씩 생성·보관한다.
/// </summary>
internal sealed class DeadbandFilter
{
    // §1 ─ 필드 ────────────────────────────────────────────

    /// <summary>데드밴드 폭 (공학단위 절대값). 0 이하면 필터 비활성.</summary>
    private readonly double _deadband;

    private double? _lastPublished;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public DeadbandFilter(double deadband)
    {
        _deadband = Math.Max(deadband, 0.0);
    }

    // §3 ─ 판정 ────────────────────────────────────────────

    /// <summary>
    /// 이번 값을 억제(발행 생략)해야 하는지 판정합니다.
    /// </summary>
    /// <param name="value">이번에 수신한 공학값 (스파이크 필터 통과 후 값)</param>
    /// <returns>true = 노이즈 수준 변화로 판단되어 발행을 억제해야 함</returns>
    public bool ShouldSuppress(double value)
    {
        // 필터 비활성 또는 첫 수신 — 항상 발행
        if (_deadband <= 0.0 || _lastPublished is null)
        {
            _lastPublished = value;
            return false;
        }

        if (Math.Abs(value - _lastPublished.Value) < _deadband)
            return true; // 데드밴드 이내 — 억제

        _lastPublished = value;
        return false;
    }

    /// <summary>필터 상태를 초기화합니다 (재시작 시 호출).</summary>
    public void Reset() => _lastPublished = null;
}
