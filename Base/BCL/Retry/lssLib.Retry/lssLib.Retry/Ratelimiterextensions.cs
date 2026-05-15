namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — RateLimiterExtensions
//   - 서버나 API가 감당할 수 있는 수준까지만 요청을 허용하는 안전장치
//  속도 제한 실행 확장 메서드
//  ▸ No abstractions  ▸ Extension-method only
//  ▸ 한도 초과 시 RateLimitExceededException throw (정책 설정에 따라)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 속도 제한 실행 확장 메서드.<br/>
/// <see cref="RateLimiterState"/> 공유 인스턴스를 주입하여 사용합니다.
/// </summary>
/// <remarks>
/// <para><b>메서드 선택 가이드</b></para>
/// <list type="table">
///   <listheader><term>상황</term><description>메서드</description></listheader>
///   <item><term>즉시 실행 또는 즉시 거부</term><description><see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, RateLimiterState, CancellationToken)"/></description></item>
///   <item><term>슬롯 대기 허용</term><description><see cref="ExecuteWithWaitAsync{T}"/></description></item>
///   <item><term>초과해도 예외 없음</term><description>policy with <c>ThrowOnExceeded = false</c></description></item>
///   <item><term>예외 없는 안전 실행</term><description><see cref="TryExecuteAsync{T}"/></description></item>
///   <item><term>CB + RL 동시 적용</term><description><see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, RateLimiterState, CircuitBreakerState, CancellationToken)"/></description></item>
/// </list>
/// </remarks>
public static class RateLimiterExtensions
{
    // ─────────────────────────────────────────────
    // §1  비동기 실행
    // ─────────────────────────────────────────────

    /// <summary>
    /// 속도 제한이 적용된 비동기 함수를 실행합니다.<br/>
    /// 슬롯 여유가 있으면 즉시 실행합니다.<br/>
    /// 한도 초과 시: <c>ThrowOnExceeded=true</c>이면 <see cref="RateLimitExceededException"/> throw,
    /// <c>false</c>이면 <c>default(T)</c>를 반환합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func"><see cref="CancellationToken"/>을 수용하는 비동기 함수.</param>
    /// <param name="limiter">공유 속도 제한 상태 인스턴스.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <example>
    /// <code>
    /// // lssLib.Serialization 탭3: RingBuffer 30 FPS 처리 제한
    /// static readonly RateLimiterState _frameLimiter =
    ///     new(RateLimiterPolicy.PerSecond(30));
    ///
    /// Func<CancellationToken, Task<(uint id, float temp)>> processFrame =
    ///     ct => Task.Run(() =>
    ///     {
    ///         var    parser   = new BufferParser(Guard.NotEmpty(frame));
    ///         uint   id       = parser.Read<uint>(BufType.UInt32LE);
    ///         float  temp     = parser.Read<float>(BufType.FloatLE);
    ///         float  smoothed = temp.SmoothStep(_lastTemp, 0.15f);  // ScaleExtensions
    ///         return (id, smoothed);
    ///     }, ct);
    ///
    /// try
    /// {
    ///     var (id, temp) = await processFrame.ExecuteAsync(_frameLimiter, ct);
    ///     ui.UpdateChart(id, temp);
    /// }
    /// catch (RateLimitExceededException)
    /// {
    ///     await Task.Delay(33, ct);  // 30 FPS 간격 대기
    /// }
    ///
    /// // ThrowOnExceeded=false — 초과 시 null 반환, 예외 없음
    /// var softLimiter = new RateLimiterState(
    ///     RateLimiterPolicy.PerSecond(10) with { ThrowOnExceeded = false });
    /// string? result = await func.ExecuteAsync(softLimiter, ct);  // 초과 시 null
    /// </code>
    /// </example>
    public static async Task<T> ExecuteAsync<T>(
        this Func<CancellationToken, Task<T>> func,
        RateLimiterState limiter,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!limiter.TryAcquire())
        {
            if (limiter.Policy.ThrowOnExceeded)
                throw new RateLimitExceededException(limiter);
            return default!;
        }

