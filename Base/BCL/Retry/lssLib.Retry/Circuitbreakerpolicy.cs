namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — CircuitBreakerPolicy  +  CircuitState
//  회로 차단기 설정 값 객체 (readonly record struct)
//    - (장애가 발생한 서비스에 계속 요청을 보내서 시스템 전체가 마비되는 것을 방지하는 안전 장치)
//  ▸ No abstractions  ▸ Immutable value type
//
//  상태 전이:
//    Closed  ──(연속 실패 ≥ FailureThreshold)──►  Open
//    Open    ──(OpenDuration 경과)──────────────►  HalfOpen
//    HalfOpen──(성공 ≥ HalfOpenSuccessThreshold)─►  Closed
//    HalfOpen──(실패)──────────────────────────────►  Open
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 회로 차단기 상태를 나타내는 열거형.
/// </summary>
public enum CircuitState
{
    /// <summary>정상 동작. 실패 횟수를 누적 중입니다.</summary>
    Closed,

    /// <summary>회로 열림. 모든 요청을 즉시 거부합니다.</summary>
    Open,

    /// <summary>복구 테스트 중. 제한된 요청만 허용합니다.</summary>
    HalfOpen
}

/// <summary>
/// 회로 차단기 동작 설정 불변 값 객체.<br/>
/// <see cref="CircuitBreakerState"/> 생성 시 전달합니다.
/// </summary>
/// <param name="FailureThreshold">Open 전환까지 허용할 연속 실패 횟수. 기본값: <c>5</c>.</param>
/// <param name="OpenDuration">Open 상태 유지 시간. <c>null</c>이면 30초 적용.</param>
/// <param name="HalfOpenSuccessThreshold">Closed 전환까지 필요한 HalfOpen 성공 횟수. 기본값: <c>1</c>.</param>
/// <param name="OnStateChanged">상태 전이 콜백 <c>(이전상태, 새상태)</c>. 선택.</param>
/// <remarks>
/// <para><b>상태 전이</b></para>
/// <code>
/// Closed  ─(실패 ≥ FailureThreshold)──► Open
/// Open    ─(OpenDuration 경과)──────────► HalfOpen
/// HalfOpen─(성공 ≥ HalfOpenSuccess)─────► Closed
/// HalfOpen─(실패)──────────────────────── Open (즉시 재차단)
/// </code>
/// <para><b>공유 인스턴스 운용</b></para>
/// <code>
/// // DI 컨테이너 또는 static 필드로 관리
/// static readonly CircuitBreakerState SensorBreaker = new(
///     new CircuitBreakerPolicy(
///         FailureThreshold: 5,
///         OpenDuration:     TimeSpan.FromSeconds(30),
///         OnStateChanged:   (prev, next) =>
///         {
///             logger.Warn($"[CB] {prev} → {next}");
///             if (next == CircuitState.Open)
///                 alertService.Send("센서 회로 차단 — 30초 후 복구 시도");
///         }
///     )
/// );
/// </code>
/// </remarks>
public readonly record struct CircuitBreakerPolicy(
    int FailureThreshold = 5,
    TimeSpan? OpenDuration = null,
    int HalfOpenSuccessThreshold = 1,
    Action<CircuitState, CircuitState>? OnStateChanged = null)
{
    // ─────────────────────────────────────────────
    // §1  사전 정의 프리셋
    // ─────────────────────────────────────────────

    /// <summary>
    /// 기본: 5회 실패 → 30 s Open.<br/>
    /// 일반 서비스에 적합합니다.
    /// </summary>
    public static readonly CircuitBreakerPolicy Default =
        new(FailureThreshold: 5, OpenDuration: TimeSpan.FromSeconds(30));

    /// <summary>
    /// 엄격: 3회 실패 → 1 min Open. HalfOpen에서 2회 성공해야 Closed.<br/>
    /// 중요 리소스(DB, 결제 등)에 적합합니다.
    /// </summary>
    public static readonly CircuitBreakerPolicy Strict =
        new(FailureThreshold: 3, OpenDuration: TimeSpan.FromMinutes(1),
            HalfOpenSuccessThreshold: 2);

    /// <summary>
    /// 관대: 10회 실패 → 10 s Open. 빠른 복구.<br/>
    /// 오류 허용도가 높은 비중요 서비스에 적합합니다.
    /// </summary>
    public static readonly CircuitBreakerPolicy Lenient =
        new(FailureThreshold: 10, OpenDuration: TimeSpan.FromSeconds(10));

    internal TimeSpan EffectiveOpenDuration =>
        OpenDuration ?? TimeSpan.FromSeconds(30);
}