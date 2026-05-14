// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Validation/ConfigValidator.cs
//  역할: ConfigSchema 기반 설정 검증 실행 + ValidationResult 반환
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace lssLib.Config.Validation;

// ── ValidationError ───────────────────────────────────────────────────────

/// <summary>단일 검증 오류 항목.</summary>
/// <param name="Section">오류 발생 섹션.</param>
/// <param name="Key">오류 발생 키.</param>
/// <param name="Message">오류 메시지.</param>
/// <param name="Value">검증 시점의 실제 값 (<see langword="null"/> 이면 키 부재).</param>
public sealed record ValidationError(
    string Section,
    string Key,
    string Message,
    string? Value = null)
{
    /// <inheritdoc/>
    public override string ToString() =>
        Value is null
            ? $"[{Section}] {Key} — {Message}"
            : $"[{Section}] {Key} = \"{Value}\" — {Message}";
}

// ── ValidationResult ──────────────────────────────────────────────────────

/// <summary>
/// 설정 검증 결과.
/// </summary>
/// <remarks>
/// <see cref="ConfigValidator.Validate"/> 의 반환값입니다.
/// <see cref="IsValid"/> 가 <see langword="false"/> 이면 <see cref="Errors"/> 에
/// 오류 목록이 담겨있습니다.
/// <para><see cref="AppliedDefaults"/> 에는 부재 키에 적용된 기본값 목록이 담깁니다.</para>
/// </remarks>
public sealed class ValidationResult
{
    #region §1 ─ 결과

    /// <summary>검증 성공 여부 (오류 0건이면 <see langword="true"/>).</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>검증 오류 목록.</summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    /// <summary>기본값이 적용된 항목 목록 (섹션·키·기본값).</summary>
    public IReadOnlyList<(string Section, string Key, string DefaultValue)> AppliedDefaults
    { get; init; } = Array.Empty<(string, string, string)>();

    /// <summary>검증에 사용된 규칙 수.</summary>
    public int RuleCount { get; init; }

    #endregion

    #region §2 ─ 편의 메서드

    /// <summary>
    /// 검증이 실패하면 <see cref="ConfigValidationException"/> 을 throw 합니다.
    /// </summary>
    /// <exception cref="ConfigValidationException">검증 오류가 하나 이상 존재할 때.</exception>
    public ValidationResult ThrowIfInvalid()
    {
        if (!IsValid)
            throw new ConfigValidationException(Errors);
        return this;
    }

    /// <summary>오류 메시지를 여러 줄 문자열로 반환합니다.</summary>
    public string ToErrorText() =>
        Errors.Count == 0
            ? "(오류 없음)"
            : string.Join(Environment.NewLine, Errors.Select(e => $"  ✗  {e}"));

    /// <summary>검증 요약 문자열.</summary>
    public override string ToString() =>
        $"ValidationResult  규칙={RuleCount}  오류={Errors.Count}  기본값적용={AppliedDefaults.Count}";

    #endregion
}

// ── ConfigValidationException ─────────────────────────────────────────────

/// <summary>설정 검증 실패 예외.</summary>
public sealed class ConfigValidationException : Exception
{
    /// <summary>검증 오류 목록.</summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <inheritdoc/>
    public ConfigValidationException(IReadOnlyList<ValidationError> errors)
        : base($"설정 검증 실패 ({errors.Count}개 오류):\n" +
               string.Join("\n", errors.Select(e => $"  - {e}")))
    {
        Errors = errors;
    }
}

// ── ConfigValidator ───────────────────────────────────────────────────────

/// <summary>
/// <see cref="ConfigSchema"/> 기반 설정 검증 정적 헬퍼.
/// </summary>
/// <remarks>
/// <see cref="Validate"/> 실행 시:
/// <list type="number">
///   <item>필수 키 존재 여부 확인</item>
///   <item>선택 키 부재 시 기본값을 <see cref="ConfigStore"/> 에 자동 적용</item>
///   <item>값 유형·범위·패턴·열거형·커스텀 검증 순서로 실행</item>
/// </list>
/// <example><code>
/// var schema = new ConfigSchema()
///     .Require("Network", "Host",    ConfigValueType.IpAddress)
///     .Require("Network", "Port",    ConfigValueType.Port)
///     .Optional("App",    "Debug",   ConfigValueType.Bool, defaultValue: "false")
///     .Optional("App",    "LogLevel",ConfigValueType.Enum,
///         allowedValues: new[]{"Debug","Info","Warn","Error"}, defaultValue: "Info");
///
/// ValidationResult result = ConfigValidator.Validate(store, schema);
/// result.ThrowIfInvalid();
/// // 또는
/// if (!result.IsValid)
///     foreach (var err in result.Errors)
///         LogManager.Instance.Warn("Config", err.ToString());
/// </code></example>
/// </remarks>
public static class ConfigValidator
{
    #region §1 ─ Validate (메인 진입점)

