// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/ScaleLibrary.cs
//  역할: 스케일 라이브러리 항목 모델
//        Raw 값 → 공학단위 변환 설정
//  S-06: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Models;

/// <summary>
/// 스케일 변환 모드
/// </summary>
public enum ScaleMode
{
    /// <summary>선형 변환: Y = Slope × X + Offset</summary>
    Linear,
    /// <summary>수식 변환: NCalc Expression (변수 x = Raw 값)</summary>
    Expression
}

/// <summary>
/// 스케일 라이브러리 항목.
/// Raw(PLC 레지스터 값) → 공학단위 변환 설정 1건.
/// Tag 에서 Id 로 참조.
/// </summary>
public partial class ScaleEntry : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────

    /// <summary>고유 ID (Tag 에서 참조키)</summary>
    public Guid Id { get; } = Guid.NewGuid();

    // §2 ─ 기본 정보 ──────────────────────────────────────────

    /// <summary>스케일 항목 이름 (예: 압력 0~10bar)</summary>
    [ObservableProperty]
    private string _name = "새 스케일";

    /// <summary>설명</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    // §3 ─ 변환 모드 ──────────────────────────────────────────

    /// <summary>
    /// 변환 모드 (Linear / Expression)
    /// ★ [NotifyPropertyChangedFor] — 모드 전환 시 편집기 폼 동적 전환
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinear))]
    [NotifyPropertyChangedFor(nameof(IsExpression))]
    private ScaleMode _mode = ScaleMode.Linear;

    public bool IsLinear     => Mode == ScaleMode.Linear;
    public bool IsExpression => Mode == ScaleMode.Expression;

    // §4 ─ Linear 변환 파라미터 ───────────────────────────────

    /// <summary>Raw 최솟값 (예: 0)</summary>
    [ObservableProperty]
    private double _rawMin;

    /// <summary>Raw 최댓값 (예: 4000)</summary>
    [ObservableProperty]
    private double _rawMax = 4000;

    /// <summary>공학단위 최솟값 (예: 0.0)</summary>
    [ObservableProperty]
    private double _engMin;

    /// <summary>공학단위 최댓값 (예: 10.0)</summary>
    [ObservableProperty]
    private double _engMax = 10;

    // §5 ─ Expression 변환 파라미터 ───────────────────────────

    /// <summary>
    /// NCalc 수식 (변수 x = Raw 값).
    /// 예) (x / 4000.0) * 10.0
    /// </summary>
    [ObservableProperty]
    private string _expression = "(x / 4000.0) * 10.0";

    // §6 ─ 공통 출력 설정 ─────────────────────────────────────

    /// <summary>공학단위 문자열 (예: bar / °C / rpm)</summary>
    [ObservableProperty]
    private string _unit = string.Empty;

    /// <summary>소수점 자릿수 (표시용)</summary>
    [ObservableProperty]
    private int _decimalPlaces = 2;

    // §7 ─ 미리보기 계산 ──────────────────────────────────────

    /// <summary>
    /// Linear 모드 기울기 (Slope = (EngMax-EngMin)/(RawMax-RawMin))
    /// </summary>
    public double Slope => (RawMax - RawMin) == 0 ? 0
        : (EngMax - EngMin) / (RawMax - RawMin);

    /// <summary>
    /// Linear 모드 오프셋 (Offset = EngMin - Slope × RawMin)
    /// </summary>
    public double Offset => EngMin - Slope * RawMin;

    /// <summary>미리보기용 수식 문자열</summary>
    public string PreviewFormula => IsLinear
        ? $"Y = {Slope:F4} × X + {Offset:F4}  [{Unit}]"
        : $"{Expression}  [{Unit}]";
}
