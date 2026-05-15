namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — RetryExtensions
//  Retry 패턴 · Timeout 래퍼 · 안전 실행 확장 메서드
//  ▸ No abstractions  ▸ Extension-method only
//  ▸ RetryPolicy record struct 기반 — 파라미터 sprawl 해소
//  ▸ CancellationToken 일관 전파, OperationCanceledException 보호
//
//  ★ Func 변수 선언 패턴 (컴파일러 타입 추론 필요)
//    Func<Task> action = async () => { ... };
//    await action.RetryAsync(policy, ct);
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Retry · Timeout · 안전 실행 확장 메서드.<br/>
/// <see cref="RetryPolicy"/>를 통해 설정을 재사용하고 호출부를 간결하게 유지합니다.
/// </summary>
/// <remarks>
/// <para><b>CancellationToken 동작</b></para>
/// <para>
/// 모든 비동기 오버로드는 <see cref="CancellationToken"/>을 수용합니다.
/// <see cref="OperationCanceledException"/>은 재시도 없이 즉시 상위로 전파됩니다.
/// </para>
/// <para><b>Func 변수 선언 패턴</b></para>
/// <para>
/// C# 컴파일러는 람다에 직접 확장 메서드를 체이닝할 때 타입 추론에 실패합니다.
/// <c>Func</c> 변수에 먼저 대입한 뒤 확장 메서드를 호출하세요.
/// </para>
/// <code>
/// // ✗ 컴파일 오류
/// (() => DoWorkAsync()).RetryAsync(policy);
///
/// // ✓ 올바른 패턴
/// Func<Task> action = () => DoWorkAsync();
/// await action.RetryAsync(policy, ct);
///
/// // ✓ CancellationToken 수용 패턴
/// Func<CancellationToken, Task<T>> func = ct => DoWorkAsync(ct);
/// var result = await func.ExecuteAsync(state, ct);
/// </code>
/// </remarks>
public static class RetryExtensions
{
    // ─────────────────────────────────────────────
    // §1  동기 Retry
    // ─────────────────────────────────────────────

    /// <summary>
    /// <see cref="Action"/>을 <see cref="RetryPolicy"/> 설정대로 재시도합니다.<br/>
    /// 모든 시도가 실패하면 마지막 예외를 throw합니다.
    /// </summary>
    /// <param name="action">재시도할 액션.</param>
    /// <param name="policy">Retry 정책. <c>null</c>이면 <see cref="RetryPolicy.Default"/> 적용.</param>
    /// <example>
    /// <code>
    /// // 시리얼 포트 초기화 — 2회 실패 후 3회째 성공
    /// Action openPort = () =>
    /// {
    ///     port.Open();
    ///     port.Write(PingCommand);
    ///     ValidateAck(port.ReadLine());
    /// };
    /// openPort.Retry(new RetryPolicy(
    ///     MaxAttempts: 5,
    ///     Delay:       TimeSpan.FromMilliseconds(500)));
    ///
    /// // 캐시 플러시 — 즉시 재시도
    /// Action flush = () => cache.Flush();
    /// flush.Retry(RetryPolicy.Immediate);
    /// </code>
    /// </example>
    public static void Retry(this Action action, RetryPolicy? policy = null)
    {
        var p = policy ?? RetryPolicy.Default;
        Exception? last = null;
        for (int i = 0; i < p.MaxAttempts; i++)
        {
            try { action(); return; }
            catch (Exception ex)
            {
                last = ex;
                p.OnRetry?.Invoke(ex, i + 1);
                if (i < p.MaxAttempts - 1) Thread.Sleep(p.GetDelay(i));
            }
        }
        throw last!;
    }

