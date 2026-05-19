// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.InfluxDB · LineProtocolBuilder.cs
//  역할: InfluxDB Line Protocol 문자열 빌더
//        measurement / tag / field / timestamp 조립
//
//  Line Protocol 형식:
//    measurementName,tag1=val1,tag2=val2 field1=val1,field2=val2 timestamp
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Text;
using lssLib.DB.Core;

namespace lssLib.DB.InfluxDB;

/// <summary>
/// InfluxDB Line Protocol 문자열 빌더.
/// 단일 Point 또는 배치 Point 목록을 Line Protocol 형식으로 조립한다.
/// </summary>
/// <example><code>
/// // 단일 Point 빌드
/// string line = new LineProtocolBuilder("sensor_data")
///     .Tag("plant",   "A01")
///     .Tag("line",    "L1")
///     .Field("temperature", 72.5)
///     .Field("pressure",    1.013)
///     .Timestamp(DateTime.UtcNow)
///     .Build();
/// // → sensor_data,plant=A01,line=L1 temperature=72.5,pressure=1.013 1716115200000000000
///
/// // 배치 빌드
/// var points = sensors.Select(s =>
///     new LineProtocolBuilder("sensor_data")
///         .Tag("id", s.SensorId.ToString())
///         .Field("value", s.Value)
///         .Timestamp(s.Time)
///         .Build());
/// string batch = LineProtocolBuilder.BuildBatch(points);
/// </code></example>
public sealed class LineProtocolBuilder
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly string _measurement;
    private readonly List<(string k, string v)> _tags = [];
    private readonly List<string> _fields = [];
    private long? _timestamp;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="measurement">Measurement 이름 (테이블 개념).</param>
    /// <exception cref="DbException">이름이 비어 있을 때.</exception>
    public LineProtocolBuilder(string measurement)
    {
        if (string.IsNullOrWhiteSpace(measurement))
            throw DbException.LineProtocolError("measurement 이름이 비어 있습니다.");

        _measurement = EscapeKey(measurement);
    }

    // §3 ─ Tag (인덱싱 문자열 — DbParamType.InfluxTag 대응)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tag 추가. Tag는 인덱싱되므로 GROUP BY / 필터 기준 값에 사용한다.
    /// </summary>
    /// <param name="key">Tag 키.</param>
    /// <param name="value">Tag 값 (빈 문자열이면 추가하지 않음).</param>
    public LineProtocolBuilder Tag(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            _tags.Add((EscapeKey(key), EscapeTagValue(value)));
        return this;
    }

    // §4 ─ Field (측정값 — DbParamType.InfluxField 대응)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Float / Double Field 추가.</summary>
    public LineProtocolBuilder Field(string key, double value)
    {
        ValidateKey(key);
        _fields.Add($"{EscapeKey(key)}={value.ToString("G", CultureInfo.InvariantCulture)}");
        return this;
    }

    /// <summary>Integer Field 추가 (접미사 i 필수).</summary>
    public LineProtocolBuilder Field(string key, long value)
    {
        ValidateKey(key);
        _fields.Add($"{EscapeKey(key)}={value}i");
        return this;
    }

    /// <summary>Integer Field 추가 (int → long 자동 변환).</summary>
    public LineProtocolBuilder Field(string key, int value)
        => Field(key, (long)value);

    /// <summary>String Field 추가 (쌍따옴표로 감쌈).</summary>
    public LineProtocolBuilder Field(string key, string value)
    {
        ValidateKey(key);
        _fields.Add($"{EscapeKey(key)}=\"{EscapeFieldStringValue(value)}\"");
        return this;
    }

    /// <summary>Boolean Field 추가.</summary>
    public LineProtocolBuilder Field(string key, bool value)
    {
        ValidateKey(key);
        _fields.Add($"{EscapeKey(key)}={(value ? "true" : "false")}");
        return this;
    }

    /// <summary>
    /// object 타입 자동 분기 Field 추가.
    /// double / long / int / bool / string 자동 판별.
    /// </summary>
    public LineProtocolBuilder Field(string key, object? value)
    {
        return value switch
        {
            null => this,
            double d => Field(key, d),
            float f => Field(key, (double)f),
            long l => Field(key, l),
            int i => Field(key, (long)i),
            short s => Field(key, (long)s),
            bool b => Field(key, b),
            string str => Field(key, str),
            _ => Field(key, value.ToString() ?? string.Empty),
        };
    }

    // §5 ─ Timestamp (DbParamType.InfluxTimestamp 대응)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Timestamp 설정 (DateTime → 나노초 Unix timestamp).
    /// InfluxDB v2.0 기본 precision: nanosecond.
    /// </summary>
    /// <param name="utcTime">UTC 시각. Kind가 Local이면 UTC로 자동 변환.</param>
    public LineProtocolBuilder Timestamp(DateTime utcTime)
    {
        var t = utcTime.Kind == DateTimeKind.Local
            ? utcTime.ToUniversalTime()
            : utcTime;
        // Unix epoch 기준 나노초 변환
        _timestamp = (long)(t - DateTime.UnixEpoch).TotalMilliseconds * 1_000_000L;
        return this;
    }

    /// <summary>Timestamp 설정 (DateTimeOffset).</summary>
    public LineProtocolBuilder Timestamp(DateTimeOffset time)
    {
        _timestamp = time.ToUnixTimeMilliseconds() * 1_000_000L;
        return this;
    }

    /// <summary>Timestamp 설정 (나노초 Unix timestamp 직접 지정).</summary>
    public LineProtocolBuilder Timestamp(long nanoseconds)
    {
        _timestamp = nanoseconds;
        return this;
    }

    // §6 ─ Build
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Line Protocol 문자열을 생성합니다.
    /// </summary>
    /// <exception cref="DbException">Field가 하나도 없을 때.</exception>
    public string Build()
    {
        if (_fields.Count == 0)
            throw DbException.LineProtocolError(
                $"Field가 없습니다. measurement={_measurement}");

        var sb = new StringBuilder(_measurement);

        // Tags (정렬 권장 — InfluxDB 최적화)
        if (_tags.Count > 0)
        {
            var sorted = _tags.OrderBy(t => t.k);
            sb.Append(',');
            sb.Append(string.Join(",", sorted.Select(t => $"{t.k}={t.v}")));
        }

        // Fields
        sb.Append(' ');
        sb.Append(string.Join(",", _fields));

        // Timestamp
        if (_timestamp.HasValue)
        {
            sb.Append(' ');
            sb.Append(_timestamp.Value);
        }

        return sb.ToString();
    }

    // §7 ─ 배치 빌드
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 여러 Line Protocol 문자열을 개행 문자로 연결해 배치 쓰기 문자열을 생성합니다.
    /// </summary>
    /// <param name="lines">Line Protocol 문자열 목록.</param>
    /// <returns>개행 구분 배치 문자열.</returns>
    public static string BuildBatch(IEnumerable<string> lines)
        => string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));

    // §8 ─ 이스케이프 유틸리티
    // ─────────────────────────────────────────────────────────────────
    // Line Protocol 이스케이프 규칙
    //   키(measurement/tag key/field key) : 공백 → \  , 쉼표 → \, 등호 → \=
    //   Tag 값                            : 공백 → \  , 쉼표 → \, 등호 → \=
    //   Field 문자열 값                   : 쌍따옴표 → \"  역슬래시 → \\

    private static string EscapeKey(string s) => s
        .Replace(" ", @"\ ")
        .Replace(",", @"\,")
        .Replace("=", @"\=");

    private static string EscapeTagValue(string s) => s
        .Replace(" ", @"\ ")
        .Replace(",", @"\,")
        .Replace("=", @"\=");

    private static string EscapeFieldStringValue(string s) => s
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw DbException.LineProtocolError("Field 키가 비어 있습니다.");
    }
}