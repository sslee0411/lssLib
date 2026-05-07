
using System.Runtime.CompilerServices;

namespace lssLib.Utils;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Utils — Guard
//  인수 선행 검증 헬퍼.
//  ▸ CallerArgumentExpression → 호출부 표현식 자동 캡처
//  ▸ No abstractions  ▸ Static class (extension 아님)
//  ▸ 검증 통과 시 값 반환 → 체이닝 가능
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 인수 선행 검증 유틸리티 클래스.<br/>
/// <c>CallerArgumentExpression</c>으로 호출부 표현식을 자동 캡처하며,
/// 검증 통과 시 입력값을 그대로 반환하므로 체이닝이 가능합니다.
/// </summary>
/// <remarks>
/// <para><b>설계 원칙</b></para>
/// <para>
/// Guard는 메서드·생성자 진입점에서 인수를 즉시 검증합니다.
/// 검증이 통과되면 이후 로직에서 null 체크나 범위 체크 없이
/// 안전하게 인수를 사용할 수 있습니다.
/// </para>
/// <para><b>실패 메시지 자동 캡처</b></para>
/// <para>
/// <c>Guard.NotNull(options.Schema)</c>가 실패하면<br/>
/// 예외 메시지에 <c>"options.Schema"</c>라는 표현식 전체가 포함됩니다.
/// </para>
/// <para><b>운용 패턴 — 생성자 초기화</b></para>
/// <code>
/// public SensorService(ILogger logger, BufSchema schema, string outputDir)
/// {
///     _logger    = Guard.NotNull(logger);
///     _schema    = Guard.NotNull(schema);
///     _outputDir = Guard.NotWhiteSpace(outputDir).EnsureDirSelf();
/// }
/// </code>
/// </remarks>
public static class Guard
{
    // ─────────────────────────────────────────────
    // §1  Null 검증
    // ─────────────────────────────────────────────

    /// <summary>
    /// 참조 타입 인수가 <c>null</c>이면 <see cref="ArgumentNullException"/>을 throw합니다.<br/>
    /// 검증 통과 시 <c>non-null T</c>를 반환하므로 체이닝에 사용할 수 있습니다.
    /// </summary>
    /// <typeparam name="T">참조 타입.</typeparam>
    /// <param name="value">검증할 값.</param>
    /// <param name="paramName">
    /// 파라미터 이름 — 생략 시 <c>CallerArgumentExpression</c>으로 자동 캡처.
    /// </param>
    /// <returns><paramref name="value"/> (non-null 보장).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/>가 <c>null</c>인 경우.</exception>
    /// <example>
    /// <code>
    /// // 기본 사용
    /// var schema = Guard.NotNull(schema);
    ///
    /// // 체이닝 — 검증 후 즉시 메서드 호출
    /// Guard.NotNull(options.Builder).SetTimeout(30);
    ///
    /// // 생성자 패턴
    /// _schema = Guard.NotNull(schema);   // 필드 대입과 검증 동시 처리
    /// </code>
    /// </example>
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
        => value ?? throw new ArgumentNullException(paramName);

    /// <summary>
    /// <see cref="Nullable{T}"/> 구조체에 값이 없으면 <see cref="ArgumentNullException"/>을 throw합니다.<br/>
    /// 검증 통과 시 언박싱된 <typeparamref name="T"/>를 반환합니다.
    /// </summary>
    /// <typeparam name="T">값 타입.</typeparam>
    /// <param name="value">검증할 Nullable 값.</param>
    /// <param name="paramName">파라미터 이름 (자동 캡처).</param>
    /// <returns>언박싱된 <typeparamref name="T"/> 값.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/>가 <c>null</c>인 경우.</exception>
    /// <example>
    /// <code>
    /// int? maybeId = record.Id;
    /// int id = Guard.NotNull(maybeId);   // int? → int (언박싱)
    /// </code>
    /// </example>
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct
        => value ?? throw new ArgumentNullException(paramName);

    // ─────────────────────────────────────────────
    // §2  문자열 검증
    // ─────────────────────────────────────────────

    /// <summary>
    /// 문자열이 <c>null</c>이거나 빈 문자열(<c>""</c>)이면
    /// <see cref="ArgumentException"/>을 throw합니다.<br/>
    /// 공백(<c>"  "</c>)은 허용합니다. 공백까지 거부하려면
    /// <see cref="NotWhiteSpace"/>를 사용하세요.
    /// </summary>
    /// <param name="value">검증할 문자열.</param>
    /// <param name="paramName">파라미터 이름 (자동 캡처).</param>
    /// <returns><paramref name="value"/> (non-empty 보장).</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/>가 <c>null</c>이거나 빈 문자열인 경우.</exception>
    /// <example>
    /// <code>
    /// // lssLib.Binary 프레임 처리 진입점
    /// string code  = Guard.NotEmpty(deviceCode);
    /// string token = Guard.NotEmpty(apiToken);
    ///
    /// // NotEmpty vs NotWhiteSpace
    /// Guard.NotEmpty("  ");      // 통과 — 공백은 허용
    /// Guard.NotWhiteSpace("  "); // throw — 공백 거부
    /// </code>
    /// </example>
    public static string NotEmpty(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("값이 비어있습니다.", paramName);
        return value;
    }

