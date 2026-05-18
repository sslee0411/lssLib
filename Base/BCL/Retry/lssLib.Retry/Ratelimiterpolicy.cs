namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — RateLimiterPolicy
//  속도 제한 설정 값 객체 (readonly record struct)
//    - 서버나 API가 감당할 수 있는 수준까지만 요청을 허용하는 안전장치
//  ▸ Sliding Window 알고리즘 기반
//  ▸ No abstractions  ▸ Immutable value type
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 속도 제한 동작 설정 불변 값 객체 (슬라이딩 윈도우 방식).<br/>
/// <see cref="RateLimiterState"/> 생성 시 전달합니다.
/// </summary>
/// <param name="MaxRequests">윈도우 내 최대 허용 요청 수.</param>
/// <param name="Window">윈도우 시간 범위.</param>
/// <param name="ThrowOnExceeded">
/// <c>true</c>: 초과 시 <see cref="RateLimitExceededException"/> throw (기본값).<br/>
/// <c>false</c>: 초과 시 예외 없이 <c>default(T)</c> 반환.
/// </param>
/// <remarks>
/// <para><b>슬라이딩 윈도우 알고리즘</b></para>
/// <para>
/// 고정 윈도우(Fixed Window)와 달리, 슬라이딩 윈도우는 현재 시각으로부터
/// <see cref="Window"/> 이전까지의 요청 수를 계산합니다.
/// 윈도우 경계에서 버스트 현상이 발생하지 않아 더 정확한 제한이 가능합니다.
/// </para>
/// <para><b>팩토리 메서드 사용 권장</b></para>
/// <code>
/// // 직접 생성보다 팩토리 메서드가 명확합니다.
/// var policy = RateLimiterPolicy.PerSecond(30);   // 초당 30회
/// var policy = RateLimiterPolicy.PerMinute(100);  // 분당 100회
/// </code>
/// </remarks>
public readonly record struct RateLimiterPolicy(
    int MaxRequests,
    TimeSpan Window,
    bool ThrowOnExceeded = true)
{
    // ─────────────────────────────────────────────
    // §1  팩토리 메서드 (시간 단위 명시)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 초당 <paramref name="max"/>회 제한 정책을 생성합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // lssLib.Serialization 탭3: RingBuffer 30 FPS 처리 제한
    /// var limiter = new RateLimiterState(RateLimiterPolicy.PerSecond(30));
    /// </code>
    /// </example>
    public static RateLimiterPolicy PerSecond(int max)
        => new(max, TimeSpan.FromSeconds(1));

    /// <summary>
    /// 분당 <paramref name="max"/>회 제한 정책을 생성합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // REST API 분당 60회 제한
    /// var limiter = new RateLimiterState(RateLimiterPolicy.PerMinute(60));
    /// </code>
    /// </example>
    public static RateLimiterPolicy PerMinute(int max)
        => new(max, TimeSpan.FromMinutes(1));

    /// <summary>시간당 <paramref name="max"/>회 제한 정책을 생성합니다.</summary>
    public static RateLimiterPolicy PerHour(int max)
        => new(max, TimeSpan.FromHours(1));

    /// <summary>일당 <paramref name="max"/>회 제한 정책을 생성합니다.</summary>
    public static RateLimiterPolicy PerDay(int max)
        => new(max, TimeSpan.FromDays(1));

    // ─────────────────────────────────────────────
    // §2  사전 정의 프리셋
    // ─────────────────────────────────────────────

    /// <summary>API 기본: 분당 60회. 일반 REST API에 적합합니다.</summary>
    public static readonly RateLimiterPolicy ApiDefault = PerMinute(60);

    /// <summary>
    /// 엄격: 초당 10회. 처리량 민감 경로에 적합합니다.
    /// </summary>
    public static readonly RateLimiterPolicy Strict = PerSecond(10);

    /// <summary>관대: 시간당 1,000회. 배치 처리에 적합합니다.</summary>
    public static readonly RateLimiterPolicy Lenient = PerHour(1_000);

    /// <summary>
    /// 로그인 시도: 분당 5회. 보안 강화 경로에 적합합니다.<br/>
    /// <c>ThrowOnExceeded=true</c>로 초과 시 즉시 차단합니다.
    /// </summary>
    public static readonly RateLimiterPolicy LoginAttempt = PerMinute(5);
}