    /// <summary>
    /// <see cref="Func{T}"/>를 <see cref="RetryPolicy"/> 설정대로 재시도하여 결과를 반환합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func">재시도할 함수.</param>
    /// <param name="policy">Retry 정책.</param>
    /// <returns>성공 시 반환값.</returns>
    /// <example>
    /// <code>
    /// // 레지스트리 읽기 재시도
    /// Func<string> readPort = () => registry.Read("port_name");
    /// string portName = readPort.Retry(RetryPolicy.Default);
    ///
    /// // 상태 읽기 즉시 재시도
    /// Func<int> getStatus = () => device.GetStatus();
    /// int status = getStatus.Retry(RetryPolicy.Immediate);
    /// </code>
    /// </example>
    public static T Retry<T>(this Func<T> func, RetryPolicy? policy = null)
    {
        var p = policy ?? RetryPolicy.Default;
        Exception? last = null;
        for (int i = 0; i < p.MaxAttempts; i++)
        {
            try { return func(); }
            catch (Exception ex)
            {
                last = ex;
                p.OnRetry?.Invoke(ex, i + 1);
                if (i < p.MaxAttempts - 1) Thread.Sleep(p.GetDelay(i));
            }
        }
        throw last!;
    }

    // ─────────────────────────────────────────────
    // §2  비동기 Retry
    // ─────────────────────────────────────────────

    /// <summary>
    /// 비동기 <see cref="Func{Task}"/>를 <see cref="RetryPolicy"/> 설정대로 재시도합니다.<br/>
    /// <see cref="OperationCanceledException"/>은 재시도 없이 즉시 throw됩니다.
    /// </summary>
    /// <param name="action">재시도할 비동기 액션.</param>
    /// <param name="policy">Retry 정책.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <example>
    /// <code>
    /// // DB 재연결 루프
    /// Func<Task> connect = async () =>
    /// {
    ///     await dbConn.OpenAsync(ct);
    /// };
    /// await connect.RetryAsync(new RetryPolicy(
    ///     MaxAttempts: 20,
    ///     Delay: TimeSpan.FromSeconds(3),
    ///     OnRetry: (_, n) => logger.Info($"DB 연결 시도 {n}/20...")),
    ///     ct);
    ///
    /// // Heartbeat 재시도
    /// Func<Task> heartbeat = () => SendHeartbeatAsync();
    /// await heartbeat.RetryAsync(RetryPolicy.Http, ct);
    /// </code>
    /// </example>
    public static async Task RetryAsync(
        this Func<Task> action,
        RetryPolicy? policy = null,
        CancellationToken ct = default)
    {
        var p = policy ?? RetryPolicy.Default;
        Exception? last = null;
        for (int i = 0; i < p.MaxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            try { await action(); return; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                p.OnRetry?.Invoke(ex, i + 1);
                if (i < p.MaxAttempts - 1) await Task.Delay(p.GetDelay(i), ct);
            }
        }
        throw last!;
    }

    /// <summary>
    /// 비동기 <see cref="Func{Task{T}}"/>를 <see cref="RetryPolicy"/> 설정대로 재시도하여 결과를 반환합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <example>
    /// <code>
    /// // HTTP API 결과 반환
    /// Func<Task<ApiResp>> callApi = () => client.GetAsync<ApiResp>(url);
    /// ApiResp resp = await callApi.RetryAsync(RetryPolicy.Http, ct);
    ///
    /// // 프레임 수신 재시도
    /// Func<Task<byte[]>> recvFrame = () => ReadFrameAsync(ct);
    /// byte[] frame = await recvFrame.RetryAsync(RetryPolicy.Default, ct);
    /// </code>
    /// </example>
    public static async Task<T> RetryAsync<T>(
        this Func<Task<T>> func,
        RetryPolicy? policy = null,
        CancellationToken ct = default)
    {
        var p = policy ?? RetryPolicy.Default;
        Exception? last = null;
        for (int i = 0; i < p.MaxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await func(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                p.OnRetry?.Invoke(ex, i + 1);
                if (i < p.MaxAttempts - 1) await Task.Delay(p.GetDelay(i), ct);
            }
        }
        throw last!;
    }