    /// <summary>
    /// <see cref="ConfigStore"/> 를 <see cref="ConfigSchema"/> 규칙에 따라 검증합니다.
    /// </summary>
    /// <param name="store">검증할 설정 저장소.</param>
    /// <param name="schema">검증 규칙 집합.</param>
    /// <param name="applyDefaults">
    /// 선택 필드 부재 시 기본값을 <paramref name="store"/> 에 자동 적용할지 여부.
    /// 기본 <see langword="true"/>.
    /// </param>
    /// <returns><see cref="ValidationResult"/>.</returns>
    public static ValidationResult Validate(
        ConfigStore store,
        ConfigSchema schema,
        bool applyDefaults = true)
    {
        var errors = new List<ValidationError>();
        var defaults = new List<(string, string, string)>();

        foreach (var rule in schema.Rules)
        {
            var raw = store.Get(rule.Section, rule.Key);

            // ── 키 부재 처리 ──────────────────────────────
            if (raw is null)
            {
                if (rule.Required && rule.DefaultValue is null)
                {
                    errors.Add(new ValidationError(
                        rule.Section, rule.Key,
                        $"필수 키가 누락되었습니다" +
                        (rule.Description is not null ? $" ({rule.Description})" : "")));
                    continue;
                }

                if (rule.DefaultValue is not null)
                {
                    raw = rule.DefaultValue;
                    if (applyDefaults)
                    {
                        store.Set(rule.Section, rule.Key, raw);
                        defaults.Add((rule.Section, rule.Key, raw));
                    }
                }
                else continue;  // Optional + DefaultValue 없음 → 통과
            }

            // ── 값 검증 ───────────────────────────────────
            var err = ValidateValue(rule, raw);
            if (err is not null)
                errors.Add(new ValidationError(rule.Section, rule.Key, err, raw));
        }

        return new ValidationResult
        {
            Errors = errors,
            AppliedDefaults = defaults,
            RuleCount = schema.Count
        };
    }

    #endregion

    #region §2 ─ 유형별 검증

    private static string? ValidateValue(ConfigFieldRule rule, string raw)
    {
        // 커스텀 검증 우선
        if (rule.CustomValidator is not null)
            return rule.CustomValidator(raw);

        return rule.ValueType switch
        {
            ConfigValueType.String => ValidateString(rule, raw),
            ConfigValueType.NonEmptyString => string.IsNullOrWhiteSpace(raw)
                                              ? "비어있는 값은 허용되지 않습니다." : null,
            ConfigValueType.Int => ValidateInt(rule, raw),
            ConfigValueType.Long => ValidateLong(rule, raw),
            ConfigValueType.Double => ValidateDouble(rule, raw),
            ConfigValueType.Bool => ValidateBool(raw),
            ConfigValueType.IpAddress => ValidateIpAddress(raw),
            ConfigValueType.Port => ValidatePort(raw),
            ConfigValueType.SemVer => ValidateSemVer(raw),
            ConfigValueType.DirectoryPath => ValidatePath(raw, isFile: false),
            ConfigValueType.FilePath => ValidatePath(raw, isFile: true),
            ConfigValueType.Regex => ValidateRegex(rule, raw),
            ConfigValueType.Enum => ValidateEnum(rule, raw),
            ConfigValueType.Guid => ValidateGuid(raw),
            ConfigValueType.Cron => ValidateCron(raw),
            _ => null
        };
    }

    // ── String ────────────────────────────────────────────────
    private static string? ValidateString(ConfigFieldRule rule, string raw)
    {
        if (rule.MaxLength.HasValue && raw.Length > rule.MaxLength.Value)
            return $"최대 {rule.MaxLength}자를 초과했습니다 (현재 {raw.Length}자).";
        return null;
    }

    // ── Int ───────────────────────────────────────────────────
    private static string? ValidateInt(ConfigFieldRule rule, string raw)
    {
        if (!int.TryParse(raw, out var v))
            return $"정수 형식이 아닙니다: \"{raw}\"";
        return CheckRange(rule, v);
    }

    // ── Long ──────────────────────────────────────────────────
    private static string? ValidateLong(ConfigFieldRule rule, string raw)
    {
        if (!long.TryParse(raw, out var v))
            return $"정수(Long) 형식이 아닙니다: \"{raw}\"";
        return CheckRange(rule, v);
    }

