namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — CircuitBreakerState
//  회로 차단기 런타임 상태. 스레드 안전 (lock 기반).
//  ▸ sealed concrete class — 추상화 없음
//  ▸ 인스턴스 공유: 동일 서비스에 대한 모든 호출에 같은 인스턴스 사용
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 회로 차단기 런타임 상태.<br/>
/// 서비스 단위로 하나의 인스턴스를 공유하여 사용합니다.
/// </summary>
/// <remarks>
/// <para><b>인스턴스 수명 관리</b></para>
/// <para>
/// <see cref="CircuitBreakerState"/>는 DI 컨테이너 또는 <c>static</c> 필드로 관리하여
/// 동일 서비스의 모든 호출에서 공유해야 상태 누적이 올바르게 동작합니다.
/// </para>
/// <code>
/// // WPF App.xaml.cs — static 필드로 관리
/// public static readonly CircuitBreakerState SensorBreaker =
///     new(CircuitBreakerPolicy.Default);
///
/// // ViewModel에 주입하여 사용
/// _breaker = App.SensorBreaker;
///
/// // 상태 바인딩
/// public string CircuitStatus => _breaker.Current.ToString();
/// </code>
/// <para><b>주의: 호출마다 new 하면 안 됩니다.</b></para>
/// <code>
/// // ✗ 잘못된 사용 — 매 호출마다 새 인스턴스 → 상태 공유 안 됨
/// var result = await func.ExecuteAsync(new CircuitBreakerState(), ct);
///
/// // ✓ 올바른 사용 — 공유 인스턴스 주입
/// var result = await func.ExecuteAsync(_sharedBreaker, ct);
/// </code>
/// </remarks>
public sealed class CircuitBreakerState
{
    private readonly CircuitBreakerPolicy _policy;
    private readonly object _lock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _failures;
    private int _halfOpenOk;
    private DateTime _openedAt;

    // ─────────────────────────────────────────────
    // §1  공개 프로퍼티
    // ─────────────────────────────────────────────

    /// <summary>현재 회로 상태. 스레드 안전.</summary>
    public CircuitState Current { get { lock (_lock) return _state; } }

    /// <summary>누적 연속 실패 횟수. 성공 또는 Closed 복귀 시 리셋됩니다.</summary>
    public int FailureCount { get { lock (_lock) return _failures; } }

    /// <summary>회로가 열린 시각. Open 상태가 아니면 <see cref="DateTime.MinValue"/>.</summary>
    public DateTime OpenedAt { get { lock (_lock) return _openedAt; } }

    /// <summary>
    /// Open 상태의 잔여 시간.<br/>
    /// Closed 또는 HalfOpen이면 <see cref="TimeSpan.Zero"/>를 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // UI에 잔여 차단 시간 표시
    /// TimeSpan remaining = breaker.RemainingOpenDuration;
    /// label.Content = remaining &gt; TimeSpan.Zero
    ///     ? $"{remaining.TotalSeconds:F0}초 후 재시도 가능"
    ///     : "정상";
    /// </code>
    /// </example>
    public TimeSpan RemainingOpenDuration
    {
        get
        {
            lock (_lock)
            {
                if (_state != CircuitState.Open) return TimeSpan.Zero;
                var remaining = _policy.EffectiveOpenDuration - (DateTime.UtcNow - _openedAt);
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }

    // ─────────────────────────────────────────────
    // §2  생성자
    // ─────────────────────────────────────────────

    /// <summary>
    /// 지정 정책으로 회로 차단기 상태를 초기화합니다.
    /// </summary>
    /// <param name="policy">적용할 회로 차단기 정책.</param>
    /// <example>
    /// <code>
    /// var breaker = new CircuitBreakerState(
    ///     new CircuitBreakerPolicy(
    ///         FailureThreshold: 5,
    ///         OpenDuration:     TimeSpan.FromSeconds(30),
    ///         OnStateChanged:   (prev, next) => logger.Warn($"[CB] {prev} → {next}")
    ///     )
    /// );
    /// </code>
    /// </example>
    public CircuitBreakerState(CircuitBreakerPolicy policy)
        => _policy = policy;

    /// <summary>
    /// <see cref="CircuitBreakerPolicy.Default"/> 정책으로 초기화합니다.
    /// </summary>
    public CircuitBreakerState()
        : this(CircuitBreakerPolicy.Default) { }

    // ─────────────────────────────────────────────
    // §3  수동 제어
    // ─────────────────────────────────────────────

    /// <summary>
    /// 회로를 강제로 <see cref="CircuitState.Closed"/> 상태로 리셋합니다.<br/>
    /// 장애 복구 확인 후 수동으로 재개할 때 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 장애 복구 후 수동 재개
    /// await VerifyServiceHealthAsync();
    /// breaker.Reset();
    /// logger.Info("회로 차단기 수동 복구 완료");
    /// </code>
    /// </example>
    public void Reset()
    {
        lock (_lock)
        {
            _failures = 0;
            _halfOpenOk = 0;
            Transition(CircuitState.Closed);
        }
    }

    /// <summary>
    /// 회로를 강제로 <see cref="CircuitState.Open"/> 상태로 전환합니다.<br/>
    /// 예방적 차단 (점검, 배포 등)에 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 점검 전 예방적 차단
    /// breaker.Trip();
    /// logger.Info("점검을 위해 회로 차단기 강제 Open");
    ///
    /// await DoMaintenanceAsync();
    ///
    /// breaker.Reset();
    /// logger.Info("점검 완료 — 회로 복구");
    /// </code>
    /// </example>
    public void Trip()
    {
        lock (_lock)
        {
            _openedAt = DateTime.UtcNow;
            Transition(CircuitState.Open);
        }
    }

    // ─────────────────────────────────────────────
    // 내부 메서드 (CircuitBreakerExtensions 전용)
    // ─────────────────────────────────────────────

    internal bool TryEnter()
    {
        lock (_lock)
        {
            switch (_state)
            {
                case CircuitState.Closed:
                    return true;

                case CircuitState.Open:
                    if (DateTime.UtcNow - _openedAt >= _policy.EffectiveOpenDuration)
                    {
                        _halfOpenOk = 0;
                        Transition(CircuitState.HalfOpen);
                        return true;
                    }
                    return false;

                case CircuitState.HalfOpen:
                    return true;

                default:
                    return false;
            }
        }
    }

    internal void OnSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _halfOpenOk++;
                if (_halfOpenOk >= _policy.HalfOpenSuccessThreshold)
                {
                    _failures = 0;
                    Transition(CircuitState.Closed);
                }
                return;
            }

            if (_state == CircuitState.Closed)
            {
                // 성공 시 연속 실패 카운터 리셋 — 다음 실패부터 새로 카운팅
                _failures = 0;
            }
        }
    }

    internal void OnFailure(Exception ex)
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _halfOpenOk = 0;
                _openedAt = DateTime.UtcNow;
                Transition(CircuitState.Open);
                return;
            }

            if (_state == CircuitState.Closed)
            {
                _failures++;
                if (_failures >= _policy.FailureThreshold)
                {
                    _openedAt = DateTime.UtcNow;
                    Transition(CircuitState.Open);
                }
            }
        }
    }

    private void Transition(CircuitState next)
    {
        var prev = _state;
        _state = next;
        if (prev != next)
            _policy.OnStateChanged?.Invoke(prev, next);
    }
}