    // ─────────────────────────────────────────────
    // §3  조건부 Retry — 특정 예외 타입만 재시도
    // ─────────────────────────────────────────────

    /// <summary>
    /// <typeparamref name="TEx"/> 타입 예외만 재시도합니다.<br/>
    /// 다른 예외와 <see cref="OperationCanceledException"/>은 즉시 throw됩니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <typeparam name="TEx">재시도할 예외 타입.</typeparam>
    /// <remarks>
    /// <para>
    /// 일시적 오류(<c>SqlException</c>, <c>HttpRequestException</c>)만 재시도하고
    /// 치명적 오류(<c>NullReferenceException</c> 등)는 즉시 전파할 때 사용합니다.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // SqlException만 재시도, 그 외 즉시 throw
    /// Func<Task<DataRow>> query = () => db.QuerySingleAsync(sql);
    /// DataRow row = await query.RetryOnAsync<DataRow, SqlException>(
    ///     RetryPolicy.Database, ct);
    ///
    /// // HttpRequestException만 재시도
    /// Func<Task<string>> fetch = () => client.GetStringAsync(url);
    /// string body = await fetch.RetryOnAsync<string, HttpRequestException>(
    ///     RetryPolicy.Http, ct);
    /// </code>
    /// </example>
    public static async Task<T> RetryOnAsync<T, TEx>(
        this Func<Task<T>> func,
        RetryPolicy? policy = null,
        CancellationToken ct = default)
        where TEx : Exception
    {
        var p = policy ?? RetryPolicy.Default;
        Exception? last = null;
        for (int i = 0; i < p.MaxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await func(); }
            catch (OperationCanceledException) { throw; }
            catch (TEx ex)
            {
                last = ex;
                p.OnRetry?.Invoke(ex, i + 1);
                if (i < p.MaxAttempts - 1) await Task.Delay(p.GetDelay(i), ct);
            }
        }
        throw last!;
    }

    // ─────────────────────────────────────────────
    // §4  Timeout 래퍼
    // ─────────────────────────────────────────────

    /// <summary>
    /// 비동기 함수에 타임아웃을 적용합니다.<br/>
    /// 초과 시 <paramref name="operationName"/>이 포함된 한국어 <see cref="TimeoutException"/>을 throw합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func"><see cref="CancellationToken"/>을 수용하는 비동기 함수.</param>
    /// <param name="timeout">타임아웃 시간.</param>
    /// <param name="operationName">작업 이름 (예외 메시지에 포함). 선택.</param>
    /// <example>
    /// <code>
    /// // 센서 읽기 3초 타임아웃
    /// Func<CancellationToken, Task<byte[]>> read =
    ///     ct => sensor.ReadFrameAsync(ct);
    /// byte[] frame = await read.WithTimeout(
    ///     TimeSpan.FromSeconds(3),
    ///     operationName: "센서 읽기");
    /// // 초과 시: "작업 '센서 읽기'이 3000 ms 내에 완료되지 않았습니다."
    ///
    /// // 타임아웃 예외 처리
    /// try {
    ///     var data = await func.WithTimeout(TimeSpan.FromSeconds(2), "초기화");
    /// }
    /// catch (TimeoutException tex) {
    ///     logger.Error(tex.Message);
    ///     await device.ResetAsync(ct);
    /// }
    /// </code>
    /// </example>
    public static async Task<T> WithTimeout<T>(
        this Func<CancellationToken, Task<T>> func,
        TimeSpan timeout,
        string? operationName = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        try { return await func(cts.Token); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        { throw new TimeoutException(BuildTimeoutMsg(operationName, timeout)); }
    }

    /// <summary>비동기 액션에 타임아웃을 적용합니다.</summary>
    /// <example>
    /// <code>
    /// Func<CancellationToken, Task>warmUp = ct => cache.WarmUpAsync(ct);
    /// await warmUp.WithTimeout(TimeSpan.FromSeconds(30), "캐시 워밍업");
    /// </code>
    /// </example>
    public static async Task WithTimeout(
        this Func<CancellationToken, Task> action,
        TimeSpan timeout,
        string? operationName = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        try { await action(cts.Token); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        { throw new TimeoutException(BuildTimeoutMsg(operationName, timeout)); }
    }

    // ─────────────────────────────────────────────
    // §5  Retry + Timeout 조합
    // ─────────────────────────────────────────────

    /// <summary>
    /// <see cref="RetryPolicy"/> 설정으로 재시도하면서 두 단계의 타임아웃을 동시에 적용합니다.
    /// </summary>
    /// <typeparam name="T">반환값 타입.</typeparam>
    /// <param name="func">실행할 비동기 함수.</param>
    /// <param name="policy">Retry 정책.</param>
    /// <param name="perAttemptTimeout">단일 시도 최대 허용 시간. <c>null</c>이면 제한 없음.</param>
    /// <param name="totalTimeout">전체 재시도 합산 최대 허용 시간. <c>null</c>이면 제한 없음.</param>
    /// <returns>성공 시 결과값.</returns>
    /// <exception cref="TimeoutException">전체 타임아웃이 초과된 경우.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="perAttemptTimeout"/>: 한 번의 시도가 이 시간을 초과하면 해당 시도를 취소하고 재시도합니다.<br/>
    /// <paramref name="totalTimeout"/>: 이 시간을 초과하면 전체 재시도를 중단하고 <see cref="TimeoutException"/>을 throw합니다.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 외부 REST API — 시도당 3초, 전체 15초
    /// Func<CancellationToken, Task<OrderResp>> order =
    ///     ct => api.PostAsync<OrderResp>(req, ct);
    /// var result = await order.RetryWithTimeout(
    ///     policy:            RetryPolicy.Http,
    ///     perAttemptTimeout: TimeSpan.FromSeconds(3),
    ///     totalTimeout:      TimeSpan.FromSeconds(15));
    ///
    /// // lssLib.Serialization WpfDemo 탭8
    /// // STX 프레임 수신 — 시도당 200ms, 전체 5초
    /// Func<CancellationToken, Task<byte[]>> recv =
    ///     ct => stream.ReadNextFrameAsync(ct);
    /// byte[] frame = await recv.RetryWithTimeout(
    ///     policy:            new RetryPolicy(MaxAttempts: 3, Delay: TimeSpan.Zero),
    ///     perAttemptTimeout: TimeSpan.FromMilliseconds(200),
    ///     totalTimeout:      TimeSpan.FromSeconds(5));
    /// </code>
    /// </example>
    public static async Task<T> RetryWithTimeout<T>(
        this Func<CancellationToken, Task<T>> func,
        RetryPolicy? policy = null,
        TimeSpan? perAttemptTimeout = null,
        TimeSpan? totalTimeout = null)
    {
        var p = policy ?? RetryPolicy.Default;
        using var totalCts = totalTimeout.HasValue
            ? new CancellationTokenSource(totalTimeout.Value)
            : new CancellationTokenSource();

        Exception? last = null;
        for (int i = 0; i < p.MaxAttempts; i++)
        {
            totalCts.Token.ThrowIfCancellationRequested();
            try
            {
                if (perAttemptTimeout.HasValue)
                {
                    using var aCts = CancellationTokenSource
                        .CreateLinkedTokenSource(totalCts.Token);
                    aCts.CancelAfter(perAttemptTimeout.Value);
                    return await func(aCts.Token);
                }
                return await func(totalCts.Token);
            }
            catch (OperationCanceledException) when (totalCts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"전체 타임아웃 {totalTimeout?.TotalMilliseconds:0} ms 초과 " +
                    $"(시도 {i + 1}/{p.MaxAttempts})");
            }
            catch (Exception ex)
            {
                last = ex;
                p.OnRetry?.Invoke(ex, i + 1);
                if (i < p.MaxAttempts - 1)
                    await Task.Delay(p.GetDelay(i), totalCts.Token);
            }
        }
        throw last!;
    }

    // ─────────────────────────────────────────────
    // §6  TryExecute — 예외 없는 안전 실행
    // ─────────────────────────────────────────────

    /// <summary>
    /// <see cref="Action"/>을 예외 없이 실행합니다.<br/>
    /// 실패 시 <paramref name="error"/>에 예외를 저장하고 <c>false</c>를 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 앱 종료 시 정리 작업
    /// Action disconnect = () => device.Disconnect();
    /// if (!disconnect.TryExecute(out var err))
    ///     logger.Warn($"연결 해제 실패 (무시): {err!.Message}");
    ///
    /// // 복수 정리 작업 — 실패해도 계속 진행
    /// var cleanups = new Action[] { FlushBuffer, ClosePort, SaveLog };
    /// foreach (var cleanup in cleanups)
    ///     cleanup.TryExecute(out _);
    /// </code>
    /// </example>
    public static bool TryExecute(this Action action, out Exception? error)
    {
        try { action(); error = null; return true; }
        catch (Exception ex) { error = ex; return false; }
    }

    /// <summary>
    /// <see cref="Func{T}"/>를 예외 없이 실행합니다.<br/>
    /// 실패 시 <c>default(T)</c>와 <paramref name="error"/>를 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// Func<string> readPort = () => registry.Read("port");
    /// string? port = readPort.TryExecute<string>(out var err);
    /// string  name = port ?? "COM1";
    /// </code>
    /// </example>
    public static T? TryExecute<T>(this Func<T> func, out Exception? error)
    {
        try { error = null; return func(); }
        catch (Exception ex) { error = ex; return default; }
    }

    /// <summary>
    /// 비동기 <see cref="Func{Task}"/>를 예외 없이 실행합니다.<br/>
    /// 결과를 <see cref="UtilResult"/>로 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // lssLib.Serialization 탭7: Binary 파일 저장 — 실패 시 UI 메시지 표시
    /// Func<Task> saveFrame = async () =>
    /// {
    ///     await outputPath.WriteBytesAsync(writer.ToArray(), ct);
    /// };
    /// UtilResult r = await saveFrame.TryExecuteAsync();
    ///
    /// if (r.IsError)
    ///     viewModel.StatusMessage = $"저장 실패: {r.Error!.Message}";
    /// else
    ///     viewModel.StatusMessage = "저장 완료";
    /// </code>
    /// </example>
    public static async Task<UtilResult> TryExecuteAsync(this Func<Task> action)
    {
        try { await action(); return UtilResults.Ok(); }
        catch (Exception ex) { return UtilResults.Fail(ex); }
    }

    /// <summary>
    /// 비동기 <see cref="Func{Task{T}}"/>를 예외 없이 실행합니다.<br/>
    /// 결과를 <see cref="UtilResult{T}"/>로 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 프레임 파싱 — 실패 시 fallback
    /// Func<Task<byte[]>> recv = () => ReadFrameAsync();
    /// UtilResult<byte[]> r = await recv.TryExecuteAsync();
    ///
    /// byte[] frame = r.UnwrapOr(Array.Empty<byte>());
    ///
    /// // Map 체이닝
    /// UtilResult<BufResult>parsed = r.Map(raw =>
    ///     new BufferParser(Guard.NotEmpty(raw)).ParseAll(schema));
    /// </code>
    /// </example>
    public static async Task<UtilResult<T>> TryExecuteAsync<T>(this Func<Task<T>> func)
    {
        try { return UtilResults.Ok(await func()); }
        catch (Exception ex) { return UtilResults.Fail<T>(ex); }
    }

    // ─────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────

    private static string BuildTimeoutMsg(string? name, TimeSpan timeout)
        => $"작업{(name != null ? $" '{name}'" : "")}이 " +
           $"{timeout.TotalMilliseconds:0} ms 내에 완료되지 않았습니다.";
}