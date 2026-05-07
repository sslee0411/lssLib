
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace lssLib.Utils;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Utils — StringExtensions
//  문자열 조작 · 변환 · 파싱 · 인코딩 확장 메서드
//  ▸ No abstractions  ▸ Extension-method only
//  ▸ Regex → StringPatterns.[GeneratedRegex] 위임 (zero alloc)
//  ▸ BCL 래퍼(IsNullOrEmpty 등) 제거 → HasValue / OrDefault 단일화
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 문자열 확장 메서드 모음.<br/>
/// Regex 계열은 <see cref="StringPatterns"/> 컴파일 타임 생성을 활용합니다.
/// </summary>
public static class StringExtensions
{
    // ─────────────────────────────────────────────
    // §1  값 존재 판단
    // ─────────────────────────────────────────────

    /// <summary>
    /// <c>null</c>이 아니고 공백이 아닌 실제 값이 있으면 <c>true</c>를 반환합니다.<br/>
    /// <c>string.IsNullOrWhiteSpace(s)</c>의 부정을 읽기 좋게 캡슐화합니다.
    /// </summary>
    /// <param name="s">검사할 문자열.</param>
    /// <returns>실질적 값이 있으면 <c>true</c>.</returns>
    /// <example>
    /// <code>
    /// "hello".HasValue()          // true
    /// "  ".HasValue()             // false
    /// ((string?)null).HasValue()  // false
    ///
    /// // LINQ 필터와 조합
    /// var validNames = items
    ///     .Select(x => x.Label)
    ///     .Where(n => n.HasValue())
    ///     .ToList();
    /// </code>
    /// </example>
    public static bool HasValue(this string? s) => !string.IsNullOrWhiteSpace(s);

    /// <summary>
    /// 값이 없으면(<c>null</c> 또는 공백) <paramref name="fallback"/>을 반환합니다.
    /// </summary>
    /// <param name="s">원본 문자열.</param>
    /// <param name="fallback">대체값. 기본값은 <c>""</c>.</param>
    /// <returns>실질적 값이 있으면 원본, 없으면 <paramref name="fallback"/>.</returns>
    /// <example>
    /// <code>
    /// // 설정 파일 기본값 처리
    /// string host   = ini["host"].OrDefault("localhost");
    /// string logDir = ini["log_dir"].OrDefault(@"logs");
    ///
    /// // 빈 문자열 반환
    /// ((string?)null).OrDefault()  // ""
    /// </code>
    /// </example>
    public static string OrDefault(this string? s, string fallback = "")
        => s.HasValue() ? s! : fallback;

    // ─────────────────────────────────────────────
    // §2  케이스 · 포맷 변환
    // ─────────────────────────────────────────────

    /// <summary>
    /// <c>PascalCase</c> / <c>camelCase</c> → <c>snake_case</c> 변환.<br/>
    /// 컴파일 타임 <see cref="StringPatterns.SnakeCasePattern"/> Regex 사용으로 런타임 할당이 없습니다.
    /// </summary>
    /// <param name="s">변환할 문자열.</param>
    /// <returns><c>snake_case</c> 문자열.</returns>
    /// <example>
    /// <code>
    /// "SensorReading".ToSnakeCase()      // "sensor_reading"
    /// "parseHTTPResponse".ToSnakeCase()  // "parse_h_t_t_p_response"
    ///
    /// // BufSchema 필드명 → DB 컬럼명 자동 변환
    /// var columnName = schema.FieldName.ToSnakeCase();
    /// </code>
    /// </example>
    public static string ToSnakeCase(this string s)
        => StringPatterns.SnakeCasePattern().Replace(s, "_$1").ToLowerInvariant();

    /// <summary>
    /// <c>snake_case</c> / <c>kebab-case</c> → <c>camelCase</c> 변환.
    /// </summary>
    /// <param name="s">변환할 문자열.</param>
    /// <returns><c>camelCase</c> 문자열.</returns>
    /// <example>
    /// <code>
    /// "sensor_reading".ToCamelCase()  // "sensorReading"
    /// "parse-http-body".ToCamelCase() // "parseHttpBody"
    ///
    /// // BufSchema 필드명 → JSON 키 자동 변환
    /// var jsonKey = schema.FieldName.ToCamelCase();
    /// </code>
    /// </example>
    public static string ToCamelCase(this string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        // snake_case / kebab-case → camelCase
        // 언더스코어·하이픈 뒤 첫 글자를 대문자로 치환 후 구분자 제거
        var result = StringPatterns.CamelCasePattern()
            .Replace(s.ToLowerInvariant(), m => m.Groups[1].Value.ToUpperInvariant());
        return result;
    }

