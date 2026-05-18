
namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — UtilResult / UtilResult<T> / UtilResults
//  안전 실행(TryExecuteAsync) 반환 타입.
//  ▸ tuple (Value, Error) 대신 명시적 성공/실패 구분 값 타입
//  ▸ No abstractions  ▸ readonly record struct
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 값 없는 성공/실패 결과 타입.<br/>
/// <c>TryExecuteAsync</c> 반환 타입으로 사용합니다.
/// </summary>
/// <remarks>
/// <para><b>설계 의도</b></para>
/// <para>
/// <c>(bool ok, Exception? error)</c> tuple 대신 명시적 타입을 사용하여
/// 호출부에서 성공/실패를 강제로 처리하도록 유도합니다.
/// </para>
/// <para><b>기본 운용 패턴</b></para>
/// <code>
/// UtilResult r = await action.TryExecuteAsync();
///
/// // 분기 처리
/// if (r.IsOk) DoneWork();
/// else        logger.Error(r.Error!.Message);
///
/// // switch 패턴
/// switch (r) {
///     case { IsOk: true }: ...
///     case { Error: TimeoutException tex }: ...
///     default: ...
/// }
/// </code>
/// </remarks>
public readonly record struct UtilResult(bool IsOk, Exception? Error)
{
    // ─────────────────────────────────────────────
    // §1  상태 프로퍼티
    // ─────────────────────────────────────────────

    /// <summary>실패 여부 (<c>!IsOk</c>).</summary>
    public bool IsError => !IsOk;

    // ─────────────────────────────────────────────
    // §2  결과 처리
    // ─────────────────────────────────────────────

    /// <summary>
    /// 실패 시 원본 예외를 re-throw합니다. 성공 시 아무 동작도 하지 않습니다.
    /// </summary>
    /// <example>
    /// <code>
    /// var r = await action.TryExecuteAsync();
    /// r.ThrowIfError();   // 실패 시 원본 예외 전파
    /// // 이후 코드는 성공이 보장됨
    /// </code>
    /// </example>
    public void ThrowIfError() { if (IsError) throw Error!; }

    /// <summary>
    /// 실패 시 지정 타입으로 캐스팅하여 예외를 re-throw합니다.
    /// </summary>
    /// <typeparam name="TEx">throw할 예외 타입.</typeparam>
    /// <example>
    /// <code>
    /// r.ThrowIfError<CircuitBreakerOpenException>();
    /// </code>
    /// </example>
    public void ThrowIfError<TEx>() where TEx : Exception
    {
        if (IsError && Error is TEx ex) throw ex;
        if (IsError) throw Error!;
    }

    internal static UtilResult Ok() => new(true, null);
    internal static UtilResult Fail(Exception ex) => new(false, ex);
}

