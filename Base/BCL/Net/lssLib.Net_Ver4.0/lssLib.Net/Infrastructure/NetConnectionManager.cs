// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetConnectionManager.cs
//  역할: 연결·재접속·상태 머신 전담 (NetChannelBase 에서 분리)
// ══════════════════════════════════════════════════════════════════════

//using lssLib.Log;

namespace lssLib.Net;

/// <summary>
/// 연결·재접속·상태 머신 전담 클래스.
/// </summary>
/// <remarks>
/// <para><see cref="SemaphoreSlim"/>(1,1) 으로 재접속 중복 실행을 방지합니다.
/// 첫 번째 숫자 1 (Initial Count): 처음에 입장 가능한 스레드 수(현재 1명이 바로 들어갈 수 있음)
/// 두 번째 숫자 1 (Maximum Count): 최대 입장 가능한 스레드 수입니다. (아무리 자리가 나도 1명까지만 허용함)
/// 왜 통신 관리자에서 쓰는가?  (lock과의 차이)
///     일반적인 lock 키워드는 내부에서 await를 사용할 수 없음
///     하지만 통신 로직은 대부분 비동기 이기 때문에 사용
/// </para>
/// <para>재접속 성공 시 <see cref="Reconnected"/> 이벤트 → <see cref="NetScheduler.Resume"/> 자동 호출.</para>
/// </remarks>
internal sealed class NetConnectionManager : IAsyncDisposable
{
    #region §1 ─ 필드

    private readonly INetTransport _transport;
    private readonly NetDeviceConfigBase _cfg;
    private readonly NetStatistics _stats;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile bool _disposed;
    private volatile bool _reconnecting;

    #endregion

    #region §2 ─ 생성자

    internal NetConnectionManager(INetTransport transport,
        NetDeviceConfigBase cfg, NetStatistics stats)
    {
        _transport = transport;
        _cfg = cfg;
        _stats = stats;
    }

    #endregion

    #region §3 ─ 프로퍼티 / 이벤트

    public NetState State => _transport.State;
    public bool IsConnected => _transport.State == NetState.Connected;
    public bool IsReconnecting => _reconnecting;

    /// <summary>재접속 성공 시 발생. NetScheduler.Resume() 트리거.</summary>
    public event Action? Reconnected;

    /// <summary>오류 발생 시 발생 (재접속 시도 전).</summary>
    public event Action<int, Exception>? ErrorOccurred;