    /// <summary>
    /// 첫 글자를 대문자로 변환합니다. 나머지 글자는 원본을 유지합니다.
    /// </summary>
    /// <param name="s">변환할 문자열.</param>
    /// <returns>첫 글자가 대문자인 문자열.</returns>
    /// <example>
    /// <code>
    /// "hello world".Capitalize()  // "Hello world"
    /// "STX frame".Capitalize()    // "STX frame"
    /// "".Capitalize()             // ""
    /// </code>
    /// </example>
    public static string Capitalize(this string s)
        => string.IsNullOrEmpty(s) ? s ?? string.Empty
           : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>
    /// 지정 길이를 초과하면 잘라내고 접미사를 추가합니다.
    /// </summary>
    /// <param name="s">원본 문자열.</param>
    /// <param name="maxLength">최대 허용 길이 (접미사 제외).</param>
    /// <param name="suffix">접미사. 기본값은 <c>"…"</c>.</param>
    /// <returns>길이가 조정된 문자열.</returns>
    /// <example>
    /// <code>
    /// "SensorTemperatureReading".Truncate(10)          // "SensorTemp…"
    /// "SensorTemperatureReading".Truncate(10, "...")   // "SensorTemp..."
    /// "Short".Truncate(10)                             // "Short"  (변환 없음)
    ///
    /// // UI 에러 메시지 80자 제한
    /// errorMsg.Truncate(80)
    /// </code>
    /// </example>
    public static string Truncate(this string s, int maxLength, string suffix = "…")
        => s.Length <= maxLength ? s : s[..maxLength] + suffix;

    /// <summary>
    /// 문자열을 <paramref name="count"/>회 반복 연결합니다.
    /// </summary>
    /// <param name="s">반복할 문자열.</param>
    /// <param name="count">반복 횟수. 0 이하이면 빈 문자열 반환.</param>
    /// <returns>반복 연결된 문자열.</returns>
    /// <example>
    /// <code>
    /// "─".Repeat(40)   // "────────────────────────────────────────"
    /// "AB".Repeat(3)   // "ABABAB"
    /// "x".Repeat(0)    // ""
    /// </code>
    /// </example>
    public static string Repeat(this string s, int count)
        => count <= 0 ? string.Empty : string.Concat(Enumerable.Repeat(s, count));

    /// <summary>
    /// 지정 너비가 될 때까지 왼쪽을 패딩합니다.
    /// </summary>
    /// <param name="s">원본 문자열.</param>
    /// <param name="totalWidth">목표 전체 너비.</param>
    /// <param name="padChar">패딩 문자. 기본값은 공백.</param>
    /// <returns>패딩된 문자열.</returns>
    /// <example>
    /// <code>
    /// "42".PadLeftTo(8)         // "      42"
    /// "7".PadLeftTo(4, '0')     // "0007"
    ///
    /// // 고정폭 프레임 ID 출력
    /// frameId.ToString().PadLeftTo(8, '0')  // "00001024"
    /// </code>
    /// </example>
    public static string PadLeftTo(this string s, int totalWidth, char padChar = ' ')
        => s.PadLeft(totalWidth, padChar);

