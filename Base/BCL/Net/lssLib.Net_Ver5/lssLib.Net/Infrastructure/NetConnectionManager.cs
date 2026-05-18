// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetConnectionManager.cs
//  역할: 연결·재접속·상태 머신 전담 (NetChannelBase 에서 분리)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 연결·재접속·상태 머신 전담 클래스.
/// </summary>
/// <remarks>
/// <para>
/// <b>SemaphoreSlim(1,1) 재접속 중복 방지:</b><br/>
/// 첫 번째 숫자(Initial Count=1): 처음에 바로 입장 가능한 스레드 수.<br/>
/// 두 번째 숫자(Maximum Count=1): 최대 허용 스레드 수.<br/>
/// 일반 <c>lock</c> 키워드는 내부에서 <c>await</c> 불가 → 비동기 통신에는 SemaphoreSlim 사용.
/// </para>
/// <para>
/// 재접속 성공 시 <see cref="Reconnected"/> 이벤트 → <see cref="NetScheduler.Resume"/> 자동 호출.
/// </para>
/// </remarks>
internal sealed class NetConnectionManager : IAsyncDisposable
{
    #region §1 ─ 필드
    /// <summary>
    /// 전송 계층 참조. 연결·재접속·상태 관리 전담.
    /// * NetChannelBase 에서 직접 통신하지 않고 이 클래스를 통해 간접적으로 통신 → 책임 분리 + 재접속 로직 집중 관리.
    /// </summary>
    private readonly INetTransport _transport;

    /// <summary>
    /// 장비 설정 참조. 재접속 시도 횟수·대기 시간 계산, 이벤트 메시지 등에 사용.
    /// </summary>
    private readonly NetDeviceConfig _cfg;

    /// <summary>
    /// 통계 참조. 오류 발생 시 기록, 재접속 성공 시 기록 → NetDeviceRegistry 통계 집계에 활용.
    /// </summary>
    private readonly NetStatistics _stats;

    /// <summary>SemaphoreSlim(1,1) — 재접속 중복 실행 방지.</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// 재접속 상태 플래그. HandleErrorAsync 진입 시 true → 중복 진입 방지.
    /// </summary>
    private volatile bool _disposed;

    /// <summary>
    /// 재접속 시도 중인지 여부. UI/로그에서 재접속 진행 상황 표시 등에 활용.
    /// </summary>
    private volatile bool _reconnecting;

    #endregion

    #region §2 ─ 생성자

    internal NetConnectionManager(
        INetTransport transport,
        NetDeviceConfig cfg,
        NetStatistics stats)
    {
        _transport = transport;
        _cfg = cfg;
        _stats = stats;
    }

    #endregion

    #region §3 ─ 프로퍼티 / 이벤트

    /// <summary>
    /// 현재 연결 상태. NetState 열거형으로 표현 (Disconnected, Connecting, Connected, Reconnecting 등).
    /// </summary>
    public NetState State => _transport.State;

    /// <summary>
    /// 연결 여부. State가 Connected인 경우 true. 재접속 시도 중에도 일시적으로 false → IsReconnecting 플래그로 구분.
    /// </summary>
    public bool IsConnected => _transport.State == NetState.Connected;

    /// <summary>
    /// 재접속 시도 중 여부. 
    /// HandleErrorAsync 진입 시 true → UI/로그에서 재접속 진행 상황 표시 등에 활용. 
    /// 재접속 시도 중에도 State는 일시적으로 Disconnected → IsConnected와 구분하여 사용.
    /// </summary>
    public bool IsReconnecting => _reconnecting;

    /// <summary>재접속 성공 시 발생. → NetScheduler.Resume() 트리거.</summary>
    public event Action? Reconnected;

    /// <summary>오류 발생 시 발생 (재접속 시도 전).</summary>
    public event Action<int, Exception>? ErrorOccurred;

    /// <summary>
    /// 재접속 시도 진행 알림. (시도번호, 최대횟수문자열, 다음대기초)
    /// <para>DeviceErrorOccurred 이벤트로 전달되어 UI/로그에 표시됩니다.</para>
    /// </summary>
    public event Action<int, string, double>? ReconnectProgress;

    #endregion

    #region §4 ─ 공개 메서드

