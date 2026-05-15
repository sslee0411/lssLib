using System.Globalization;

namespace lssLib.Utils;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Utils — DateTimeExtensions
//  DateTime / TimeSpan 포맷 · 변환 · 범위 판단
//  ▸ No abstractions  ▸ Extension-method only
//  ▸ 모든 포맷: CultureInfo.InvariantCulture 고정
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// <see cref="DateTime"/> · <see cref="TimeSpan"/> 확장 메서드 모음.<br/>
/// 모든 포맷 메서드는 <c>CultureInfo.InvariantCulture</c>를 사용하여
/// 지역 설정에 무관하게 동일한 결과를 보장합니다.
/// </summary>
public static class DateTimeExtensions
{
    // ─────────────────────────────────────────────
    // §1  표준 포맷 문자열
    // ─────────────────────────────────────────────

    /// <summary>
    /// <c>DateTime</c>을 <c>"yyyy-MM-dd"</c> ISO 날짜 문자열로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// DateTime.Today.ToIsoDate()  // "2024-04-01"
    ///
    /// // 날짜별 로그 디렉터리명
    /// string logDir = Path.Combine("logs", DateTime.Today.ToIsoDate());
    /// // → "logs/2024-04-01"
    /// </code>
    /// </example>
    public static string ToIsoDate(this DateTime dt)
        => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>DateTime</c>을 <c>"HH:mm:ss"</c> 시각 문자열로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// DateTime.Now.ToTimeString()  // "14:30:00"
    /// </code>
    /// </example>
    public static string ToTimeString(this DateTime dt)
        => dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>DateTime</c>을 <c>"yyyy-MM-dd HH:mm:ss"</c> 로그용 타임스탬프로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// DateTime.Now.ToIsoDateTime()  // "2024-04-01 14:30:00"
    ///
    /// // 로그 파일 기록
    /// logPath.AppendLine($"[{DateTime.Now.ToIsoDateTime()}] INFO 서버 시작");
    /// </code>
    /// </example>
    public static string ToIsoDateTime(this DateTime dt)
        => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>DateTime</c>을 <c>"yyyy-MM-ddTHH:mm:ss.fffZ"</c> ISO 8601 UTC 문자열로 변환합니다.<br/>
    /// REST API 응답 및 JSON 직렬화에 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// DateTime.UtcNow.ToIso8601Utc()  // "2024-04-01T14:30:00.000Z"
    ///
    /// // REST API payload
    /// var body = new { timestamp = DateTime.UtcNow.ToIso8601Utc() };
    /// </code>
    /// </example>
    public static string ToIso8601Utc(this DateTime dt)
        => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>DateTime</c>을 <c>"yyyyMMdd_HHmmss"</c> 파일명 안전 타임스탬프로 변환합니다.<br/>
    /// lssLib 세션 덤프·로그 파일 네이밍 규칙과 일치합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// DateTime.Now.ToFileStamp()  // "20240401_143000"
    ///
    /// // 세션 덤프 파일명
    /// string dumpPath = $"dumps/session_{DateTime.Now.ToFileStamp()}.bin";
    /// // → "dumps/session_20240401_143000.bin"
    /// </code>
    /// </example>
    public static string ToFileStamp(this DateTime dt)
        => dt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>DateTime</c>을 <c>"yyyyMMddHHmmssfff"</c> 밀리초 포함 정밀 스탬프로 변환합니다.<br/>
    /// 프레임 단위 고유 ID 및 분산 트레이스 ID 생성에 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// DateTime.Now.ToMsStamp()  // "20240401143000123"
    ///
    /// // 프레임별 고유 ID
    /// string frameId = $"frame_{DateTime.Now.ToMsStamp()}";
    ///
    /// // 분산 트레이스 ID
    /// string traceId = $"{DateTime.Now.ToMsStamp()}_{Guid.NewGuid():N}";
    /// </code>
    /// </example>
    public static string ToMsStamp(this DateTime dt)
        => dt.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