/// <summary>
/// 값 있는 성공/실패 결과 타입.<br/>
/// <c>TryExecuteAsync<T></c> 반환 타입으로 사용합니다.
/// </summary>
/// <typeparam name="T">성공 시 반환될 값의 타입.</typeparam>
/// <remarks>
/// <para><b>기본 운용 패턴</b></para>
/// <code>
/// UtilResult<byte[]> r = await func.TryExecuteAsync();
///
/// // 안전한 값 접근
/// byte[] frame  = r.Unwrap();                        // 실패 시 throw
/// byte[] frame2 = r.UnwrapOr(Array.Empty<byte>());   // 실패 시 fallback
///
/// // Map 체이닝
/// UtilResult<uint> id = r.Map(b => BitConverter.ToUInt32(b, 0));
/// </code>
/// </remarks>
public readonly record struct UtilResult<T>(bool IsOk, T? Value, Exception? Error)
{
    // ─────────────────────────────────────────────
    // §1  상태 프로퍼티
    // ─────────────────────────────────────────────

    /// <summary>실패 여부 (<c>!IsOk</c>).</summary>
    public bool IsError => !IsOk;

    // ─────────────────────────────────────────────
    // §2  결과 처리
    // ─────────────────────────────────────────────

    /// <summary>
    /// 성공 시 값을 반환합니다.<br/>
    /// 실패 시 <see cref="InvalidOperationException"/>을 throw합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">결과가 실패인 경우.</exception>
    /// <example>
    /// <code>
    /// var r = await func.TryExecuteAsync();
    /// if (r.IsOk)
    ///     Process(r.Unwrap());  // 성공이 확인된 후 안전하게 호출
    /// </code>
    /// </example>
    public T Unwrap()
        => IsOk ? Value! : throw new InvalidOperationException("UtilResult에 오류가 있습니다.", Error);

    /// <summary>
    /// 실패 시 <paramref name="fallback"/>을 반환합니다. 예외를 throw하지 않습니다.
    /// </summary>
    /// <example>
    /// <code>
    /// byte[] frame = r.UnwrapOr(Array.Empty<byte>());  // 실패 시 빈 배열
    /// string name  = r.UnwrapOr("unknown");              // 실패 시 기본값
    /// </code>
    /// </example>
    public T UnwrapOr(T fallback) => IsOk ? Value! : fallback;

    /// <summary>
    /// 실패 시 <paramref name="factory"/> 람다의 반환값을 사용합니다. 예외를 throw하지 않습니다.
    /// </summary>
    /// <param name="factory">실패 시 대체값을 생성하는 함수. 원본 예외를 인수로 받습니다.</param>
    /// <example>
    /// <code>
    /// byte[] frame = r.UnwrapOrElse(ex =>
    /// {
    ///     logger.Warn($"폴백 사용: {ex!.Message}");
    ///     return _lastValidFrame;
    /// });
    /// </code>
    /// </example>
    public T UnwrapOrElse(Func<Exception?, T> factory)
        => IsOk ? Value! : factory(Error);

    /// <summary>
    /// 실패 시 원본 예외를 re-throw합니다. 성공 시 아무 동작도 하지 않습니다.
    /// </summary>
    public void ThrowIfError() { if (IsError) throw Error!; }

    /// <summary>
    /// 성공 값에 변환 함수를 적용합니다. 실패는 타입만 변경하여 그대로 전파됩니다.
    /// </summary>
    /// <typeparam name="TOut">변환 후 타입.</typeparam>
    /// <param name="mapper">성공 값을 변환할 함수.</param>
    /// <returns>변환된 <see cref="UtilResult{TOut}"/>.</returns>
    /// <example>
    /// <code>
    /// // 수신 → 파싱 → 포맷 체이닝
    /// var raw   = await func.TryExecuteAsync();                            // UtilResult<byte[]>
    /// var id    = raw.Map(b => BitConverter.ToUInt32(b, 0));               // UtilResult<uint>
    /// var label = id.Map(n => $"Sensor-{n:D4}");                           // UtilResult<string>
    ///
    /// string display = label.UnwrapOr("Sensor-????");
    /// </code>
    /// </example>
    public UtilResult<TOut> Map<TOut>(Func<T, TOut> mapper)
        => IsOk ? UtilResults.Ok(mapper(Value!)) : UtilResults.Fail<TOut>(Error!);

    internal static UtilResult<T> Ok(T value) => new(true, value, null);
    internal static UtilResult<T> Fail(Exception ex) => new(false, default, ex);
}

/// <summary>
/// <see cref="UtilResult"/> · <see cref="UtilResult{T}"/> 팩토리 클래스.<br/>
/// <c>UtilResults.Ok()</c> · <c>UtilResults.Fail(ex)</c> 형태로 사용합니다.
/// </summary>
/// <example>
/// <code>
/// // 성공 생성
/// return UtilResults.Ok();           // 값 없음
/// return UtilResults.Ok(frameData);  // 값 포함
///
/// // 실패 생성
/// return UtilResults.Fail(ex);
/// return UtilResults.Fail<byte[]>(ex);
/// </code>
/// </example>
public static class UtilResults
{
    /// <summary>성공 결과 생성 (값 없음).</summary>
    public static UtilResult Ok() => new(true, null);

    /// <summary>성공 결과 생성 (값 포함).</summary>
    public static UtilResult<T> Ok<T>(T value) => new(true, value, null);

    /// <summary>실패 결과 생성 (값 없음).</summary>
    public static UtilResult Fail(Exception ex) => new(false, ex);

    /// <summary>실패 결과 생성 (값 없음, 제네릭).</summary>
    public static UtilResult<T> Fail<T>(Exception ex) => new(false, default, ex);
}