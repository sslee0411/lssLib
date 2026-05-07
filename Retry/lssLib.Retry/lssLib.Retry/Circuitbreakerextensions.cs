
namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — CircuitBreakerExtensions
//  회로 차단기 실행 확장 메서드
//    - (장애가 발생한 서비스에 계속 요청을 보내서 시스템 전체가 마비되는 것을 방지하는 안전 장치)
//  ▸ No abstractions  ▸ Extension-method only
//  ▸ Open 상태에서 즉시 CircuitBreakerOpenException throw
//  ▸ OperationCanceledException 재시도 없이 즉시 전파
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 회로 차단기 실행 확장 메서드.<br/>
/// <see cref="CircuitBreakerState"/> 공유 인스턴스를 주입하여 사용합니다.
/// </summary>
/// <remarks>
/// <para><b>메서드 선택 가이드</b></para>
/// <list type="table">
///   <listheader><term>상황</term><description>메서드</description></listheader>
///   <item><term>기본 CB 실행</term><description><see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, CircuitBreakerState, CancellationToken)"/></description></item>
///   <item><term>CB + Retry 조합</term><description><see cref="ExecuteWithRetryAsync{T}"/></description></item>
///   <item><term>예외 없는 안전 실행</term><description><see cref="TryExecuteAsync{T}"/></description></item>
/// </list>
/// </remarks>
public static class CircuitBreakerExtensions
{
    // ─────────────────────────────────────────────
    // §1  비동기 실행
    // ─────────────────────────────────────────────

