// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Validation/ConfigSchema.cs
//  역할: 설정 스키마 빌더 — 필드별 검증 규칙 선언적 등록
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config.Validation;

/// <summary>
/// 설정 스키마 빌더.
/// </summary>
/// <remarks>
/// 선언적 체이닝으로 설정 파일의 필드별 검증 규칙을 정의합니다.
/// 정의된 스키마는 <see cref="ConfigValidator.Validate"/> 에 전달합니다.
/// <example><code>
/// // ── 스키마 정의 ──────────────────────────────────────────
/// var schema = new ConfigSchema()
///     // 필수 필드
///     .Require("Network", "Host",    ConfigValueType.IpAddress)
///     .Require("Network", "Port",    ConfigValueType.Port)
///     .Require("Network", "Timeout", ConfigValueType.Int,    range: (100, 30_000))
///     // 선택 필드 (없으면 기본값 적용)
///     .Optional("App", "Debug",   ConfigValueType.Bool,   defaultValue: "false")
///     .Optional("App", "LogLevel",ConfigValueType.Enum,
///         allowedValues: new[]{"Debug","Info","Warn","Error"})
///     // 커스텀 검증
///     .Require("App", "Version",  ConfigValueType.SemVer)
///     .Custom ("App", "DataDir",  v => Directory.Exists(v) ? null : $"경로 없음: {v}");
///
/// // ── 검증 실행 ────────────────────────────────────────────
/// var result = ConfigValidator.Validate(store, schema);
/// if (!result.IsValid)
///     foreach (var e in result.Errors) Console.WriteLine(e.Message);
/// </code></example>
/// </remarks>
public sealed class ConfigSchema
{
    #region §1 ─ 필드

    private readonly List<ConfigFieldRule> _rules = new();

    #endregion

    #region §2 ─ 규칙 등록 (체이닝 API)

    /// <summary>
    /// 필수 설정 필드를 등록합니다.
    /// </summary>
    /// <param name="section">섹션 이름.</param>
    /// <param name="key">키 이름.</param>
    /// <param name="type">값 유형.</param>
    /// <param name="range">숫자 유형의 허용 범위 (min, max). <see langword="null"/> 이면 범위 제한 없음.</param>
    /// <param name="maxLength">문자열 최대 길이. <see langword="null"/> 이면 제한 없음.</param>
    /// <param name="pattern"><see cref="ConfigValueType.Regex"/> 유형 전용 정규식 패턴.</param>
    /// <param name="allowedValues"><see cref="ConfigValueType.Enum"/> 유형 전용 허용 값 목록.</param>
    /// <param name="description">필드 설명 (오류 메시지에 포함).</param>
    public ConfigSchema Require(
        string section,
        string key,
        ConfigValueType type = ConfigValueType.String,
        (double min, double max)? range = null,
        int? maxLength = null,
        string? pattern = null,
        string[]? allowedValues = null,
        string? description = null)
    {
        _rules.Add(new ConfigFieldRule
        {
            Section = section,
            Key = key,
            Required = true,
            ValueType = type,
            Min = range?.min,
            Max = range?.max,
            MaxLength = maxLength,
            Pattern = pattern,
            AllowedValues = allowedValues,
            Description = description
        });
        return this;
    }

    /// <summary>
    /// 선택적 설정 필드를 등록합니다. 키가 없으면 <paramref name="defaultValue"/> 를 사용합니다.
    /// </summary>
    /// <param name="section">섹션 이름.</param>
    /// <param name="key">키 이름.</param>
    /// <param name="type">값 유형.</param>
    /// <param name="defaultValue">키 부재 시 사용할 기본값.</param>
    /// <param name="range">숫자 허용 범위.</param>
    /// <param name="allowedValues"><see cref="ConfigValueType.Enum"/> 허용 값 목록.</param>
    /// <param name="description">필드 설명.</param>
    public ConfigSchema Optional(
        string section,
        string key,
        ConfigValueType type = ConfigValueType.String,
        string? defaultValue = null,
        (double min, double max)? range = null,
        string[]? allowedValues = null,
        string? description = null)
    {
        _rules.Add(new ConfigFieldRule
        {
            Section = section,
            Key = key,
            Required = false,
            ValueType = type,
            DefaultValue = defaultValue,
            Min = range?.min,
            Max = range?.max,
            AllowedValues = allowedValues,
            Description = description
        });
        return this;
    }

    /// <summary>
    /// 커스텀 검증 함수를 가진 필드를 등록합니다.
    /// </summary>
    /// <param name="section">섹션 이름.</param>
    /// <param name="key">키 이름.</param>
    /// <param name="validator">검증 함수 — 성공 시 <see langword="null"/>, 실패 시 오류 메시지 반환.</param>
    /// <param name="required">필수 여부.</param>
    /// <param name="defaultValue">선택 필드의 기본값.</param>
    public ConfigSchema Custom(
        string section,
        string key,
        Func<string, string?> validator,
        bool required = true,
        string? defaultValue = null)
    {
        _rules.Add(new ConfigFieldRule
        {
            Section = section,
            Key = key,
            Required = required,
            ValueType = ConfigValueType.String,
            CustomValidator = validator,
            DefaultValue = defaultValue
        });
        return this;
    }

    #endregion

    #region §3 ─ 조회

    /// <summary>등록된 모든 규칙을 반환합니다.</summary>
    public IReadOnlyList<ConfigFieldRule> Rules => _rules;

    /// <summary>등록된 규칙 수.</summary>
    public int Count => _rules.Count;

    /// <summary>특정 섹션의 규칙 목록을 반환합니다.</summary>
    public IEnumerable<ConfigFieldRule> GetSection(string section) =>
        _rules.Where(r => r.Section.Equals(section, StringComparison.OrdinalIgnoreCase));

    #endregion
}