    // ─────────────────────────────────────────────
    // §2  Unix Epoch 양방향 변환
    // ─────────────────────────────────────────────

    private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Unix 타임스탬프(초)를 <see cref="DateTime"/> UTC로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // IoT 센서 프레임 헤더의 Unix 타임스탬프 수신
    /// long   unixSec  = parser.Read<long>(BufType.Int64LE);
    /// DateTime recvAt = unixSec.FromUnixSeconds();
    /// logger.Info($"[{recvAt.ToIsoDateTime()}] 수신");
    /// </code>
    /// </example>
    public static DateTime FromUnixSeconds(this long s) => _epoch.AddSeconds(s);

    /// <summary>
    /// Unix 타임스탬프(밀리초)를 <see cref="DateTime"/> UTC로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// long     unixMs  = 1712000000000L;
    /// DateTime recvAt  = unixMs.FromUnixMilliseconds();
    /// </code>
    /// </example>
    public static DateTime FromUnixMilliseconds(this long ms) => _epoch.AddMilliseconds(ms);

    /// <summary>
    /// <see cref="DateTime"/>을 Unix 타임스탬프(초)로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // REST API 범위 조회
    /// long from = DateTime.Today.StartOfMonth().ToUnixSeconds();
    /// long to   = DateTime.Today.EndOfMonth().ToUnixSeconds();
    /// var  data = await api.GetAsync(from, to);
    /// </code>
    /// </example>
    public static long ToUnixSeconds(this DateTime dt)
        => (long)(dt.ToUniversalTime() - _epoch).TotalSeconds;

    /// <summary>
    /// <see cref="DateTime"/>을 Unix 타임스탬프(밀리초)로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// long ts = DateTime.UtcNow.ToUnixMilliseconds();
    /// </code>
    /// </example>
    public static long ToUnixMilliseconds(this DateTime dt)
        => (long)(dt.ToUniversalTime() - _epoch).TotalMilliseconds;

    // ─────────────────────────────────────────────
    // §3  날짜 경계 / 범위 판단
    // ─────────────────────────────────────────────

    /// <summary>
    /// 해당 날짜의 자정 <c>00:00:00.0000000</c>을 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 일별 집계 쿼리
    /// var daily = await db.QueryAsync(
    ///     "SELECT * FROM logs WHERE ts BETWEEN @from AND @to",
    ///     new { from = DateTime.Today.StartOfDay(),
    ///           to   = DateTime.Today.EndOfDay() });
    /// </code>
    /// </example>
    public static DateTime StartOfDay(this DateTime dt) => dt.Date;

    /// <summary>
    /// 해당 날짜의 마지막 틱 <c>23:59:59.9999999</c>을 반환합니다.
    /// </summary>
    public static DateTime EndOfDay(this DateTime dt) => dt.Date.AddDays(1).AddTicks(-1);

