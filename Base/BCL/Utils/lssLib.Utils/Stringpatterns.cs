using System.Text.RegularExpressions;

namespace lssLib.Utils;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Utils — StringPatterns
//  [GeneratedRegex] 컴파일 타임 Regex 생성 전용 클래스.
//  ▸ 런타임 Regex 인스턴스 할당 없음
//  ▸ internal — StringExtensions에서만 참조
//  ▸ 반복 호출이 많은 패턴을 여기에 추가하세요.
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// <c>[GeneratedRegex]</c> 컴파일 타임 Regex 패턴 저장소.<br/>
/// 런타임에 Regex 인스턴스를 할당하지 않으며, 반복 호출 시 성능 저하가 없습니다.
/// </summary>
/// <remarks>
/// <para><b>사용 원칙</b></para>
/// <para>
/// 반복 호출이 잦은 Regex 패턴은 이 클래스에 <c>[GeneratedRegex]</c>로 등록하고,
/// <see cref="StringExtensions"/>에서 메서드를 통해 접근합니다.
/// 일회성 패턴은 <see cref="StringExtensions.IsMatch"/>를 사용하세요.
/// </para>
/// <para><b>새 패턴 추가 방법</b></para>
/// <code>
/// // StringPatterns.cs에 추가
/// [GeneratedRegex(@"^[A-Z]{2,3}$")]
/// internal static partial Regex IsoCountryPattern();
///
/// // StringExtensions.cs에 메서드 추가
/// public static bool IsIsoCountry(this string s)
///     => StringPatterns.IsoCountryPattern().IsMatch(s);
/// </code>
/// </remarks>
internal static partial class StringPatterns
{
    /// <summary>
    /// PascalCase/camelCase → snake_case 변환용 패턴.<br/>
    /// 소문자 뒤에 오는 대문자 앞에 언더스코어를 삽입합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // "MyDeviceName" → "_My_Device_Name" → "my_device_name"
    /// StringPatterns.SnakeCasePattern().Replace("MyDeviceName", "_$1");
    /// </code>
    /// </example>
    [GeneratedRegex("([A-Z])")]
    internal static partial Regex SnakeCasePattern();

    /// <summary>
    /// snake_case / kebab-case → PascalCase/camelCase 변환용 패턴.<br/>
    /// 언더스코어 또는 하이픈 뒤에 오는 첫 글자를 캡처합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // "sensor_reading" → "SensorReading" (PascalCase)
    /// StringPatterns.CamelCasePattern().Replace("sensor_reading",
    ///     m => m.Groups[1].Value.ToUpperInvariant());
    ///
    /// // "parse-http-body" → "parseHttpBody" (camelCase)
    /// // → ToCamelCase() 내부에서 사용
    /// </code>
    /// </example>
    [GeneratedRegex(@"[_\-]([a-zA-Z])")]
    internal static partial Regex CamelCasePattern();

    /// <summary>
    /// 순수 16진수 문자열 검증 패턴 (<c>[0-9A-Fa-f]+</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// StringPatterns.HexOnlyPattern().IsMatch("DEADBEEF");  // true
    /// StringPatterns.HexOnlyPattern().IsMatch("GG");        // false
    /// </code>
    /// </example>
    [GeneratedRegex(@"^[0-9A-Fa-f]+$")]
    internal static partial Regex HexOnlyPattern();

    /// <summary>
    /// 순수 숫자 문자열 검증 패턴 (<c>[0-9]+</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// "12345".IsDigitsOnly();  // true  (StringExtensions 경유)
    /// "12a45".IsDigitsOnly();  // false
    /// </code>
    /// </example>
    [GeneratedRegex(@"^\d+$")]
    internal static partial Regex DigitsOnlyPattern();

    /// <summary>
    /// 기본 이메일 형식 검증 패턴.
    /// </summary>
    /// <example>
    /// <code>
    /// "user@example.com".IsEmail();  // true  (StringExtensions 경유)
    /// "not-an-email".IsEmail();      // false
    /// </code>
    /// </example>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    internal static partial Regex EmailPattern();
}