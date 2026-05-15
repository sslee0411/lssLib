// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetResult.cs
//  역할: 통신 작업 결과 값 타입 (WriteAsync / RequestAsync 반환)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 통신 작업(RequestAsync)의 결과를 나타내는 불변 값 타입.
/// </summary>
/// <remarks>
/// <b>기본 사용 패턴:</b>
/// <code>
/// NetResult r = await channel.RequestAsync(queryFrame);
///
/// // ── 패턴 1: IsOk / IsError 분기 ─────────────────────────────
/// if (r.IsOk)
/// {
///     // lssLib.Binary 파싱 연동
///     // var result = r.Data!.ToParser().Parse(MySchema.Default);
///     // float temp = result.GetFloat("Temperature");
/// }
/// else
///     LogManager.Instance.Error("PLC", r.Error!.Message);
///
/// // ── 패턴 2: 실패 시 기본값 ────────────────────────────────────
/// byte[] data = r.DataOr(Array.Empty<byte>());
///
/// // ── 패턴 3: 실패 시 예외 전파 ─────────────────────────────────
/// r.ThrowIfError();
/// ProcessFrame(r.Data!);
///
/// // ── 패턴 4: 타입 변환 (Map) ───────────────────────────────────
/// NetResult<float> temp = r.Map(bytes =>
///     bytes.ToParser().Parse(SensorSchema.Default).GetFloat("Temperature"));
/// if (temp.IsOk) UpdateDisplay(temp.Value);
/// </code>
///
/// <b>IsError 가 되는 경우:</b>
/// <list type="bullet">
///   <item><description>연결 없는 상태에서 RequestAsync 호출</description></item>
///   <item><description>RequestAsync 응답 타임아웃</description></item>
///   <item><description>INetProtocol.TryDecode 실패 (CRC 오류, 프레임 손상)</description></item>
///   <item><description>Transport.WriteAsync / ReadAsync 예외 발생</description></item>
/// </list>
/// </remarks>
public readonly record struct NetResult
{
    #region §1 ─ 프로퍼티

    /// <summary>성공 여부. true 이면 Data 에 수신 페이로드가 있습니다.</summary>
    public bool IsOk { get; init; }

    /// <summary>실패 여부 (!IsOk).</summary>
    public bool IsError => !IsOk;

    /// <summary>성공 시 수신 디코딩된 페이로드. 실패 시 null.</summary>
    public byte[]? Data { get; init; }

    /// <summary>실패 시 원본 예외. 성공 시 null.</summary>
    public Exception? Error { get; init; }

    /// <summary>결과 생성 시각.</summary>
    public DateTime Timestamp { get; init; }

    #endregion

    #region §2 ─ 팩토리

    /// <summary>수신 데이터를 포함한 성공 결과를 생성합니다.</summary>
    public static NetResult Ok(byte[] data) => new()
    { IsOk = true, Data = data, Error = null, Timestamp = DateTime.Now };

    /// <summary>페이로드 없는 성공 결과를 생성합니다 (WriteAsync 큐 투입 성공).</summary>
    public static NetResult OkEmpty() => new()
    { IsOk = true, Data = Array.Empty<byte>(), Error = null, Timestamp = DateTime.Now };

    /// <summary>원본 예외를 포함한 실패 결과를 생성합니다.</summary>
    public static NetResult Fail(Exception error) => new()
    { IsOk = false, Data = null, Error = error, Timestamp = DateTime.Now };

    /// <summary>메시지 문자열로 실패 결과를 생성합니다.</summary>
    public static NetResult Fail(string message)
        => Fail(new InvalidOperationException(message));

    #endregion

    #region §3 ─ 편의 메서드

    /// <summary>성공 시 Data, 실패 시 fallback 을 반환합니다. 예외 없음.</summary>
    public byte[] DataOr(byte[] fallback) => IsOk ? Data! : fallback;

    /// <summary>실패 시 원본 예외를 re-throw 합니다 (스택 트레이스 보존).</summary>
    public void ThrowIfError()
    {
        if (IsError)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                  .Capture(Error!).Throw();
    }

    /// <summary>성공 값을 다른 타입으로 변환합니다. 실패는 그대로 전파됩니다.</summary>
    public NetResult<T> Map<T>(Func<byte[], T> mapper) where T : notnull
    {
        if (IsError) return NetResult<T>.Fail(Error!);
        try { return NetResult<T>.Ok(mapper(Data!)); }
        catch (Exception ex) { return NetResult<T>.Fail(ex); }
    }

    #endregion
}

/// <summary>파싱된 값을 포함하는 통신 결과 값 타입.</summary>
/// <typeparam name="T">성공 값 타입 (예: float, BufResult)</typeparam>
/// <example><code>
/// NetResult&lt;float&gt; temp = rawResult.Map(bytes =>
///     bytes.ToParser().Parse(SensorSchema.Default).GetFloat("Temperature"));
///
/// float value = temp.ValueOr(0f);
/// </code></example>
public readonly record struct NetResult<T> where T : notnull
{
    /// <summary>성공 여부.</summary>
    public bool IsOk { get; init; }

    /// <summary>실패 여부 (!IsOk).</summary>
    public bool IsError => !IsOk;

    /// <summary>성공 시 파싱된 값. 실패 시 default.</summary>
    public T? Value { get; init; }

    /// <summary>실패 시 원본 예외. 성공 시 null.</summary>
    public Exception? Error { get; init; }

    /// <summary>결과 생성 시각.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>성공 결과를 생성합니다.</summary>
    public static NetResult<T> Ok(T value) => new()
    { IsOk = true, Value = value, Timestamp = DateTime.Now };

    /// <summary>실패 결과를 생성합니다.</summary>
    public static NetResult<T> Fail(Exception error) => new()
    { IsOk = false, Error = error, Timestamp = DateTime.Now };

    /// <summary>성공 시 Value, 실패 시 fallback 반환.</summary>
    public T ValueOr(T fallback) => IsOk ? Value! : fallback;

    /// <summary>실패 시 원본 예외를 re-throw 합니다 (스택 트레이스 보존).</summary>
    public void ThrowIfError()
    {
        if (IsError)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                  .Capture(Error!).Throw();
    }
}