// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Validation/ConfigFieldRule.cs
//  역할: 단일 설정 필드의 검증 규칙 값 객체
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config.Validation;

/// <summary>
/// 단일 설정 필드 검증 규칙.
/// </summary>
/// <remarks>
/// <see cref="ConfigSchema"/> 빌더가 내부적으로 생성하며,
/// <see cref="ConfigValidator"/> 가 이 규칙을 기준으로 검증을 수행합니다.
/// <para>직접 생성하지 않고 <see cref="ConfigSchema.Require"/> /
/// <see cref="ConfigSchema.Optional"/> 을 통해 사용합니다.</para>
/// </remarks>
public sealed class ConfigFieldRule
{
    #region §1 ─ 필드 식별

    /// <summary>섹션 이름 (대소문자 무시).</summary>
    public string Section { get; init; } = string.Empty;

    /// <summary>키 이름 (대소문자 무시).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>필수 여부. <see langword="false"/> 이면 키가 없을 때 기본값을 사용.</summary>
    public bool Required { get; init; } = true;

    #endregion

    #region §2 ─ 값 유형 및 범위

    /// <summary>기대하는 값 유형.</summary>
    public ConfigValueType ValueType { get; init; } = ConfigValueType.String;

    /// <summary>
    /// 숫자 유형(<see cref="ConfigValueType.Int"/>, <see cref="ConfigValueType.Double"/> 등)의
    /// 최솟값. <see langword="null"/> 이면 하한 없음.
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// 숫자 유형의 최댓값. <see langword="null"/> 이면 상한 없음.
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// <see cref="ConfigValueType.String"/> / <see cref="ConfigValueType.NonEmptyString"/> 의
    /// 최대 문자 수. <see langword="null"/> 이면 제한 없음.
    /// </summary>
    public int? MaxLength { get; init; }

    #endregion

    #region §3 ─ 고급 검증

    /// <summary>
    /// <see cref="ConfigValueType.Regex"/> 유형일 때 사용할 정규식 패턴.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// <see cref="ConfigValueType.Enum"/> 유형일 때 허용되는 값 목록 (대소문자 무시).
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>
    /// 커스텀 검증 함수. 반환값이 비어있으면 성공, 오류 메시지 문자열이면 실패.
    /// </summary>
    public Func<string, string?>? CustomValidator { get; init; }

    #endregion

    #region §4 ─ 기본값 / 설명

    /// <summary>
    /// 키가 없을 때 사용할 기본값. <see langword="null"/> 이면 키 부재 시 검증 실패.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>이 필드에 대한 설명 (오류 메시지·문서에 사용).</summary>
    public string? Description { get; init; }

    #endregion

    #region §5 ─ 문자열 표현

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{Section}] {Key}  type={ValueType}  required={Required}" +
        (Min.HasValue || Max.HasValue ? $"  range=({Min}~{Max})" : "") +
        (DefaultValue is not null ? $"  default={DefaultValue}" : "");

    #endregion
}