    /// <summary>초기 접속 (StartAsync 에서 호출).</summary>
    internal async Task ConnectAsync(CancellationToken ct)
    {
        bool doRetry = _cfg.IsRetryEnabled && _cfg.RetryTarget.HasFlag(RetryTarget.Connect);
        int max = doRetry
            ? (_cfg.MaxRetries == 0 ? int.MaxValue : _cfg.MaxRetries)
            : 1;
        await ConnectLoopAsync(max, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 통신 오류 발생 시 호출 → 연결 끊기 + 재접속 루프.
    /// </summary>
    /// <remarks>
    /// <para>SemaphoreSlim(1,1).WaitAsync(0): 즉시 시도 → 이미 재접속 진행 중이면 즉시 return.</para>
    /// <para>
    /// <b>처리 순서:</b>
    /// <list type="number">
    ///   <item><description>_stats.RecordError → 오류 통계 기록</description></item>
    ///   <item><description>ErrorOccurred 이벤트 → DeviceErrorOccurred 전파</description></item>
    ///   <item><description>_transport.DisconnectAsync → 기존 연결 정리</description></item>
    ///   <item><description>ReconnectLoopAsync → 재접속 시도 (지수 백오프)</description></item>
    ///   <item><description>성공: RecordReconnect + Reconnected 이벤트 → Scheduler.Resume</description></item>
    ///   <item><description>retryPayload 있으면: Critical 우선순위로 재투입</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal async Task HandleErrorAsync(
        Exception ex,
        byte[]? retryPayload,
        Func<byte[], CancellationToken, Task>? onReconnected,
        CancellationToken ct)
    {
        // 이미 재접속 진행 중이면 중복 무시 (0=즉시 시도, 대기 없음)
        if (!await _lock.WaitAsync(0).ConfigureAwait(false)) return;

        try
        {
            _reconnecting = true;
            _stats.RecordError(ex.Message);
            ErrorOccurred?.Invoke(_cfg.DeviceId, ex);

            try
            {
                // 기존 연결 끊기 (이미 끊긴 상태여도 예외 없음)
                await _transport.DisconnectAsync().ConfigureAwait(false);
            }
            catch { /* Disconnect 중 오류 무시 */ }

            if (ct.IsCancellationRequested) return;

            bool success = await ReconnectLoopAsync(ct).ConfigureAwait(false);
            if (!success) return;

            _stats.RecordReconnect();
            Reconnected?.Invoke();

            // 끊기 전 보존된 Write 페이로드가 있으면 재접속 후 Critical 로 재투입
            if (retryPayload is not null && onReconnected is not null && IsConnected)
                await onReconnected(retryPayload, ct).ConfigureAwait(false);
        }
        finally
        {
            _reconnecting = false;
            _lock.Release();
        }
    }

    #endregion

    #region §5 ─ 내부 루프

    /// <summary>
    /// 초기 접속 시도 루프.
    /// maxAttempts 횟수만큼 시도하고 마지막 시도에서 예외 전파.
    /// </summary>
    private async Task ConnectLoopAsync(int maxAttempts, CancellationToken ct)
    {
        for (int n = 1; n <= maxAttempts; n++)
        {
            try
            {
                await _transport.ConnectAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch when (n < maxAttempts)
            {
                // 마지막 시도가 아니면 대기 후 재시도
                await Task.Delay(CalcDelay(n), ct).ConfigureAwait(false);
            }
        }
        // 마지막 시도 — 실패 시 예외 그대로 전파
        await _transport.ConnectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 재접속 루프.
    /// <para>
    /// <b>MaxRetries=0 → 무제한:</b> 서버가 살아날 때까지 지수 백오프로 계속 시도.
    /// </para>
    /// <para>
    /// <b>백오프 계산:</b><br/>
    /// ReconnectBackoff=true  → RetryDelay × 2^(n-1), 최대 60s<br/>
    /// ReconnectBackoff=false → RetryDelay 고정
    /// </para>
    /// <para>
    /// <b>진행 상황:</b> ReconnectProgress 이벤트 → NetChannelBase → DeviceErrorOccurred 이벤트
    /// </para>
    /// </summary>
    private async Task<bool> ReconnectLoopAsync(CancellationToken ct)
    {
        bool doRetry = _cfg.IsRetryEnabled && _cfg.RetryTarget.HasFlag(RetryTarget.Connect);
        int max = doRetry
            ? (_cfg.MaxRetries == 0 ? int.MaxValue : _cfg.MaxRetries)
            : 1;
        string strMaxLabel = max == int.MaxValue ? "∞" : max.ToString();

        for (int n = 1; n <= max; n++)
        {
            if (ct.IsCancellationRequested) return false;

            if (n > 1)
            {
                var delay = CalcDelay(n);
                ReconnectProgress?.Invoke(n, strMaxLabel, delay.TotalSeconds);
                try { await Task.Delay(delay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return false; }
            }
            else
            {
                // 첫 번째 시도는 즉시 (대기 없음)
                ReconnectProgress?.Invoke(1, strMaxLabel, 0);
            }

            if (ct.IsCancellationRequested) return false;

            try
            {
                await _transport.ConnectAsync(ct).ConfigureAwait(false);
                return true;  // 재접속 성공
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                // 한도 초과 시 마지막 오류 이벤트
                if (n >= max && max != int.MaxValue)
                    ErrorOccurred?.Invoke(_cfg.DeviceId,
                        new InvalidOperationException(
                            $"[{_cfg.DeviceName}] 재접속 {max}회 실패: {ex.Message}"));
            }
        }
        return false;
    }

    /// <summary>
    /// 재시도 대기 시간 계산.
    /// <para>ReconnectBackoff=true: RetryDelay × 2^(attempt-1), 최대 60s.</para>
    /// <para>ReconnectBackoff=false: RetryDelay 고정.</para>
    /// </summary>
    private TimeSpan CalcDelay(int attempt) => _cfg.ReconnectBackoff
        ? TimeSpan.FromTicks(Math.Min(
            _cfg.RetryDelay.Ticks * (long)Math.Pow(2, attempt - 1),
            TimeSpan.FromSeconds(60).Ticks))
        : _cfg.RetryDelay;

    #endregion

    #region §6 ─ IAsyncDisposable

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _lock.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    #endregion
}