    /// <summary>
    /// 재접속 시도 진행 알림. (시도번호, 최대횟수문자열, 다음대기초)
    /// DeviceErrorOccurred 이벤트로 전달되어 UI/로그에 표시됩니다.
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
    /// <para>SemaphoreSlim(1,1) 으로 중복 실행 방지 — 이미 재접속 중이면 즉시 return.</para>
    /// </summary>
    internal async Task HandleErrorAsync(Exception ex,
                                            byte[]? retryPayload,
                                            Func<byte[], CancellationToken, Task>? onReconnected,
                                            CancellationToken ct)
    {
        if (!await _lock.WaitAsync(0).ConfigureAwait(false)) // 중복 진행 금지 ( 즉시 실행 )
        {
            //  Log(LogLevel.Debug, "재접속 이미 진행 중 — 중복 무시");
            return;
        }
        try
        {
            _stats.RecordError(ex.Message);
            ErrorOccurred?.Invoke(_cfg.DeviceId, ex);
            // Log(LogLevel.Warn, $"오류 → 재접속 준비: {ex.Message}");


            try
            {
                // 기존 연결 끊기 (이미 끊긴 상태여도 예외 없음)
                await _transport.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception dex)
            {
                dex.Message.ToString(); // Log 무한 루프 방지 위해 메시지만 참조
                //Log(LogLevel.Warn, $"Disconnect 오류(무시): {dex.Message}");
            }

            if (ct.IsCancellationRequested) return;

            // 재접속 시도
            bool success = await ReconnectLoopAsync(ct).ConfigureAwait(false);
            if (success)
            {
                _stats.RecordReconnect();
                Reconnected?.Invoke();

                if (retryPayload is not null && // 끊기 전 재 전송 대기 중이던 페이로드가 있고,
                    onReconnected is not null &&  // 재접속 후 재 전송 콜백이 있고,
                    IsConnected) // 재접속이 성공적으로 이루어진 경우
                {
                    //Log(LogLevel.Info, "재접속 후 Write 재전송 (Critical)");

                    // Critical 로 재투입 (예: Write -> 실패 → 재접속( 연결 Off-> on ) → Write 재투입 )
                    await onReconnected(retryPayload, ct).ConfigureAwait(false);
                }
            }
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
    /// </summary>
    /// <param name="maxAttempts"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task ConnectLoopAsync(int maxAttempts, CancellationToken ct)
    {
        for (int n = 1; n <= maxAttempts; n++)
        {
            try
            {
                // 첫 번째 시도는 즉시, 이후 시도는 지수 백오프 또는 고정 간격으로 대기
                await _transport.ConnectAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (n < maxAttempts)
            {
                var delay = CalcDelay(n);

                ex.Message.ToString(); // Log 무한 루프 방지 위해 메시지만 참조
                //Log(LogLevel.Warn,
                //    $"초기 접속 실패 ({n}/{maxAttempts}) — {delay.TotalSeconds:F1}s 후 재시도: {ex.Message}");

                // 재접속 대기 (지수 백오프 또는 고정 간격)
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        // 마지막 시도 (재접속 한도 초과 시 예외 throw)
        // 최종 에러 보고를 위한 조치
        await _transport.ConnectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 재접속 루프.
    ///
    /// <b>MaxRetries=0 → 무제한:</b> 서버가 살아날 때까지 지수 백오프로 계속 시도합니다.
    ///
    /// <b>백오프 계산:</b>
    ///   ReconnectBackoff=true  → RetryDelay × 2^(n-1), 최대 60s
    ///   ReconnectBackoff=false → RetryDelay 고정
    ///
    /// <b>진행 상황:</b> ReconnectProgress 이벤트 → NetChannelBase → DeviceErrorOccurred 이벤트
    /// </summary>
    private async Task<bool> ReconnectLoopAsync(CancellationToken ct)
    {
        // 재접속 활성화 여부 및 ConnectTarget 포함 여부 확인 → 최대 재시도 횟수 결정
        bool doRetry = _cfg.IsRetryEnabled && _cfg.RetryTarget.HasFlag(RetryTarget.Connect);
        int max = doRetry ?
                (_cfg.MaxRetries == 0 ? int.MaxValue : _cfg.MaxRetries) :
                1;

        string maxStr = max == int.MaxValue ? "∞" : max.ToString();

        //Log(LogLevel.Warn, $"재접속 시작 (최대 {(max == int.MaxValue ? "무제한" : max)}회)");

        for (int n = 1; n <= max; n++)
        {
            // 재접속 시도 전 취소 요청 확인
            if (ct.IsCancellationRequested) return false;

            // n=1: 즉시 시도, n>1: 백오프 대기 후 시도
            if (n > 1)
            {
                var delay = CalcDelay(n);

                ReconnectProgress?.Invoke(n, maxStr, delay.TotalSeconds);
                //Log(LogLevel.Warn, $"재접속 대기 {delay.TotalSeconds:F1}s ({n}/{max})");
                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
            else
            {
                ReconnectProgress?.Invoke(1, maxStr, 0);
            }

            if (ct.IsCancellationRequested) return false;

            try
            {
                // 재접속 시도
                await _transport.ConnectAsync(ct).ConfigureAwait(false);
                //Log(LogLevel.Info, $"재접속 성공 ({n}회)");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                // Log(LogLevel.Warn, $"재접속 실패 ({n}/{max}): {ex.Message}");
                // 한도 초과 시 ErrorOccurred
                if (n >= max && max != int.MaxValue)
                {
                    //Log(LogLevel.Fatal, "재접속 한도 초과 — 포기");
                    ErrorOccurred?.Invoke(_cfg.DeviceId,
                                            new InvalidOperationException(
                                            $"[{_cfg.DeviceName}] 재접속 {max}회 실패: {ex.Message}"));
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 재시도 시 대기 시간 계산 (지수 백오프 또는 고정 간격)
    /// </summary>
    private TimeSpan CalcDelay(int attempt) => _cfg.ReconnectBackoff
        ? TimeSpan.FromTicks(Math.Min(
            _cfg.RetryDelay.Ticks * (long)Math.Pow(2, attempt - 1),
            TimeSpan.FromSeconds(60).Ticks))
        : _cfg.RetryDelay;

    /*
    private void Log(LogLevel lv, string msg)
        => LogManager.Instance.AddLog(lv, _cfg.DeviceName, msg);*/

    #endregion

    #region §6 ─ IAsyncDisposable

    /// <summary>
    /// 통신 관리자 리소스 해제.
    /// </summary>
    /// <returns></returns>
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