        return await func(ct);
    }

    /// <summary>속도 제한이 적용된 비동기 액션을 실행합니다 (반환값 없음).</summary>
    public static async Task ExecuteAsync(
        this Func<CancellationToken, Task> action,
        RateLimiterState limiter,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!limiter.TryAcquire())
        {
            if (limiter.Policy.ThrowOnExceeded)
                throw new RateLimitExceededException(limiter);
            return;
        }

        await action(ct);
    }

    // ─────────────────────────────────────────────
    // §2  대기 후 실행 (Wait-and-Execute)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 슬롯이 열릴 때까지 대기한 후 실행합니다.<br/>
    /// <paramref name="maxWait"/> 내에 슬롯을 얻지 못하면 <see cref="TimeoutException"/>을 throw합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func">실행할 비동기 함수.</param>
    /// <param name="limiter">공유 속도 제한 상태.</param>
    /// <param name="maxWait">최대 대기 시간. <c>null</c>이면 무한 대기.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <exception cref="TimeoutException"><paramref name="maxWait"/> 내 슬롯 획득 실패.</exception>
    /// <example>
    /// <code>
    /// // lssLib.Serialization 탭6: TextExtensions 직렬화 결과 API 전송
    /// // 분당 60회 API에 최대 3초 슬롯 대기
    /// static readonly RateLimiterState _apiLimiter =
    ///     new(RateLimiterPolicy.PerMinute(60));
    ///
    /// Func<CancellationToken, Task<string>> sendJson =
    ///     async ct =>
    ///     {
    ///         string json = schema.SerializeToJson(data);  // TextExtensions
    ///         return await httpClient.PostStringAsync(endpoint, json, ct);
    ///     };
    ///
    /// string response = await sendJson.ExecuteWithWaitAsync(
    ///     _apiLimiter,
    ///     maxWait: TimeSpan.FromSeconds(3),
    ///     ct:      ct);
    /// </code>
    /// </example>
    public static async Task<T> ExecuteWithWaitAsync<T>(
        this Func<CancellationToken, Task<T>> func,
        RateLimiterState limiter,
        TimeSpan? maxWait = null,
        CancellationToken ct = default)
    {
        var deadline = maxWait.HasValue
            ? DateTime.UtcNow + maxWait.Value
            : DateTime.MaxValue;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (limiter.TryAcquire())
                return await func(ct);

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"속도 제한 슬롯 대기 중 타임아웃: " +
                    $"{limiter.Policy.MaxRequests}회/{limiter.Policy.Window.TotalSeconds:0}초");

            var nextSlot = limiter.NextAvailableAt - DateTime.UtcNow;
            var waitMs = Math.Clamp((int)nextSlot.TotalMilliseconds, 10, 100);
            await Task.Delay(waitMs, ct);
        }
    }

    // ─────────────────────────────────────────────
    // §3  안전 실행 (UtilResult 반환)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 속도 제한 적용 안전 실행.<br/>
    /// 한도 초과 포함 모든 예외를 <see cref="UtilResult{T}"/>로 반환합니다. 예외를 throw하지 않습니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <example>
    /// <code>
    /// Func<CancellationToken, Task<int>> func = ct => Task.FromResult(i * 10);
    /// var r = await func.TryExecuteAsync(_limiter, ct);
    ///
    /// switch (r)
    /// {
    ///     case { IsOk: true }:
    ///         Process(r.Value);
    ///         break;
    ///     case { Error: RateLimitExceededException rlEx }:
    ///         logger.Warn($"RL 초과: {rlEx.NextAvailableAt:HH:mm:ss}");
    ///         break;
    ///     default:
    ///         logger.Error(r.Error!.Message);
    ///         break;
    /// }
    /// </code>
    /// </example>
    public static async Task<UtilResult<T>> TryExecuteAsync<T>(
        this Func<CancellationToken, Task<T>> func,
        RateLimiterState limiter,
        CancellationToken ct = default)
    {
        try
        {
            var result = await func.ExecuteAsync(limiter, ct);
            return UtilResults.Ok(result);
        }
        catch (Exception ex) { return UtilResults.Fail<T>(ex); }
    }

    // ─────────────────────────────────────────────
    // §4  Circuit Breaker + Rate Limiter 조합
    // ─────────────────────────────────────────────

    /// <summary>
    /// 속도 제한과 회로 차단기를 동시에 적용합니다.<br/>
    /// 처리 순서: 속도 제한 확인 → 회로 차단기 확인 → 실행.<br/>
    /// 어느 쪽이든 차단되면 즉시 예외를 throw합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func">실행할 비동기 함수.</param>
    /// <param name="limiter">공유 속도 제한 상태.</param>
    /// <param name="circuitBreaker">공유 회로 차단기 상태.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <remarks>
    /// <para>
    /// 속도 제한이 회로 차단기보다 먼저 확인됩니다.
    /// RL 초과 시 CB 상태와 무관하게 즉시 <see cref="RateLimitExceededException"/>을 throw합니다.
    /// </para>
    /// <para>
    /// CB + RL 조합의 <c>TryExecuteAsync</c> 오버로드는 제공되지 않습니다.
    /// 안전 실행이 필요하면 직접 try/catch로 감싸세요.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // lssLib.Serialization WpfDemo 탭8: 복합 파이프라인
    /// static readonly RateLimiterState    _limiter  = new(RateLimiterPolicy.PerSecond(30));
    /// static readonly CircuitBreakerState _breaker  = new(CircuitBreakerPolicy.Default);
    ///
    /// Func<CancellationToken, Task<BufResult>> acquire = ct =>
    ///     Task.Run(() =>
    ///     {
    ///         // STX 프레임 수신 + CRC 검증 + Binary 파싱
    ///         Guard.That(frame[0] == 0x02, "STX 없음");
    ///         uint rxCrc = frame[^4..].ToUInt32LE();
    ///         uint calCrc = frame[..^4].ComputeCrc32();
    ///         Guard.That(rxCrc == calCrc, "CRC 불일치");
    ///         return new BufferParser(frame[1..^5]).ParseAll(schema);
    ///     }, ct);
    ///
    /// // RL → CB 순서로 보호
    /// UtilResult<BufResult> r;
    /// try { r = UtilResults.Ok(await acquire.ExecuteAsync(_limiter, _breaker, ct)); }
    /// catch (Exception ex) { r = UtilResults.Fail<BufResult&gt;(ex); }
    ///
    /// switch (r)
    /// {
    ///     case { IsOk: true }:
    ///         UpdateDisplay(r.Unwrap());
    ///         break;
    ///     case { Error: CircuitBreakerOpenException cbEx }:
    ///         ui.ShowCircuitOpen(cbEx.RemainingDuration);
    ///         await Task.Delay(1_000, ct);
    ///         break;
    ///     case { Error: RateLimitExceededException }:
    ///         await Task.Delay(33, ct);  // 30 FPS 간격
    ///         break;
    ///     default:
    ///         ui.ShowError(r.Error!.Message);
    ///         break;
    /// }
    /// </code>
    /// </example>
    public static async Task<T> ExecuteAsync<T>(
        this Func<CancellationToken, Task<T>> func,
        RateLimiterState limiter,
        CircuitBreakerState circuitBreaker,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 속도 제한 먼저 확인 — 초과 시 CB 상태와 무관하게 즉시 throw
        if (!limiter.TryAcquire())
            throw new RateLimitExceededException(limiter);

        // 회로 차단기 확인 후 실행
        return await func.ExecuteAsync(circuitBreaker, ct);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  RateLimitExceededException
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 속도 제한 초과 시 throw되는 예외.<br/>
/// 정책 정보와 다음 허용 시각을 포함합니다.
/// </summary>
/// <example>
/// <code>
/// catch (RateLimitExceededException ex)
/// {
///     logger.Warn(ex.Message);
///     // "요청 한도 초과: 30회/1초. 45 ms 후 재시도 가능합니다."
///
///     TimeSpan wait = ex.NextAvailableAt - DateTime.UtcNow;
///     if (wait &gt; TimeSpan.Zero)
///         await Task.Delay(wait, ct);
/// }
/// </code>
/// </example>
public sealed class RateLimitExceededException : Exception
{
    /// <summary>초과된 속도 제한 정책.</summary>
    public RateLimiterPolicy Policy { get; }

    /// <summary>다음 요청이 허용될 예상 시각 (UTC).</summary>
    public DateTime NextAvailableAt { get; }

    /// <inheritdoc cref="RateLimitExceededException"/>
    public RateLimitExceededException(RateLimiterState state)
        : base(BuildMessage(state.Policy, state.NextAvailableAt))
    {
        Policy = state.Policy;
        NextAvailableAt = state.NextAvailableAt;
    }

    private static string BuildMessage(RateLimiterPolicy policy, DateTime nextAt)
    {
        var wait = nextAt - DateTime.UtcNow;
        return wait > TimeSpan.Zero
            ? $"요청 한도 초과: {policy.MaxRequests}회/{policy.Window.TotalSeconds:0}초. " +
              $"{wait.TotalMilliseconds:0} ms 후 재시도 가능합니다."
            : $"요청 한도 초과: {policy.MaxRequests}회/{policy.Window.TotalSeconds:0}초.";
    }
}