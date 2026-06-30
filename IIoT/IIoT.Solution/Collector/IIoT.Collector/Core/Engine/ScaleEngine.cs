// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/ScaleEngine.cs
//  역할: Raw 값 → 공학단위 변환 (ScaleEntryDto 기반)
//        Linear 모드: Studio ScaleLibraryViewModel._CalcEng() 와 동일 선형 공식
//        Expression 모드: NCalc 로 실제 수식 평가
//          (Studio ScaleLibraryViewModel.cs 주석:
//           "Expression 모드: NCalc 미지원 → 근사 선형 계산 (Collector에서 NCalc 사용)"
//           → Collector 가 실제 수식 평가를 담당하는 설계)
//  C-05: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Models;
using lssLib.Log;
using NCalc;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 스케일 변환 엔진 (DI 싱글턴).
/// <para>
/// TagRuntimeConfig.ScaleEntryId 로 CollectorConfigLoader.ScaleLibrary 를 조회하여
/// Raw 값을 공학단위로 변환한다. ScaleEntryId 가 없거나 라이브러리에 없으면
/// 변환 없이 Raw 값을 그대로 반환한다 (수집 자체는 절대 중단되지 않음).
/// </para>
/// </summary>
public sealed class ScaleEngine
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader _configLoader;

    public ScaleEngine(CollectorConfigLoader configLoader)
    {
        _configLoader = configLoader;
    }

    // §2 ─ 변환 결과 ───────────────────────────────────────

    /// <summary>스케일 변환 결과.</summary>
    /// <param name="EngValue">변환된 공학값 (변환 실패/미설정 시 raw 를 double 로 캐스팅한 값)</param>
    /// <param name="Unit">공학 단위 (ScaleEntry.Unit, 미설정 시 Tag.Unit 그대로)</param>
    /// <param name="DecimalPlaces">표시 소수 자릿수 (ScaleEntry.DecimalPlaces, 기본 2)</param>
    /// <param name="WasScaled">실제 스케일 변환이 적용되었는지 여부 (false = Raw 그대로 사용)</param>
    public readonly record struct ScaleResult(
        double EngValue,
        string Unit,
        int    DecimalPlaces,
        bool   WasScaled);

    // §3 ─ 공개 API ────────────────────────────────────────

    /// <summary>
    /// Tag 의 ScaleEntryId 설정에 따라 raw 값을 공학단위로 변환합니다.
    /// ScaleEntryId 가 없거나 라이브러리에서 찾을 수 없으면 변환 없이 반환합니다.
    /// </summary>
    public ScaleResult Apply(TagRuntimeConfig tag, object? raw)
    {
        var rawDouble = _ToDouble(raw);

        if (string.IsNullOrWhiteSpace(tag.ScaleEntryId) ||
            !_configLoader.ScaleLibrary.TryGetValue(tag.ScaleEntryId, out var entry))
        {
            return new ScaleResult(rawDouble, tag.Unit, 2, WasScaled: false);
        }

        var eng = entry.Mode == "Expression"
            ? _EvaluateExpression(entry.Expression, rawDouble, tag.Id)
            : _CalcLinear(entry, rawDouble);

        return new ScaleResult(eng, entry.Unit, entry.DecimalPlaces, WasScaled: true);
    }

    // §4 ─ Linear 변환 ─────────────────────────────────────

    /// <summary>
    /// Studio ScaleLibraryViewModel._CalcEng() 와 동일한 선형 변환 공식.
    /// EngMin + (raw - RawMin) × (EngMax - EngMin) / (RawMax - RawMin)
    /// </summary>
    private static double _CalcLinear(ScaleEntryDto entry, double raw)
    {
        if (entry.RawMax == entry.RawMin) return entry.EngMin;
        return entry.EngMin + (raw - entry.RawMin)
               * (entry.EngMax - entry.EngMin) / (entry.RawMax - entry.RawMin);
    }

    // §5 ─ Expression(NCalc) 변환 ──────────────────────────

    /// <summary>
    /// NCalc 로 수식을 평가합니다. 수식 내 변수명은 "x" (Studio 편집기 안내 "x = Raw 값" 과 동일).
    /// 평가 실패 시 raw 값을 그대로 반환하고 경고 로그를 남깁니다 (수집 중단 방지).
    /// </summary>
    private static double _EvaluateExpression(string? expression, double raw, string tagId)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return raw;

        try
        {
            var ncalc = new Expression(expression);
            ncalc.Parameters["x"] = raw;

            var result = ncalc.Evaluate();
            return Convert.ToDouble(result);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("ScaleEngine",
                $"[{tagId}] Expression 평가 실패 \"{expression}\": {ex.Message} — Raw 값 사용");
            return raw;
        }
    }

    // §6 ─ 헬퍼 ────────────────────────────────────────────

    private static double _ToDouble(object? raw) => raw switch
    {
        null      => 0.0,
        double d  => d,
        float f   => f,
        int i     => i,
        long l    => l,
        short s   => s,
        bool b    => b ? 1.0 : 0.0,
        string s2 when double.TryParse(s2, out var v) => v,
        _ => Convert.ToDouble(raw)
    };
}