    /// <summary>
    /// 해당 주의 월요일 <c>00:00:00</c>을 반환합니다 (ISO 8601 기준).
    /// </summary>
    /// <example>
    /// <code>
    /// // 주말 제외 다음 영업일 계산
    /// DateTime next = DateTime.Today;
    /// while (next.IsWeekend()) next = next.AddDays(1);
    /// scheduler.RunAt(next.StartOfDay());
    /// </code>
    /// </example>
    public static DateTime StartOfWeek(this DateTime dt)
        => dt.AddDays(-((7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7)).Date;

    /// <summary>해당 월의 1일 <c>00:00:00</c>을 반환합니다.</summary>
    public static DateTime StartOfMonth(this DateTime dt)
        => new(dt.Year, dt.Month, 1, 0, 0, 0, dt.Kind);

    /// <summary>해당 월의 마지막 틱을 반환합니다.</summary>
    public static DateTime EndOfMonth(this DateTime dt)
        => dt.StartOfMonth().AddMonths(1).AddTicks(-1);

    /// <summary>
    /// <paramref name="from"/>과 <paramref name="to"/> 사이에 있는지 검사합니다 (경계값 포함).
    /// </summary>
    /// <example>
    /// <code>
    /// // 오늘 수신된 프레임만 필터
    /// var todayFrames = allFrames
    ///     .Where(f => f.ReceivedAt.IsBetween(
    ///         DateTime.Today.StartOfDay(),
    ///         DateTime.Today.EndOfDay()))
    ///     .ToList();
    ///
    /// // 업무 시간 판단
    /// bool isBusinessHour = DateTime.Now.IsBetween(
    ///     DateTime.Today.AddHours(9),
    ///     DateTime.Today.AddHours(18));
    /// </code>
    /// </example>
    public static bool IsBetween(this DateTime dt, DateTime from, DateTime to)
        => dt >= from && dt <= to;

    /// <summary>평일(월~금)이면 <c>true</c>를 반환합니다.</summary>
    /// <example>
    /// <code>
    /// if (DateTime.Today.IsWeekday())
    ///     SendDailyReport();
    /// </code>
    /// </example>
    public static bool IsWeekday(this DateTime dt)
        => dt.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

    /// <summary>주말(토~일)이면 <c>true</c>를 반환합니다.</summary>
    public static bool IsWeekend(this DateTime dt) => !dt.IsWeekday();

    // ─────────────────────────────────────────────
    // §4  상대 시간 (한국어)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 현재 시각을 기준으로 경과 시간을 한국어 자연어로 표현합니다.<br/>
    /// 30일 초과 시 <see cref="ToIsoDate"/> 형식으로 반환합니다.
    /// </summary>
    /// <returns>
    /// <list type="table">
    ///   <listheader><term>경과 시간</term><description>반환값 예시</description></listheader>
    ///   <item><term>60초 미만</term><description>"30초 전"</description></item>
    ///   <item><term>1시간 미만</term><description>"5분 전"</description></item>
    ///   <item><term>1일 미만</term><description>"2시간 전"</description></item>
    ///   <item><term>7일 미만</term><description>"3일 전"</description></item>
    ///   <item><term>30일 미만</term><description>"2주 전"</description></item>
    ///   <item><term>30일 이상</term><description>"2024-02-01"</description></item>
    /// </list>
    /// </returns>
    /// <example>
    /// <code>
    /// // WPF 이벤트 로그 타임스탬프
    /// foreach (var ev in eventLog.OrderByDescending(e => e.OccurredAt))
    ///     listBox.Items.Add($"[{ev.OccurredAt.ToRelativeKo()}] {ev.Message}");
    /// // → "[3분 전] CRC 불일치 감지"
    /// // → "[2일 전] 센서 재연결 완료"
    ///
    /// // 마지막 수신 표시
    /// sensorLabel.Content = $"마지막 수신: {device.LastFrameAt.ToRelativeKo()}";
    /// </code>
    /// </example>
    public static string ToRelativeKo(this DateTime dt)
    {
        var diff = DateTime.Now - dt;
        return diff.TotalSeconds switch
        {
            < 60 => $"{(int)diff.TotalSeconds}초 전",
            < 3_600 => $"{(int)diff.TotalMinutes}분 전",
            < 86_400 => $"{(int)diff.TotalHours}시간 전",
            < 604_800 => $"{(int)diff.TotalDays}일 전",
            < 2_592_000 => $"{(int)(diff.TotalDays / 7)}주 전",
            _ => dt.ToIsoDate()
        };
    }

    // ─────────────────────────────────────────────
    // §5  TimeSpan 유틸
    // ─────────────────────────────────────────────

    /// <summary>
    /// <see cref="TimeSpan"/>을 <c>"hh:mm:ss.fff"</c> 형식으로 변환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// TimeSpan.FromSeconds(90.5).ToDisplay()  // "00:01:30.500"
    ///
    /// // 처리 시간 측정
    /// var sw = Stopwatch.StartNew();
    /// await ProcessAsync();
    /// sw.Stop();
    /// logger.Info($"처리 완료: {sw.Elapsed.ToDisplay()}");
    /// // → "처리 완료: 00:00:01.234"
    /// </code>
    /// </example>
    public static string ToDisplay(this TimeSpan ts)
        => ts.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    /// <summary>
    /// <see cref="TimeSpan"/>을 총 밀리초 정수로 변환합니다.<br/>
    /// 타임아웃 비교 및 성능 측정에 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// TimeSpan.FromSeconds(1.5).ToMs()  // 1500L
    ///
    /// // 타임아웃 임박 경고
    /// TimeSpan remaining = deadline - DateTime.Now;
    /// if (remaining.ToMs() < 500)
    ///     logger.Warn($"타임아웃 임박: {remaining.ToDisplay()} 남음");
    /// </code>
    /// </example>
    public static long ToMs(this TimeSpan ts) => (long)ts.TotalMilliseconds;

    // ─────────────────────────────────────────────
    // §6  안전 파싱
    // ─────────────────────────────────────────────

    /// <summary>
    /// 문자열을 <see cref="DateTime"/>으로 파싱합니다. 실패 시 <c>null</c>을 반환합니다.<br/>
    /// 기본 포맷은 <c>"yyyy-MM-dd HH:mm:ss"</c>입니다.
    /// </summary>
    /// <param name="s">파싱할 문자열.</param>
    /// <param name="format">날짜 포맷 문자열. 기본값: <c>"yyyy-MM-dd HH:mm:ss"</c>.</param>
    /// <returns>파싱된 <see cref="DateTime"/> 또는 <c>null</c>.</returns>
    /// <example>
    /// <code>
    /// "2024-04-01 14:30:00".TryParseDateTime()              // DateTime?
    /// "2024-04-01".TryParseDateTime("yyyy-MM-dd")           // DateTime?
    /// "invalid".TryParseDateTime()                          // null
    ///
    /// // 기본값 처리
    /// DateTime from = rawFrom.TryParseDateTime("yyyy-MM-dd")
    ///     ?? DateTime.Today.StartOfMonth();
    /// </code>
    /// </example>
    public static DateTime? TryParseDateTime(this string? s,
        string format = "yyyy-MM-dd HH:mm:ss")
        => DateTime.TryParseExact(s, format,
               CultureInfo.InvariantCulture, DateTimeStyles.None, out var r) ? r : null;

    /// <summary>
    /// 여러 포맷을 순서대로 시도하여 파싱합니다. 모두 실패하면 <c>null</c>을 반환합니다.<br/>
    /// 외부 시스템의 날짜 포맷이 혼재할 때 사용합니다.
    /// </summary>
    /// <param name="s">파싱할 문자열.</param>
    /// <param name="formats">시도할 날짜 포맷 목록.</param>
    /// <returns>첫 번째로 성공한 파싱 결과 또는 <c>null</c>.</returns>
    /// <example>
    /// <code>
    /// // WPF DatePicker 다양한 입력 처리
    /// DateTime from = dateFromBox.Text.TryParseAny(
    ///     "yyyy-MM-dd", "yyyyMMdd", "MM/dd/yyyy", "yyyy/MM/dd")
    ///     ?? DateTime.Today.StartOfMonth();
    ///
    /// // 여러 소스의 날짜 형식 통합
    /// static DateTime? ParseAnySource(string? raw) =>
    ///     raw.TryParseAny(
    ///         "yyyy-MM-dd HH:mm:ss",   // DB 기본
    ///         "yyyy-MM-ddTHH:mm:ssZ",  // ISO 8601
    ///         "yyyyMMdd",               // 레거시
    ///         "MM/dd/yyyy"              // 미국 형식
    ///     );
    /// </code>
    /// </example>
    public static DateTime? TryParseAny(this string? s, params string[] formats)
        => DateTime.TryParseExact(s, formats,
               CultureInfo.InvariantCulture, DateTimeStyles.None, out var r) ? r : null;
}