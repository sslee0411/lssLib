// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/ScaleLibrary.cs
//  역할: 스케일 라이브러리 항목 모델
//        Raw 값 → 공학단위 변환 설정
//  S-06 rev2: RadioButton 바인딩 오류 수정
//             IsLinear/IsExpression setter 추가
//             PreviewFormula 실시간 알림 추가
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Models;

/// <summary>스케일 변환 모드</summary>
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
/// </summary>
public partial class ScaleEntry : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────

    public Guid Id { get; } = Guid.NewGuid();

    // §2 ─ 기본 정보 ──────────────────────────────────────────

    [ObservableProperty]
    private string _name = "새 스케일";

    [ObservableProperty]
    private string _description = string.Empty;

    // §3 ─ 변환 모드 ──────────────────────────────────────────

    /// <summary>
    /// 변환 모드 (Linear / Expression)
    /// ★ [NotifyPropertyChangedFor] — IsLinear/IsExpression 알림 포함
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinear))]
    [NotifyPropertyChangedFor(nameof(IsExpression))]
    private ScaleMode _mode = ScaleMode.Linear;

    /// <summary>
    /// RadioButton 양방향 바인딩용 — getter/setter 모두 필요
    /// ★ IsChecked="{Binding IsLinear}" 는 setter 없으면 바인딩 오류
    /// </summary>
    public bool IsLinear
    {
        get => Mode == ScaleMode.Linear;
        set { if (value) Mode = ScaleMode.Linear; }
    }

    public bool IsExpression
    {
        get => Mode == ScaleMode.Expression;
        set { if (value) Mode = ScaleMode.Expression; }
    }

    // §4 ─ Linear 변환 파라미터 ───────────────────────────────

    /// <summary>
    /// Raw 최솟값.
    /// ★ [NotifyPropertyChangedFor] — Slope/Offset/PreviewFormula 연쇄 알림
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Slope))]
    [NotifyPropertyChangedFor(nameof(Offset))]
    [NotifyPropertyChangedFor(nameof(PreviewFormula))]
    private double _rawMin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Slope))]
    [NotifyPropertyChangedFor(nameof(Offset))]
    [NotifyPropertyChangedFor(nameof(PreviewFormula))]
    private double _rawMax = 4000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Slope))]
    [NotifyPropertyChangedFor(nameof(Offset))]
    [NotifyPropertyChangedFor(nameof(PreviewFormula))]
    private double _engMin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Slope))]
    [NotifyPropertyChangedFor(nameof(Offset))]
    [NotifyPropertyChangedFor(nameof(PreviewFormula))]
    private double _engMax = 10;

    // §5 ─ Expression 파라미터 ────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFormula))]
    private string _expression = "(x / 4000.0) * 10.0";

    // §6 ─ 공통 출력 설정 ─────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFormula))]
    private string _unit = string.Empty;

    [ObservableProperty]
    private int _decimalPlaces = 2;

    // §7 ─ 계산 프로퍼티 ──────────────────────────────────────

    public double Slope  => (RawMax - RawMin) == 0 ? 0
                            : (EngMax - EngMin) / (RawMax - RawMin);

    public double Offset => EngMin - Slope * RawMin;

    public string PreviewFormula => IsLinear
        ? $"Y = {Slope:F4} × X + {Offset:F4}  [{Unit}]"
        : $"{Expression}  [{Unit}]";
}