    /// <summary>
    /// 회로 차단기가 적용된 비동기 함수를 실행합니다.<br/>
    /// Open 상태이면 즉시 <see cref="CircuitBreakerOpenException"/>을 throw합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func"><see cref="CancellationToken"/>을 수용하는 비동기 함수.</param>
    /// <param name="state">공유 회로 차단기 상태 인스턴스.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>성공 시 결과값.</returns>
    /// <exception cref="CircuitBreakerOpenException">회로가 열려 있는 경우.</exception>
    /// <example>
    /// <code>
    /// // lssLib.Serialization WpfDemo 탭2: CRC 파이프라인
    /// static readonly CircuitBreakerState _crcBreaker = new(
    ///     new CircuitBreakerPolicy(
    ///         FailureThreshold: 5,
    ///         OpenDuration:     TimeSpan.FromSeconds(10),
    ///         OnStateChanged:   (_, next) =>
    ///         {
    ///             if (next == CircuitState.Open)
    ///                 ui.ShowCrcError("CRC 연속 오류 — 10초 차단");
    ///         }
    ///     )
    /// );
    ///
    /// Func<CancellationToken, Task<BufResult>> parseFrame =
    ///     ct => Task.Run(() =>
    ///     {
    ///         uint rxCrc   = raw[^4..].ToUInt32LE();
    ///         uint calcCrc = raw[..^4].ComputeCrc32();  // CrcExtensions
    ///         if (rxCrc != calcCrc)
    ///             throw new CrcMismatchException(rxCrc, calcCrc);
    ///         return new BufferParser(raw).ParseAll(schema);
    ///     }, ct);
    ///
    /// try
    /// {
    ///     var result = await parseFrame.ExecuteAsync(_crcBreaker, ct);
    /// }
    /// catch (CircuitBreakerOpenException ex)
    /// {
    ///     logger.Warn($"CB 차단: {ex.RemainingDuration.TotalSeconds:F0}초 후 재시도");
    /// }
    /// </code>
    /// </example>
    public static async Task<T> ExecuteAsync<T>(
        this Func<CancellationToken, Task<T>> func,
        CircuitBreakerState state,
        CancellationToken ct = default)
    {
        if (!state.TryEnter())
            throw new CircuitBreakerOpenException(state);

        ct.ThrowIfCancellationRequested();
        try
        {
            var result = await func(ct);
            state.OnSuccess();
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            state.OnFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// 회로 차단기가 적용된 비동기 액션을 실행합니다 (반환값 없음).
    /// </summary>
    /// <example>
    /// <code>
    /// Func<CancellationToken, Task>pingDb = ct => db.PingAsync(ct);
    /// await pingDb.ExecuteAsync(_dbBreaker, ct);
    /// </code>
    /// </example>
    public static async Task ExecuteAsync(
        this Func<CancellationToken, Task> action,
        CircuitBreakerState state,
        CancellationToken ct = default)
    {
        if (!state.TryEnter())
            throw new CircuitBreakerOpenException(state);

        ct.ThrowIfCancellationRequested();
        try
        {
            await action(ct);
            state.OnSuccess();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            state.OnFailure(ex);
            throw;
        }
    }

    // ─────────────────────────────────────────────
    // §2  Retry 조합
    // ─────────────────────────────────────────────

    /// <summary>
    /// 회로 차단기 + Retry 조합 실행.<br/>
    /// Open 상태이면 즉시 throw (재시도 없음).<br/>
    /// Closed/HalfOpen 상태에서 실패 시 <paramref name="retryPolicy"/> 설정대로 재시도합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func">실행할 비동기 함수.</param>
    /// <param name="circuitBreaker">공유 회로 차단기 상태.</param>
    /// <param name="retryPolicy">Retry 정책. <c>null</c>이면 <see cref="RetryPolicy.Default"/>.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <example>
    /// <code>
    /// // lssLib.Serialization WpfDemo 탭5: SmoothStep 센서 스트리밍
    /// static readonly CircuitBreakerState _streamBreaker =
    ///     new(CircuitBreakerPolicy.Default);
    ///
    /// Func<CancellationToken, Task<float>> readSignal = async ct =>
    /// {
    ///     byte[]  raw  = await sensorStream.ReadAsync(ct);
    ///     float   raw_v = new BufferParser(raw).Read<float>(BufType.FloatLE);
    ///     // ScaleExtensions.SmoothStep으로 신호 보간
    ///     return raw_v.SmoothStep(_lastSignal, alpha: 0.2f);
    /// };
    ///
    /// float signal = await readSignal.ExecuteWithRetryAsync(
    ///     _streamBreaker, RetryPolicy.Default, ct);
    /// </code>
    /// </example>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        this Func<CancellationToken, Task<T>> func,
        CircuitBreakerState circuitBreaker,
        RetryPolicy? retryPolicy = null,
        CancellationToken ct = default)
    {
        var p = retryPolicy ?? RetryPolicy.Default;

        if (!circuitBreaker.TryEnter())
            throw new CircuitBreakerOpenException(circuitBreaker);

        Exception? last = null;
        for (int i = 0; i < p.MaxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await func(ct);
                circuitBreaker.OnSuccess();
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (CircuitBreakerOpenException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                p.OnRetry?.Invoke(ex, i + 1);
                if (i < p.MaxAttempts - 1)
                    await Task.Delay(p.GetDelay(i), ct);
            }
        }

        circuitBreaker.OnFailure(last!);
        throw last!;
    }

    // ─────────────────────────────────────────────
    // §3  안전 실행 (UtilResult 반환)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 회로 차단기 적용 안전 실행.<br/>
    /// Open 상태 포함 모든 예외를 <see cref="UtilResult{T}"/>로 반환합니다. 예외를 throw하지 않습니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <example>
    /// <code>
    /// // lssLib.Serialization WpfDemo 탭8: 복합 파이프라인
    /// Func<CancellationToken, Task<BufResult>> acquire =
    ///     ct => Task.Run(() => ReadAndParseAsync(schema, ct), ct);
    ///
    /// var r = await acquire.TryExecuteAsync(_breaker, ct);
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
    ///     default:
    ///         ui.ShowError(r.Error!.Message);
    ///         break;
    /// }
    /// </code>
    /// </example>
    public static async Task<UtilResult<T>> TryExecuteAsync<T>(
        this Func<CancellationToken, Task<T>> func,
        CircuitBreakerState state,
        CancellationToken ct = default)
    {
        try
        {
            var result = await func.ExecuteAsync(state, ct);
            return UtilResults.Ok(result);
        }
        catch (Exception ex) { return UtilResults.Fail<T>(ex); }
    }
}

// ═══════════════════════════════════════════════════════════════════
//  CircuitBreakerOpenException
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 회로 차단기가 <see cref="CircuitState.Open"/> 상태일 때 throw되는 예외.<br/>
/// <see cref="State"/>와 잔여 대기 시간을 포함합니다.
/// </summary>
/// <example>
/// <code>
/// catch (CircuitBreakerOpenException ex)
/// {
///     logger.Warn(ex.Message);
///     // "회로 차단기가 열려 있습니다. 28.3초 후 재시도 가능합니다."
///
///     ui.ShowRetryTimer(ex.RemainingDuration);
/// }
/// </code>
/// </example>
public sealed class CircuitBreakerOpenException : Exception
{
    /// <summary>예외 발생 시 회로 상태 스냅샷.</summary>
    public CircuitState State { get; }

    /// <summary>Open 상태 잔여 시간.</summary>
    public TimeSpan RemainingDuration { get; }

    /// <inheritdoc cref="CircuitBreakerOpenException"/>
    public CircuitBreakerOpenException(CircuitBreakerState state)
        : base(BuildMessage(state))
    {
        State = state.Current;
        RemainingDuration = state.RemainingOpenDuration;
    }

    private static string BuildMessage(CircuitBreakerState state)
    {
        var remaining = state.RemainingOpenDuration;
        return remaining > TimeSpan.Zero
            ? $"회로 차단기가 열려 있습니다. {remaining.TotalSeconds:F1}초 후 재시도 가능합니다."
            : "회로 차단기가 열려 있습니다. 복구 대기 중입니다.";
    }
}