    // ─────────────────────────────────────────────
    // §3  검색 · 비교 (대소문자 무시)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 대소문자를 무시하고 <paramref name="value"/>를 포함하는지 검사합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "Connection: Keep-Alive".ContainsIgnoreCase("keep-alive")  // true
    /// header.ContainsIgnoreCase("content-type")
    /// </code>
    /// </example>
    public static bool ContainsIgnoreCase(this string s, string value)
        => s.Contains(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 대소문자를 무시하고 <paramref name="value"/>로 시작하는지 검사합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "HTTP/1.1 200 OK".StartsWithIgnoreCase("http")  // true
    /// portName.StartsWithIgnoreCase("COM")
    /// </code>
    /// </example>
    public static bool StartsWithIgnoreCase(this string s, string value)
        => s.StartsWith(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 대소문자를 무시하고 <paramref name="value"/>로 끝나는지 검사합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "frame.BIN".EndsWithIgnoreCase(".bin")  // true
    /// </code>
    /// </example>
    public static bool EndsWithIgnoreCase(this string s, string value)
        => s.EndsWith(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 대소문자를 무시하고 <paramref name="other"/>와 같은지 비교합니다.<br/>
    /// null-safe: 양쪽 모두 <c>null</c>이면 <c>true</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// "OK".EqualsIgnoreCase("ok")                  // true
    /// ((string?)null).EqualsIgnoreCase(null)        // true
    /// "OK".EqualsIgnoreCase(null)                   // false
    /// </code>
    /// </example>
    public static bool EqualsIgnoreCase(this string? s, string? other)
        => string.Equals(s, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="candidates"/> 중 하나와 대소문자를 무시하고 일치하면 <c>true</c>를 반환합니다.
    /// </summary>
    /// <param name="s">원본 문자열.</param>
    /// <param name="candidates">비교 후보 목록.</param>
    /// <returns>후보 중 하나와 일치하면 <c>true</c>.</returns>
    /// <example>
    /// <code>
    /// status.IsAnyOf("ok", "success", "done")  // HTTP 상태 처리
    /// ext.IsAnyOf(".bin", ".dat", ".dump")      // 바이너리 파일 필터
    ///
    /// // 파일 확장자 분기
    /// if (path.GetExt().IsAnyOf(".bin", ".dat"))
    ///     ProcessBinary(path);
    /// </code>
    /// </example>
    public static bool IsAnyOf(this string s, params string[] candidates)
        => candidates.Any(c => s.EqualsIgnoreCase(c));

    // ─────────────────────────────────────────────
    // §4  안전 파싱
    //     실패 시 null 반환. 예외 throw 없음.
    // ─────────────────────────────────────────────

    /// <summary>
    /// 문자열을 <c>int</c>로 파싱합니다. 실패 시 <c>null</c>을 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "123".ToIntOrNull()    // 123
    /// "abc".ToIntOrNull()    // null  (예외 없음)
    ///
    /// // 설정 파일 파싱
    /// int port = ini["port"].ToIntOrNull() ?? 8080;
    /// </code>
    /// </example>
    public static int? ToIntOrNull(this string? s)
        => int.TryParse(s, out var v) ? v : null;

    /// <summary>
    /// 문자열을 <c>long</c>으로 파싱합니다. 실패 시 <c>null</c>을 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "-1".ToLongOrNull()    // -1L
    /// long maxSize = ini["max_size"].ToLongOrNull() ?? 65536L;
    /// </code>
    /// </example>
    public static long? ToLongOrNull(this string? s)
        => long.TryParse(s, out var v) ? v : null;

    /// <summary>
    /// 문자열을 <c>double</c>로 파싱합니다 (<c>InvariantCulture</c> 고정).<br/>
    /// 지역 설정에 무관하게 <c>"3.14"</c>가 항상 올바르게 파싱됩니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "3.14".ToDoubleOrNull()    // 3.14
    /// "1,234".ToDoubleOrNull()   // null  (천 단위 구분자 미지원)
    ///
    /// // SmoothStep alpha 파라미터
    /// float alpha = (float)(ini["smooth_alpha"].ToDoubleOrNull() ?? 0.15);
    /// </code>
    /// </example>
    public static double? ToDoubleOrNull(this string? s)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// 문자열을 <c>decimal</c>로 파싱합니다 (<c>InvariantCulture</c> 고정, 28~29자리 정밀 보존).<br/>
    /// lssLib.Binary의 <c>DecimalLE</c> 16바이트 직렬화 파이프라인과 정밀도가 일치합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "9.999".ToDecimalOrNull()    // 9.999m
    /// "1234.56".ToDecimalOrNull()  // 1234.56m  (금융 데이터)
    ///
    /// // lssLib.Binary decimal 역직렬화 후 비교
    /// decimal expected = "123.456".ToDecimalOrNull() ?? 0m;
    /// </code>
    /// </example>
    public static decimal? ToDecimalOrNull(this string? s)
        => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// 문자열을 <c>bool</c>로 유연하게 파싱합니다.<br/>
    /// 인식 값: <c>true/false · 1/0 · yes/no · on/off</c> (대소문자 무시).
    /// </summary>
    /// <returns>
    /// <c>true</c>: <c>true, 1, yes, on</c>;<br/>
    /// <c>false</c>: <c>false, 0, no, off</c>;<br/>
    /// <c>null</c>: 인식할 수 없는 값.
    /// </returns>
    /// <example>
    /// <code>
    /// "yes".ToBoolOrNull()     // true
    /// "on".ToBoolOrNull()      // true
    /// "1".ToBoolOrNull()       // true
    /// "false".ToBoolOrNull()   // false
    /// "abc".ToBoolOrNull()     // null
    ///
    /// // UI 체크박스 설정 읽기
    /// bool autoSave = ini["auto_save"].ToBoolOrNull() ?? true;
    /// bool debug    = ini["debug"].ToBoolOrNull()     ?? false;
    /// </code>
    /// </example>
    public static bool? ToBoolOrNull(this string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s!.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => null
        };
    }

    // ─────────────────────────────────────────────
    // §5  인코딩 · 바이트 변환
    //     lssLib.Binary byte[] 파이프라인 연계 설계
    // ─────────────────────────────────────────────

    /// <summary>
    /// 문자열을 UTF-8 바이트 배열로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // lssLib.Binary 파이프라인 연계
    /// byte[] raw = "FRAME_HEADER".ToUtf8Bytes();
    ///
    /// // CRC 계산 (lssLib.Extensions.CrcExtensions)
    /// string json = schema.ToJson();
    /// uint   etag = json.ToUtf8Bytes().ComputeCrc32();
    /// </code>
    /// </example>
    public static byte[] ToUtf8Bytes(this string s) => Encoding.UTF8.GetBytes(s);

    /// <summary>
    /// UTF-8 바이트 배열을 문자열로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// byte[] raw = "FRAME_HEADER".ToUtf8Bytes();
    /// string str = raw.ToUtf8String();  // "FRAME_HEADER"
    /// </code>
    /// </example>
    public static string ToUtf8String(this byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// 문자열을 Base64로 인코딩합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "lssLib.Utils v2".ToBase64()  // "bHNzTGliLlV0aWxzIHYy"
    ///
    /// // HTTP Basic 인증 헤더
    /// string auth = $"Basic {$"{user}:{pass}".ToBase64()}";
    /// </code>
    /// </example>
    public static string ToBase64(this string s)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    /// <summary>
    /// Base64 문자열을 원본 문자열로 디코딩합니다. 실패 시 <c>null</c>을 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "aGVsbG8=".FromBase64()   // "hello"
    /// "invalid!!".FromBase64()  // null  (예외 없음)
    /// </code>
    /// </example>
    public static string? FromBase64(this string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch { return null; }
    }

    /// <summary>
    /// 바이트 배열을 16진수 문자열로 변환합니다.<br/>
    /// <paramref name="spaced"/>=<c>true</c>이면 lssLib.Binary HexDump 포맷(<c>"AA BB CC"</c>)과 동일합니다.
    /// </summary>
    /// <param name="bytes">변환할 바이트 배열.</param>
    /// <param name="spaced">바이트 사이 공백 삽입 여부.</param>
    /// <returns>16진수 문자열.</returns>
    /// <example>
    /// <code>
    /// byte[] frame = { 0x02, 0xDE, 0xAD, 0xBE, 0xEF, 0x03 };
    ///
    /// frame.ToHex()              // "02DEADBEEF03"
    /// frame.ToHex(spaced: true)  // "02 DE AD BE EF 03"
    ///
    /// // 시리얼 수신 로그 (lssLib.Binary 덤프 포맷 동일)
    /// logger.Debug($"Frame: {raw.ToHex(spaced: true)}");
    /// </code>
    /// </example>
    public static string ToHex(this byte[] bytes, bool spaced = false)
        => spaced
            ? string.Join(" ", bytes.Select(b => b.ToString("X2")))
            : BitConverter.ToString(bytes).Replace("-", "");

    /// <summary>
    /// 16진수 문자열을 바이트 배열로 변환합니다.<br/>
    /// 공백과 하이픈은 자동으로 제거됩니다.
    /// </summary>
    /// <param name="hex">변환할 16진수 문자열 (<c>"DEADBEEF"</c> 또는 <c>"DE AD BE EF"</c>).</param>
    /// <returns>바이트 배열.</returns>
    /// <exception cref="FormatException">유효하지 않은 16진수 문자가 포함된 경우.</exception>
    /// <example>
    /// <code>
    /// "DE AD BE EF".FromHex()   // byte[] { 0xDE, 0xAD, 0xBE, 0xEF }
    /// "DEADBEEF".FromHex()      // 동일
    /// "GG".FromHex()            // FormatException
    ///
    /// // 시리얼 HEX 수신 → BufferParser 전달
    /// string hexLine = serialPort.ReadLine().Trim();
    /// byte[] data    = hexLine.FromHex();
    /// var    parser  = new BufferParser(Guard.NotEmpty(data));
    /// </code>
    /// </example>
    public static byte[] FromHex(this string hex)
    {
        var clean = hex.Replace(" ", "").Replace("-", "");
        if (clean.Length % 2 != 0 || !StringPatterns.HexOnlyPattern().IsMatch(clean))
            throw new FormatException($"유효하지 않은 HEX 문자열: '{hex}'");
        return Enumerable.Range(0, clean.Length / 2)
                         .Select(i => Convert.ToByte(clean.Substring(i * 2, 2), 16))
                         .ToArray();
    }

    // ─────────────────────────────────────────────
    // §6  정규식 유틸
    // ─────────────────────────────────────────────

    /// <summary>
    /// 문자열이 숫자(<c>0-9</c>)로만 구성되어 있는지 검사합니다.<br/>
    /// <see cref="StringPatterns.DigitsOnlyPattern"/> (컴파일 타임 Regex)을 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "12345".IsDigitsOnly()   // true
    /// "12a45".IsDigitsOnly()   // false
    /// "".IsDigitsOnly()        // false
    /// </code>
    /// </example>
    public static bool IsDigitsOnly(this string s)
        => StringPatterns.DigitsOnlyPattern().IsMatch(s);

    /// <summary>
    /// 기본 이메일 형식인지 검사합니다.<br/>
    /// <see cref="StringPatterns.EmailPattern"/> (컴파일 타임 Regex)을 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "user@example.com".IsEmail()    // true
    /// "not-an-email".IsEmail()        // false
    /// </code>
    /// </example>
    public static bool IsEmail(this string s)
        => StringPatterns.EmailPattern().IsMatch(s);

    /// <summary>
    /// 임의 정규식 패턴과 일치하는지 검사합니다.<br/>
    /// 반복 호출이 많으면 <see cref="StringPatterns"/>에 <c>[GeneratedRegex]</c> 등록을 권장합니다.
    /// </summary>
    /// <param name="s">검사할 문자열.</param>
    /// <param name="pattern">정규식 패턴.</param>
    /// <param name="options">정규식 옵션.</param>
    /// <returns>패턴과 일치하면 <c>true</c>.</returns>
    /// <example>
    /// <code>
    /// "2024-04-01".IsMatch(@"^\d{4}-\d{2}-\d{2}$")  // true
    /// "FRAME_001".IsMatch(@"^FRAME_\d+$")             // true
    /// </code>
    /// </example>
    public static bool IsMatch(this string s, string pattern,
        RegexOptions options = RegexOptions.None)
        => Regex.IsMatch(s, pattern, options);

    /// <summary>
    /// 정규식의 첫 번째 캡처 그룹 값을 반환합니다. 일치하지 않으면 <c>null</c>.
    /// </summary>
    /// <param name="s">검색할 문자열.</param>
    /// <param name="pattern">캡처 그룹을 포함한 정규식 패턴.</param>
    /// <param name="groupIndex">반환할 그룹 인덱스 (기본값: 1번째 캡처 그룹).</param>
    /// <param name="options">정규식 옵션.</param>
    /// <returns>캡처 그룹 값 또는 <c>null</c>.</returns>
    /// <example>
    /// <code>
    /// "version: 2.0.0".MatchGroup(@"version: (\S+)")  // "2.0.0"
    /// "Error E1042".MatchGroup(@"E(\d+)")              // "1042"
    /// "no match".MatchGroup(@"(\d+)")                  // null
    ///
    /// // 장치 응답 파싱
    /// string response = "STATUS=READY;TEMP=42.5;CH=3";
    /// string? status  = response.MatchGroup(@"STATUS=(\w+)");   // "READY"
    /// string? tempStr = response.MatchGroup(@"TEMP=([\d.]+)");  // "42.5"
    /// double  temp    = tempStr?.ToDoubleOrNull() ?? 0.0;
    /// </code>
    /// </example>
    public static string? MatchGroup(this string s, string pattern,
        int groupIndex = 1, RegexOptions options = RegexOptions.None)
    {
        var m = Regex.Match(s, pattern, options);
        return m.Success && m.Groups.Count > groupIndex ? m.Groups[groupIndex].Value : null;
    }

    /// <summary>
    /// CR / LF / CRLF 모두를 구분자로 줄 단위 분할합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "line1\r\nline2\nline3".ToLines()
    /// // → ["line1", "line2", "line3"]
    ///
    /// // 멀티라인 INI 파싱
    /// var settings = iniText.ToLines()
    ///     .Where(l => l.ContainsIgnoreCase("="))
    ///     .ToDictionary(...);
    /// </code>
    /// </example>
    public static IEnumerable<string> ToLines(this string s)
        => s.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

    /// <summary>
    /// 빈 줄을 제거하고 내용이 있는 줄만 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// "line1\n\nline3".ToNonEmptyLines()
    /// // → ["line1", "line3"]
    ///
    /// // 주석 및 빈 줄 제거 후 설정 파싱
    /// var validLines = raw.ToNonEmptyLines()
    ///     .Where(l => !l.StartsWithIgnoreCase("#"));
    /// </code>
    /// </example>
    public static IEnumerable<string> ToNonEmptyLines(this string s)
        => s.ToLines().Where(l => l.HasValue());
}