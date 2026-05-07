// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetResult.cs
//  역할: 통신 작업 결과 값 타입
//
//  ┌─ 설계 의도 ────────────────────────────────────────────────────┐
//  │                                                                 │
//  │  WriteAsync / RequestAsync 의 결과를 예외 없이 표현합니다.      │
//  │  lssLib.Retry.UtilResult 와 동일한 패턴으로 설계되었습니다.    │
//  │                                                                 │
//  │  IsOk = true  → Data 에 수신 페이로드 포함                     │
//  │  IsOk = false → Error 에 원본 예외 포함                        │
//  │                                                                 │
//  │  연결 끊김, 타임아웃, 디코딩 실패 모두 IsError 로 표현됩니다.  │
//  └─────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 통신 작업(Write / RequestAsync)의 결과를 나타내는 불변 값 타입.
/// </summary>
/// <remarks>
/// <para>
/// <b>v3 변경사항: <c>WriteAsync</c> 도 <c>NetResult</c> 를 반환합니다.</b>
/// 연결이 끊긴 상태에서 <c>WriteAsync</c> 를 호출하면 큐에 넣지 않고
/// 즉시 <see cref="Fail(string)"/> 을 반환합니다.
/// </para>
///
/// <b>기본 사용 패턴:</b>
/// <code>
/// // ── 패턴 1: IsOk / IsError 분기 ──────────────────────────────
/// NetResult r = await channel.RequestAsync(queryFrame);
///
/// if (r.IsOk)
/// {
///     // lssLib.Binary 연동
///     // var result = r.Data!.ToParser().Parse(MySchema.Default);
///     // float temp = result.GetFloat("Temperature");
///     LogManager.Instance.Info("PLC", $"수신 {r.Data!.Length}B");
/// }
/// else
/// {
///     LogManager.Instance.Error("PLC", r.Error!.Message);
///
///     // 연결 상태 추가 확인
///     if (!channel.IsConnected)
///         UpdateUI_Disconnected();
/// }
///
/// // ── 패턴 2: 실패 시 기본값 ───────────────────────────────────
/// byte[] data = r.DataOr(Array.Empty&lt;byte&gt;());
///
/// // ── 패턴 3: 실패 시 예외 re-throw ────────────────────────────
/// r.ThrowIfError();           // 실패면 원본 예외를 그대로 throw
/// ProcessFrame(r.Data!);      // 이 줄은 성공 시에만 실행됨
///
/// // ── 패턴 4: 타입 변환 (Map) ──────────────────────────────────
/// // lssLib.Binary 파싱 결과로 변환
/// NetResult&lt;float&gt; temp = r.Map(bytes =>
///     bytes.ToParser().Parse(SensorSchema.Default).GetFloat("Temperature"));
///
/// if (temp.IsOk)
///     UpdateTemperature(temp.Value);
///
/// // ── 패턴 5: WriteAsync 결과 확인 (v3 신규) ───────────────────
/// NetResult wr = await channel.WriteAsync(setpointFrame);
/// if (wr.IsError)
///     LogManager.Instance.Warn("PLC", $"Write 실패: {wr.Error!.Message}");
/// </code>
///
/// <b>IsError 가 되는 경우:</b>
/// <list type="bullet">
///   <item><description>연결이 끊긴 상태에서 WriteAsync / RequestAsync 호출</description></item>
///   <item><description>RequestAsync 응답 타임아웃</description></item>
///   <item><description>INetProtocol.TryDecode 실패 (CRC 오류, 프레임 손상)</description></item>
///   <item><description>Transport.WriteAsync / ReadAsync 에서 예외 발생</description></item>
/// </list>
/// </remarks>
public readonly record struct NetResult
{
    #region §1 ─ 프로퍼티

    /// <summary>
    /// 성공 여부.
    /// <para><c>true</c> 이면 <see cref="Data"/> 에 수신 페이로드가 있습니다.</para>
    /// <para><c>false</c> 이면 <see cref="Error"/> 에 원인 예외가 있습니다.</para>
    /// </summary>
    public bool IsOk { get; init; }

    /// <summary>
    /// 실패 여부 (<c>!IsOk</c>).
    /// <para><c>true</c> 이면 <see cref="Error"/> 를 확인하세요.</para>
    /// </summary>
    public bool IsError => !IsOk;

    /// <summary>
    /// 성공 시 수신 디코딩된 페이로드.
    /// <para>실패 시 <c>null</c>. 반드시 <see cref="IsOk"/> 확인 후 접근하세요.</para>
    /// <para>lssLib.Binary 연동: <c>r.Data!.ToParser().Parse(schema)</c></para>
    /// </summary>
    public byte[]? Data { get; init; }

    /// <summary>
    /// 실패 시 원본 예외.
    /// <para>성공 시 <c>null</c>. 반드시 <see cref="IsError"/> 확인 후 접근하세요.</para>
    /// <para>타임아웃이면 <see cref="TimeoutException"/>, 연결 끊김이면 <see cref="InvalidOperationException"/>.</para>
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// 결과 생성 시각 (처리 완료 시각).
    /// <para>응답 시간 측정에 활용할 수 있습니다.</para>
    /// </summary>
    public DateTime Timestamp { get; init; }

    #endregion

    #region §2 ─ 팩토리 (외부에서 직접 생성 시 사용)

    /// <summary>
    /// 수신 데이터를 포함한 성공 결과를 생성합니다.
    /// <para>주로 <see cref="NetChannelBase"/> 내부에서 사용됩니다.</para>
    /// </summary>
    /// <param name="data">디코딩된 수신 페이로드</param>
    public static NetResult Ok(byte[] data) => new()
    {
        IsOk = true,
        Data = data,
        Error = null,
        Timestamp = DateTime.Now
    };

    /// <summary>
    /// 페이로드 없는 성공 결과를 생성합니다.
    /// <para><c>WriteAsync</c> 의 큐 투입 성공 결과로 사용됩니다.</para>
    /// </summary>
    public static NetResult OkEmpty() => new()
    {
        IsOk = true,
        Data = Array.Empty<byte>(),
        Error = null,
        Timestamp = DateTime.Now
    };

    /// <summary>
    /// 원본 예외를 포함한 실패 결과를 생성합니다.
    /// </summary>
    /// <param name="error">원인 예외 (스택 트레이스 보존)</param>
    public static NetResult Fail(Exception error) => new()
    {
        IsOk = false,
        Data = null,
        Error = error,
        Timestamp = DateTime.Now
    };

    /// <summary>
    /// 메시지 문자열로 실패 결과를 생성합니다.
    /// <para>내부적으로 <see cref="InvalidOperationException"/> 을 생성합니다.</para>
    /// </summary>
    /// <param name="message">오류 메시지</param>
    public static NetResult Fail(string message)
        => Fail(new InvalidOperationException(message));

    #endregion

    #region §3 ─ 편의 메서드

    /// <summary>
    /// 성공 시 <see cref="Data"/> 를 반환하고, 실패 시 <paramref name="fallback"/> 을 반환합니다.
    /// <para>예외 없이 항상 값을 반환합니다.</para>
    /// </summary>
    /// <param name="fallback">실패 시 대체 값 (주로 <c>Array.Empty&lt;byte&gt;()</c>)</param>
    /// <example><code>
    /// byte[] raw = r.DataOr(Array.Empty&lt;byte&gt;());
    /// </code></example>
    public byte[] DataOr(byte[] fallback) => IsOk ? Data! : fallback;

    /// <summary>
    /// 실패 시 원본 예외를 re-throw 합니다.
    /// <para>
    /// <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/> 를 사용하여
    /// 원본 스택 트레이스를 보존합니다.
    /// </para>
    /// <para>성공이면 아무 동작도 하지 않습니다.</para>
    /// </summary>
    /// <example><code>
    /// // 예외를 그대로 전파하고 싶을 때
    /// NetResult r = await channel.RequestAsync(frame);
    /// r.ThrowIfError();           // 실패면 여기서 예외 발생
    /// ProcessFrame(r.Data!);      // 성공 시에만 실행
    /// </code></example>
    public void ThrowIfError()
    {
        if (IsError)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                  .Capture(Error!).Throw();
    }

    /// <summary>
    /// 성공 값을 다른 타입으로 변환합니다.
    /// <para>실패는 그대로 전파되어 <see cref="NetResult{T}.IsError"/> 가 됩니다.</para>
    /// <para>변환 중 예외 발생 시도 <see cref="NetResult{T}.IsError"/> 로 처리됩니다.</para>
    /// </summary>
    /// <typeparam name="T">변환할 타입 (예: <c>float</c>, <c>BufResult</c>)</typeparam>
    /// <param name="mapper">성공 값 변환 함수</param>
    /// <example><code>
    /// // lssLib.Binary 파싱 결과로 변환
    /// NetResult&lt;float&gt; temp = r.Map(bytes =>
    ///     bytes.ToParser().Parse(SensorSchema.Default).GetFloat("Temperature"));
    ///
    /// if (temp.IsOk)
    ///     Dispatcher.InvokeAsync(() => TxtTemp.Text = $"{temp.Value:F2} °C");
    /// </code></example>
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
/// <typeparam name="T">성공 값 타입 (예: <c>float</c>, <c>BufResult</c>)</typeparam>
/// <remarks>
/// <see cref="NetResult.Map{T}"/> 의 반환 타입입니다.
/// 직접 생성할 수도 있습니다.
///
/// <b>사용 예시:</b>
/// <code>
/// // Map 을 통한 생성 (권장)
/// NetResult&lt;float&gt; temp = rawResult.Map(bytes =>
///     bytes.ToParser().Parse(SensorSchema.Default).GetFloat("Temperature"));
///
/// float value = temp.ValueOr(0f);   // 실패 시 0
///
/// // 직접 사용
/// if (temp.IsOk)
///     UpdateUI(temp.Value);
/// else
///     LogError(temp.Error!.Message);
/// </code>
/// </remarks>
public readonly record struct NetResult<T> where T : notnull
{
    /// <summary>성공 여부.</summary>
    public bool IsOk { get; init; }

    /// <summary>실패 여부 (<c>!IsOk</c>).</summary>
    public bool IsError => !IsOk;

    /// <summary>성공 시 파싱된 값. 실패 시 <c>default</c>.</summary>
    public T? Value { get; init; }

    /// <summary>실패 시 원본 예외. 성공 시 <c>null</c>.</summary>
    public Exception? Error { get; init; }

    /// <summary>결과 생성 시각.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>성공 결과를 생성합니다.</summary>
    public static NetResult<T> Ok(T value) => new()
    { IsOk = true, Value = value, Timestamp = DateTime.Now };

    /// <summary>실패 결과를 생성합니다.</summary>
    public static NetResult<T> Fail(Exception error) => new()
    { IsOk = false, Error = error, Timestamp = DateTime.Now };

    /// <summary>성공 시 <see cref="Value"/> 반환, 실패 시 <paramref name="fallback"/> 반환.</summary>
    public T ValueOr(T fallback) => IsOk ? Value! : fallback;

    /// <summary>실패 시 원본 예외를 re-throw 합니다 (스택 트레이스 보존).</summary>
    public void ThrowIfError()
    {
        if (IsError)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                  .Capture(Error!).Throw();
    }
}