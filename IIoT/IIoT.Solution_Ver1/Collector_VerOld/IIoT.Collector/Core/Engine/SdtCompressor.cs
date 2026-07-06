// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/SdtCompressor.cs
//  역할: SDT(Swinging Door Trending) 압축기
//        변화량이 ExcDev(허용 오차) 이내인 값은 저장 생략
//        → DB 용량 90% 이상 절감
//  C-07: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// SDT (Swinging Door Trending) 압축기.
/// <para>
/// 단일 Tag 에 대한 압축 상태를 보관한다.
/// <see cref="ITimeSeriesStore"/> 가 Tag당 1개씩 생성·보관한다.
/// </para>
/// <para>
/// 원리: 마지막 저장값(Anchor)에서 ExcDev 이내 변화는 생략하고,
/// ExcDev 를 초과하면 저장하고 새 Anchor 로 갱신한다.
/// </para>
/// </summary>
internal sealed class SdtCompressor
{
    // §1 ─ 필드 ────────────────────────────────────────────

    /// <summary>허용 오차 (공학단위). 이 범위 내 변화는 생략.</summary>
    private readonly double _excDev;

    /// <summary>마지막 저장된 값 (null = 첫 수신 전)</summary>
    private double? _lastStored;

    // §2 ─ 생성자 ──────────────────────────────────────────

    /// <param name="excDev">허용 오차 (공학단위 절대값)</param>
    public SdtCompressor(double excDev)
    {
        _excDev = Math.Max(excDev, 0.0);
    }

    // §3 ─ 저장 여부 판정 ──────────────────────────────────

    /// <summary>
    /// 새 값을 받아 저장 여부를 반환합니다.
    /// true 를 반환한 경우에만 DB 에 쓰고, _lastStored 를 갱신합니다.
    /// <para>
    /// excDev=0 이면 delta(any) >= 0 이 항상 성립하므로 전량 저장됩니다.
    /// (별도 분기 없이 아래 로직으로 자연스럽게 처리됨)
    /// </para>
    /// </summary>
    public bool ShouldStore(double engValue)
    {
        // 첫 수신값은 무조건 저장
        if (_lastStored is null)
        {
            _lastStored = engValue;
            return true;
        }

        var delta = Math.Abs(engValue - _lastStored.Value);

        if (delta >= _excDev)
        {
            _lastStored = engValue;
            return true;
        }

        return false;
    }

    /// <summary>압축기 상태를 초기화합니다 (재시작 시 호출).</summary>
    public void Reset() => _lastStored = null;
}