    /// <summary>
    /// 문자열이 <c>null</c>이거나 빈 문자열(<c>""</c>)이거나 공백만으로 구성된 경우
    /// <see cref="ArgumentException"/>을 throw합니다.
    /// </summary>
    /// <param name="value">검증할 문자열.</param>
    /// <param name="paramName">파라미터 이름 (자동 캡처).</param>
    /// <returns><paramref name="value"/> (non-whitespace 보장).</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/>가 <c>null</c>, 빈 문자열, 또는 공백인 경우.</exception>
    /// <example>
    /// <code>
    /// // 필수 설정값 검증
    /// string portName  = Guard.NotWhiteSpace(config.PortName);
    /// string apiKey    = Guard.NotWhiteSpace(config.ApiKey);
    ///
    /// // FileExtensions 체이닝
    /// string outDir = Guard.NotWhiteSpace(basePath).EnsureDirSelf();
    ///
    /// // WpfDemo 설정 로드
    /// public DemoConfig(string portName, string outputDir)
    /// {
    ///     PortName  = Guard.NotWhiteSpace(portName);
    ///     OutputDir = Guard.NotWhiteSpace(outputDir).EnsureDirSelf();
    /// }
    /// </code>
    /// </example>
    public static string NotWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("값이 null이거나 공백입니다.", paramName);
        return value!;
    }

    // ─────────────────────────────────────────────
    // §3  배열 검증
    // ─────────────────────────────────────────────

    /// <summary>
    /// 배열이 <c>null</c>이거나 길이가 0이면 <see cref="ArgumentException"/>을 throw합니다.<br/>
    /// lssLib.Binary <c>BufferParser</c>에 전달하기 전 선행 검증에 적합합니다.
    /// </summary>
    /// <typeparam name="T">배열 요소 타입.</typeparam>
    /// <param name="array">검증할 배열.</param>
    /// <param name="paramName">파라미터 이름 (자동 캡처).</param>
    /// <returns><paramref name="array"/> (non-empty 보장).</returns>
    /// <exception cref="ArgumentException"><paramref name="array"/>가 <c>null</c>이거나 빈 배열인 경우.</exception>
    /// <example>
    /// <code>
    /// // lssLib.Binary 연계 — STX 프레임 수신 처리 진입점
    /// void ProcessFrame(byte[] raw)
    /// {
    ///     byte[] frame = Guard.NotEmpty(raw);      // null·빈 즉시 차단
    ///     var parser   = new BufferParser(frame);  // 안전하게 전달
    ///     uint id      = parser.Read<uint>(BufType.UInt32LE);
    /// }
    ///
    /// // CRC 계산 전 배열 검증
    /// byte[] payload = Guard.NotEmpty(frame[..^4]);
    /// uint   crc     = payload.ComputeCrc32();   // CrcExtensions
    /// </code>
    /// </example>
    public static T[] NotEmpty<T>(
        T[]? array,
        [CallerArgumentExpression(nameof(array))] string? paramName = null)
    {
        if (array is null || array.Length == 0)
            throw new ArgumentException("배열이 null이거나 비어있습니다.", paramName);
        return array;
    }

    // ─────────────────────────────────────────────
    // §4  수치 범위 검증
    // ─────────────────────────────────────────────

    /// <summary>
    /// 값이 [<paramref name="min"/>, <paramref name="max"/>] 범위를 벗어나면
    /// <see cref="ArgumentOutOfRangeException"/>을 throw합니다.<br/>
    /// <see cref="IComparable{T}"/> 제약으로 <c>int</c>, <c>float</c>, <c>DateTime</c> 등 모두 사용 가능합니다.
    /// </summary>
    /// <typeparam name="T"><see cref="IComparable{T}"/>를 구현하는 타입.</typeparam>
    /// <param name="value">검증할 값.</param>
    /// <param name="min">허용 최솟값 (포함).</param>
    /// <param name="max">허용 최댓값 (포함).</param>
    /// <param name="paramName">파라미터 이름 (자동 캡처).</param>
    /// <returns><paramref name="value"/> (범위 내 보장).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/>가 범위를 벗어난 경우.</exception>
    /// <example>
    /// <code>
    /// // 프레임 오프셋 범위 검증
    /// int offset = Guard.Range(offset, 0, frame.Length - 1);
    ///
    /// // 채널 번호 검증
    /// byte channel = Guard.Range(channel, (byte)0, (byte)7);
    ///
    /// // ScaleExtensions SmoothStep alpha 파라미터
    /// float alpha = Guard.Range(configAlpha, 0.0f, 1.0f);
    ///
    /// // DateTime 범위 — 리포트 날짜가 유효한 기간인지 확인
    /// var reportDate = Guard.Range(date,
    ///     DateTime.Today.AddYears(-1),
    ///     DateTime.Today);
    /// </code>
    /// </example>
    public static T Range<T>(
        T value, T min, T max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(
                paramName, value, $"범위를 벗어났습니다. [{min}, {max}]");
        return value;
    }

    /// <summary>
    /// 값이 0 미만이면 <see cref="ArgumentOutOfRangeException"/>을 throw합니다.<br/>
    /// 0은 허용됩니다. 0도 거부하려면 <see cref="Positive{T}"/>를 사용하세요.
    /// </summary>
    /// <typeparam name="T"><see cref="IComparable{T}"/>를 구현하는 수치 타입.</typeparam>
    /// <param name="value">검증할 값.</param>
    /// <param name="paramName">파라미터 이름 (자동 캡처).</param>
    /// <returns><paramref name="value"/> (0 이상 보장).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/>가 음수인 경우.</exception>
    /// <example>
    /// <code>
    /// int maxRetry = Guard.NotNegative(retryCount);  // -1 → throw, 0 → 통과
    /// int delayMs  = Guard.NotNegative(delay);       // Retry 대기 시간
    ///
    /// // RingBuffer 용량 — 0은 허용, 음수는 거부
    /// int capacity = Guard.NotNegative(bufferSize);
    /// </code>
    /// </example>
    public static T NotNegative<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default(T)) < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "음수는 허용되지 않습니다.");
        return value;
    }

    /// <summary>
    /// 값이 0 이하이면 <see cref="ArgumentOutOfRangeException"/>을 throw합니다.<br/>
    /// 0도 거부합니다. 0을 허용하려면 <see cref="NotNegative{T}"/>를 사용하세요.
    /// </summary>
    /// <typeparam name="T"><see cref="IComparable{T}"/>를 구현하는 수치 타입.</typeparam>
    /// <param name="value">검증할 값.</param>
    /// <param name="paramName">파라미터 이름 (자동 캡처).</param>
    /// <returns><paramref name="value"/> (양수 보장).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/>가 0 이하인 경우.</exception>
    /// <example>
    /// <code>
    /// int    bufSize      = Guard.Positive(bufferSize);   // 0 → throw, 1 → 통과
    /// double samplingRate = Guard.Positive(rate);         // 센서 샘플링 주파수
    ///
    /// // WpfDemo 설정 검증
    /// float hystLow = Guard.Positive(
    ///     (float)(ini["hyst_low"].ToDoubleOrNull() ?? 0.05));
    /// </code>
    /// </example>
    public static T Positive<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default(T)) <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "양수여야 합니다.");
        return value;
    }

    // ─────────────────────────────────────────────
    // §5  조건 검증
    // ─────────────────────────────────────────────

    /// <summary>
    /// 조건이 <c>false</c>이면 <see cref="ArgumentException"/>을 throw합니다.<br/>
    /// <paramref name="message"/>를 생략하면 조건식 텍스트 자체가 메시지에 포함됩니다.
    /// </summary>
    /// <param name="condition">검증할 조건.</param>
    /// <param name="message">실패 시 메시지. <c>null</c>이면 조건식 표현이 사용됩니다.</param>
    /// <param name="expression">조건식 텍스트 (자동 캡처, 직접 지정 불필요).</param>
    /// <exception cref="ArgumentException"><paramref name="condition"/>이 <c>false</c>인 경우.</exception>
    /// <example>
    /// <code>
    /// // 조건식이 메시지에 자동 포함
    /// Guard.That(header.Length >= 4);
    /// // 실패 시: "조건을 만족하지 않습니다: header.Length >= 4"
    ///
    /// // lssLib.Binary STX/ETX 헤더 검증
    /// void ValidateFrame(byte[] frame, BufSchema schema)
    /// {
    ///     Guard.NotEmpty(frame);
    ///     Guard.That(frame[0] == 0x02,  "STX 헤더가 없습니다.");
    ///     Guard.That(frame[^1] == 0x03, "ETX 종료자가 없습니다.");
    ///     Guard.That(frame.Length == schema.ExpectedSize,
    ///         $"프레임 크기 불일치: 수신={frame.Length}, 예상={schema.ExpectedSize}");
    /// }
    ///
    /// // lssLib.Extensions CRC 검증
    /// uint rxCrc   = frame[^4..].ToUInt32LE();
    /// uint calcCrc = frame[..^4].ComputeCrc32();
    /// Guard.That(rxCrc == calcCrc,
    ///     $"CRC 불일치: 수신={rxCrc:X8}, 계산={calcCrc:X8}");
    ///
    /// // 상태 전이 전 사전 조건
    /// Guard.That(_isConnected,  "장치가 연결되지 않았습니다.");
    /// Guard.That(!_isAcquiring, "이미 수집 중입니다.");
    /// </code>
    /// </example>
    public static void That(
        bool condition,
        string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new ArgumentException(message ?? $"조건을 만족하지 않습니다: {expression}");
    }
}