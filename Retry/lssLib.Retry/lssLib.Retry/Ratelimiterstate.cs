namespace lssLib.Retry;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry — RateLimiterState
//  속도 제한 런타임 상태 (슬라이딩 윈도우 알고리즘)
//  - 서버나 API가 감당할 수 있는 수준까지만 요청을 허용하는 안전장치
//  ▸ sealed concrete class — 추상화 없음
//  ▸ 스레드 안전: lock 기반 큐 조작
//  ▸ 인스턴스 공유: 동일 리소스에 대한 모든 호출에 같은 인스턴스 사용
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 속도 제한 런타임 상태 (슬라이딩 윈도우).<br/>
/// 리소스 단위로 하나의 인스턴스를 공유하여 사용합니다.
/// </summary>
/// <remarks>
/// <para><b>인스턴스 수명 관리</b></para>
/// <para>
/// <see cref="RateLimiterState"/>는 DI 컨테이너 또는 <c>static</c> 필드로 관리하여
/// 동일 리소스의 모든 호출에서 공유해야 슬롯 카운팅이 올바르게 동작합니다.
/// </para>
/// <code>
/// // WPF App.xaml.cs — static 필드로 관리
/// public static readonly RateLimiterState FrameLimiter =
///     new(RateLimiterPolicy.PerSecond(30));
///
/// // 상태 바인딩
/// public int AvailableSlots => App.FrameLimiter.Available;
/// </code>
/// <para><b>주의: 호출마다 new 하면 안 됩니다.</b></para>
/// <code>
/// // ✗ 잘못된 사용 — 매 호출마다 새 인스턴스 → 제한 동작 안 함
/// await func.ExecuteAsync(new RateLimiterState(policy), ct);
///
/// // ✓ 올바른 사용 — 공유 인스턴스 주입
/// await func.ExecuteAsync(_sharedLimiter, ct);
/// </code>
/// </remarks>
public sealed class RateLimiterState
{
    private readonly RateLimiterPolicy _policy;
    private readonly Queue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    // ─────────────────────────────────────────────
    // §1  공개 프로퍼티
    // ─────────────────────────────────────────────

    /// <summary>설정된 속도 제한 정책.</summary>
    public RateLimiterPolicy Policy => _policy;

    /// <summary>
    /// 현재 윈도우 내 남은 허용 횟수.<br/>
    /// 실시간 모니터링용으로 사용합니다. 경쟁 조건으로 정확하지 않을 수 있습니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // WPF 상태 표시
    /// statusLabel.Content = $"남은 슬롯: {limiter.Available}/{limiter.Policy.MaxRequests}";
    /// </code>
    /// </example>
    public int Available
    {
        get
        {
            lock (_lock)
            {
                Purge();
                return Math.Max(0, _policy.MaxRequests - _timestamps.Count);
            }
        }
    }

    /// <summary>현재 윈도우 내 사용된 요청 수.</summary>
    public int Used
    {
        get
        {
            lock (_lock)
            {
                Purge();
                return _timestamps.Count;
            }
        }
    }

    /// <summary>
    /// 다음 슬롯이 열리는 예상 시각.<br/>
    /// 여유가 있으면 <see cref="DateTime.UtcNow"/>를 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 다음 가능 시각까지 대기
    /// TimeSpan wait = limiter.NextAvailableAt - DateTime.UtcNow;
    /// if (wait &gt; TimeSpan.Zero)
    ///     await Task.Delay(wait, ct);
    /// </code>
    /// </example>
    public DateTime NextAvailableAt
    {
        get
        {
            lock (_lock)
            {
                Purge();
                if (_timestamps.Count < _policy.MaxRequests)
                    return DateTime.UtcNow;
                return _timestamps.Peek() + _policy.Window;
            }
        }
    }

    // ─────────────────────────────────────────────
    // §2  생성자
    // ─────────────────────────────────────────────

    /// <summary>
    /// 지정 정책으로 속도 제한 상태를 초기화합니다.
    /// </summary>
    /// <param name="policy">적용할 속도 제한 정책.</param>
    /// <example>
    /// <code>
    /// // 초당 30프레임 처리 제한
    /// var limiter = new RateLimiterState(RateLimiterPolicy.PerSecond(30));
    ///
    /// // 분당 60회 API 제한
    /// var apiLimiter = new RateLimiterState(RateLimiterPolicy.ApiDefault);
    /// </code>
    /// </example>
    public RateLimiterState(RateLimiterPolicy policy)
        => _policy = policy;

    // ─────────────────────────────────────────────
    // 내부 메서드 (RateLimiterExtensions 전용)
    // ─────────────────────────────────────────────

    internal bool TryAcquire()
    {
        lock (_lock)
        {
            Purge();
            if (_timestamps.Count >= _policy.MaxRequests)
                return false;
            _timestamps.Enqueue(DateTime.UtcNow);
            return true;
        }
    }

    private void Purge()
    {
        var cutoff = DateTime.UtcNow - _policy.Window;
        while (_timestamps.Count > 0 && _timestamps.Peek() <= cutoff)
            _timestamps.Dequeue();
    }
}