
namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — RetryPolicy
//  Retry 설정 캡슐화 값 객체 (readonly record struct)
//  ▸ 파라미터 sprawl 해소 — 설정 한 번 정의 후 여러 호출에 재사용
//  ▸ No abstractions  ▸ Immutable value type
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Retry 동작 설정 불변 값 객체.<br/>
/// <see cref="RetryExtensions"/>의 모든 오버로드에 직접 전달하여 사용합니다.
/// </summary>
/// <param name="MaxAttempts">최대 시도 횟수. 기본값: <c>3</c>.</param>
/// <param name="Delay">재시도 기본 대기 시간. <c>null</c>이면 200 ms 적용.</param>
/// <param name="Backoff">
/// <c>true</c>: 지수 백오프 적용 (<c>Delay × 2^attempt</c>).<br/>
/// <c>false</c>: 고정 간격. 기본값: <c>false</c>.
/// </param>
/// <param name="OnRetry">재시도 직전 콜백 <c>(exception, attemptNumber)</c>. 선택.</param>
/// <remarks>
/// <para><b>지수 백오프 계산</b></para>
/// <para>
/// <c>Backoff=true</c>일 때 대기 시간 = <c>Delay × 2^attempt</c><br/>
/// Http 정책(Delay=500ms) 기준:<br/>
/// - 1회 실패 후: 500 ms<br/>
/// - 2회 실패 후: 1,000 ms<br/>
/// - 3회 실패 후: 2,000 ms
/// </para>
/// <para><b>정책 재사용 패턴</b></para>
/// <code>
/// // 공유 정책 — 여러 호출에 재사용
/// static readonly RetryPolicy IotPolicy = new(
///     MaxAttempts: 10,
///     Delay:       TimeSpan.FromMilliseconds(100),
///     Backoff:     false,
///     OnRetry:     (ex, n) => logger.Warn($"[재시도 {n}/10] {ex.Message}")
/// );
///
/// await connectFunc.RetryAsync(IotPolicy, ct);
/// await readFunc.RetryAsync(IotPolicy, ct);
///
/// // with 식으로 파생 정책 생성
/// var verbosePolicy = RetryPolicy.Http with
/// {
///     OnRetry = (ex, n) => diagnostics.RecordRetry(n, ex)
/// };
/// </code>
/// </remarks>
public readonly record struct RetryPolicy(
    int MaxAttempts = 3,
    TimeSpan? Delay = null,
    bool Backoff = false,
    Action<Exception, int>? OnRetry = null)
{
    // ─────────────────────────────────────────────
    // §1  사전 정의 프리셋
    // ─────────────────────────────────────────────

    /// <summary>
    /// 기본 정책: 3회 · 200 ms 고정 대기.<br/>
    /// 일반적인 일시 오류에 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// action.Retry(RetryPolicy.Default);
    /// await func.RetryAsync(RetryPolicy.Default, ct);
    /// </code>
    /// </example>
    public static readonly RetryPolicy Default = new(3, TimeSpan.FromMilliseconds(200));

    /// <summary>
    /// HTTP/네트워크 정책: 3회 · 500 ms · 지수 백오프.<br/>
    /// REST API, 외부 서비스 호출에 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 500ms → 1,000ms → 2,000ms
    /// await callApi.RetryAsync(RetryPolicy.Http, ct);
    /// </code>
    /// </example>
    public static readonly RetryPolicy Http = new(3, TimeSpan.FromMilliseconds(500), Backoff: true);

    /// <summary>
    /// DB 연결 정책: 5회 · 1 s · 지수 백오프.<br/>
    /// DB 연결 실패, 쿼리 타임아웃에 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 1s → 2s → 4s → 8s → 16s
    /// await dbFunc.RetryOnAsync<DataRow, SqlException>(RetryPolicy.Database, ct);
    /// </code>
    /// </example>
    public static readonly RetryPolicy Database = new(5, TimeSpan.FromSeconds(1), Backoff: true);

    /// <summary>
    /// 즉시 재시도: 3회 · 대기 없음.<br/>
    /// 경쟁 조건처럼 즉시 재시도가 유효한 경우에 사용합니다.
    /// </summary>
    public static readonly RetryPolicy Immediate = new(3, TimeSpan.Zero);

    // ─────────────────────────────────────────────
    // 내부 헬퍼 (RetryExtensions 전용)
    // ─────────────────────────────────────────────

    internal TimeSpan GetDelay(int attempt)
    {
        var baseMs = (Delay ?? TimeSpan.FromMilliseconds(200)).TotalMilliseconds;
        return Backoff
            ? TimeSpan.FromMilliseconds(baseMs * Math.Pow(2, attempt))
            : TimeSpan.FromMilliseconds(baseMs);
    }
}