    // ── Double ────────────────────────────────────────────────
    private static string? ValidateDouble(ConfigFieldRule rule, string raw)
    {
        if (!double.TryParse(raw,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
            return $"숫자 형식이 아닙니다: \"{raw}\"";
        return CheckRange(rule, v);
    }

    // ── Bool ──────────────────────────────────────────────────
    private static string? ValidateBool(string raw)
    {
        var up = raw.ToUpperInvariant();
        return up is "TRUE" or "FALSE" or "1" or "0" or "YES" or "NO" or "ON" or "OFF"
            ? null
            : $"불리언 형식이 아닙니다: \"{raw}\" (허용: true/false/1/0/yes/no/on/off)";
    }

    // ── IpAddress ─────────────────────────────────────────────
    private static string? ValidateIpAddress(string raw) =>
        IPAddress.TryParse(raw, out _) ? null : $"올바른 IP 주소 형식이 아닙니다: \"{raw}\"";

    // ── Port ──────────────────────────────────────────────────
    private static string? ValidatePort(string raw)
    {
        if (!int.TryParse(raw, out var v))
            return $"포트는 정수여야 합니다: \"{raw}\"";
        return v is < 1 or > 65535
            ? $"포트 범위(1~65535)를 벗어났습니다: {v}"
            : null;
    }

    // ── SemVer ────────────────────────────────────────────────
    private static readonly Regex _semVerRx =
        new(@"^\d+\.\d+\.\d+(-[\w.]+)?(\+[\w.]+)?$", RegexOptions.Compiled);

    private static string? ValidateSemVer(string raw) =>
        _semVerRx.IsMatch(raw) ? null : $"시맨틱 버전 형식이 아닙니다: \"{raw}\" (예: 1.2.3)";

    // ── Path ──────────────────────────────────────────────────
    private static string? ValidatePath(string raw, bool isFile)
    {
        try
        {
            var _ = isFile ? Path.GetFullPath(raw) : Path.GetFullPath(raw);
            // 유효하지 않은 문자 확인
            var invalid = isFile ? Path.GetInvalidPathChars() : Path.GetInvalidPathChars();
            if (raw.IndexOfAny(invalid) >= 0)
                return $"경로에 허용되지 않는 문자가 있습니다: \"{raw}\"";
            return null;
        }
        catch
        {
            return $"경로 형식이 올바르지 않습니다: \"{raw}\"";
        }
    }

    // ── Regex ─────────────────────────────────────────────────
    private static string? ValidateRegex(ConfigFieldRule rule, string raw)
    {
        if (string.IsNullOrEmpty(rule.Pattern))
            return "Pattern 이 지정되지 않았습니다.";
        return Regex.IsMatch(raw, rule.Pattern)
            ? null
            : $"패턴(\"{rule.Pattern}\")에 일치하지 않습니다: \"{raw}\"";
    }

    // ── Enum ──────────────────────────────────────────────────
    private static string? ValidateEnum(ConfigFieldRule rule, string raw)
    {
        if (rule.AllowedValues is null || rule.AllowedValues.Count == 0)
            return "AllowedValues 가 지정되지 않았습니다.";
        return rule.AllowedValues.Any(v => v.Equals(raw, StringComparison.OrdinalIgnoreCase))
            ? null
            : $"허용된 값이 아닙니다: \"{raw}\"  " +
              $"(허용: {string.Join(", ", rule.AllowedValues)})";
    }

    // ── Guid ──────────────────────────────────────────────────
    private static string? ValidateGuid(string raw) =>
        Guid.TryParse(raw, out _) ? null : $"GUID 형식이 아닙니다: \"{raw}\"";

    // ── Cron ──────────────────────────────────────────────────
    private static readonly Regex _cronRx =
        new(@"^(\S+\s){4}\S+(\s\S+)?$", RegexOptions.Compiled);

    private static string? ValidateCron(string raw) =>
        _cronRx.IsMatch(raw.Trim()) ? null : $"Cron 형식이 아닙니다: \"{raw}\" (예: 0 2 * * *)";

    // ── Range Helper ──────────────────────────────────────────
    private static string? CheckRange(ConfigFieldRule rule, double v)
    {
        if (rule.Min.HasValue && v < rule.Min.Value)
            return $"최솟값({rule.Min})보다 작습니다: {v}";
        if (rule.Max.HasValue && v > rule.Max.Value)
            return $"최댓값({rule.Max})보다 큽니다: {v}";
        return null;
    }

    #endregion
}