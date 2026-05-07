// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · NetResult.cs
//  역할: 전송·수신 결과 값 타입 (lssLib.Retry.UtilResult 와 동일 패턴)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 통신 작업(Write / Request)의 결과를 나타내는 불변 값 타입.
/// </summary>
/// <remarks>
/// <example><code>
/// NetResult r = await channel.RequestAsync(frame);
///
/// if (r.IsOk)
///     var parsed = r.Data!.ToParser().Parse(MySchema.Default);
/// else
///     LogManager.Instance.Error("Net", r.Error!.Message);
///
/// // 실패 시 기본값
/// byte[] data = r.DataOr(Array.Empty&lt;byte&gt;());
/// </code></example>
/// </remarks>
public readonly record struct NetResult
{
    #region §1 ─ 프로퍼티

    /// <summary>성공 여부.</summary>
    public bool IsOk { get; init; }

    /// <summary>실패 여부 (<c>!IsOk</c>).</summary>
    public bool IsError => !IsOk;

    /// <summary>성공 시 수신 데이터. 실패 시 <c>null</c>.</summary>
    public byte[]? Data { get; init; }

    /// <summary>실패 시 원본 예외. 성공 시 <c>null</c>.</summary>
    public Exception? Error { get; init; }

    /// <summary>결과 생성 시각.</summary>
    public DateTime Timestamp { get; init; }

    #endregion

    #region §2 ─ 팩토리

    /// <summary>성공 결과를 생성합니다.</summary>
    public static NetResult Ok(byte[] data) => new()
    {
        IsOk = true,
        Data = data,
        Error = null,
        Timestamp = DateTime.Now
    };

    /// <summary>데이터 없는 성공 결과 (Write 완료 등).</summary>
    public static NetResult OkEmpty() => new()
    {
        IsOk = true,
        Data = Array.Empty<byte>(),
        Error = null,
        Timestamp = DateTime.Now
    };

    /// <summary>실패 결과를 생성합니다.</summary>
    public static NetResult Fail(Exception error) => new()
    {
        IsOk = false,
        Data = null,
        Error = error,
        Timestamp = DateTime.Now
    };

    /// <summary>메시지만으로 실패 결과를 생성합니다.</summary>
    public static NetResult Fail(string message) =>
        Fail(new InvalidOperationException(message));

    #endregion

    #region §3 ─ 편의 메서드

    /// <summary>성공 시 Data 를 반환, 실패 시 <paramref name="fallback"/> 을 반환합니다.</summary>
    public byte[] DataOr(byte[] fallback) => IsOk ? Data! : fallback;

    /// <summary>실패 시 원본 예외를 re-throw 합니다.</summary>
    public void ThrowIfError()
    {
        if (IsError) System.Runtime.ExceptionServices.ExceptionDispatchInfo
            .Capture(Error!).Throw();
    }

    /// <summary>결과를 다른 타입으로 변환합니다. 실패는 그대로 전파됩니다.</summary>
    public NetResult<T> Map<T>(Func<byte[], T> mapper) where T : notnull
    {
        if (IsError) return NetResult<T>.Fail(Error!);
        try { return NetResult<T>.Ok(mapper(Data!)); }
        catch (Exception ex) { return NetResult<T>.Fail(ex); }
    }

    #endregion
}

/// <summary>
/// 파싱된 값을 포함하는 통신 결과 값 타입.
/// </summary>
/// <typeparam name="T">성공 값 타입 (예: <c>BufResult</c>)</typeparam>
public readonly record struct NetResult<T> where T : notnull
{
    public bool IsOk { get; init; }
    public bool IsError => !IsOk;
    public T? Value { get; init; }
    public Exception? Error { get; init; }
    public DateTime Timestamp { get; init; }

    public static NetResult<T> Ok(T value) => new()
    { IsOk = true, Value = value, Timestamp = DateTime.Now };

    public static NetResult<T> Fail(Exception error) => new()
    { IsOk = false, Error = error, Timestamp = DateTime.Now };

    /// <summary>성공 시 값 반환, 실패 시 <paramref name="fallback"/> 반환.</summary>
    public T ValueOr(T fallback) => IsOk ? Value! : fallback;

    /// <summary>실패 시 원본 예외를 re-throw 합니다.</summary>
    public void ThrowIfError()
    {
        if (IsError) System.Runtime.ExceptionServices.ExceptionDispatchInfo
            .Capture(Error!).Throw();
    }
}