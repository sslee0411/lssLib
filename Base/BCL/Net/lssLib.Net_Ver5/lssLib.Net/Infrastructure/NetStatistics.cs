// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetStatistics.cs
//  역할: 장비별 통신 통계 수집 (Interlocked 스레드 안전)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 장비 채널의 통신 통계를 수집합니다.
/// </summary>
/// <remarks>
/// <para>
/// <b>수집 항목:</b> 전송·수신·오류 카운터, 평균·최대 응답 시간, 재접속·재전송 횟수, 마지막 오류.
/// </para>
/// <para>
/// <b>스레드 안전:</b> 모든 카운터는 <see cref="Interlocked"/> 를 통해 원자적으로 갱신됩니다.
/// </para>
/// <b>사용 예시:</b>
/// <code>
/// // 실시간 조회
/// var s = channel.Statistics;
/// LogManager.Instance.Info(channel.DeviceName,
///     $"전송={s.TotalSent} 수신={s.TotalReceived} 오류={s.TotalErrors} " +
///     $"평균응답={s.AvgResponseMs:F1}ms");
///
/// // WPF DataGrid 바인딩용 불변 스냅샷
/// var snap = channel.Statistics.Snapshot();
/// DgStats.ItemsSource = new[] { snap };
///
/// // 리셋
/// channel.Statistics.Reset();
/// </code>
/// </remarks>
public sealed class NetStatistics
{
    #region §1 ─ 카운터 (Interlocked 스레드 안전)
    /// <summary>
    /// TotalSent: WriteAsync 성공 횟수
    /// 총 전송 성공 횟수
    /// </summary>
    private long _totalSent;             

    /// <summary>
    /// TotalReceived: DeviceFrameReceived 이벤트 발생 횟수 (수신 프레임 수)
    /// 총 수신 프레임 횟수
    /// </summary>
    private long _totalReceived;

    /// <summary>
    /// TotalErrors: WriteAsync 실패 횟수 + 연결 오류 등 모든 오류 횟수
    /// 총 오류 횟수 (전송 실패 + 연결 오류 등)
    /// </summary>
    private long _totalErrors;

    /// <summary>
    /// TotalReconnects: 재접속 성공 횟수 (NetConnectionManager.HandleErrorAsync 진입 시 기록)
    /// 총 재접속 성공 횟수
    /// </summary>
    private long _totalReconnects;

    /// <summary>
    /// TotalWriteRetries: WriteAsync 재전송 횟수 (NetChannelBase.WriteAsync 실패 시 기록)
    /// 총 Write 재전송 횟수
    /// </summary>
    private long _totalWriteRetries;

    /// <summary>
    /// 평균 응답 시간(ms). RequestAsync 기준.
    /// 총 응답 시간 누적 (ms, RequestAsync 기준)
    /// </summary>
    private long _totalResponseTimeMs;

    /// <summary>
    /// 평균 응답 시간 계산용 카운터. RequestAsync 기준.
    /// 총 응답 횟수 (평균 계산용)
    /// </summary>
    private long _totalResponseCount;

    /// <summary>
    /// 최대 응답 시간(ms). RequestAsync 기준.
    /// </summary>
    private long _maxResponseTimeMs;

    /// <summary>
    /// 마지막 오류 메시지 및 시각. DeviceErrorOccurred 이벤트 발생 시 기록.
    /// </summary>
    private volatile string _lastError = string.Empty;

    /// <summary>
    /// 마지막 오류 발생 시각 (HH:mm:ss.fff). DeviceErrorOccurred 이벤트 발생 시 기록.
    /// </summary>
    private volatile string _lastErrorTime = string.Empty;
    #endregion

    #region §2 ─ 공개 프로퍼티

    /// <summary>
    /// 총 전송 성공 횟수.
    /// <para>Interlocked.Read: 읽는 도중 다른 스레드가 수정하지 못하도록 보장.</para>
    /// </summary>
    public long TotalSent => Interlocked.Read(ref _totalSent);

    /// <summary>총 수신 프레임 횟수.</summary>
    public long TotalReceived => Interlocked.Read(ref _totalReceived);

    /// <summary>총 오류 횟수 (전송 실패 + 연결 오류 등).</summary>
    public long TotalErrors => Interlocked.Read(ref _totalErrors);

    /// <summary>총 재접속 성공 횟수.</summary>
    public long TotalReconnects => Interlocked.Read(ref _totalReconnects);

    /// <summary>총 Write 재전송 횟수.</summary>
    public long TotalWriteRetries => Interlocked.Read(ref _totalWriteRetries);

    /// <summary>평균 응답 시간(ms). RequestAsync 기준.</summary>
    public double AvgResponseMs
    {
        get
        {
            long count = Interlocked.Read(ref _totalResponseCount);
            return count == 0 ? 0
                : (double)Interlocked.Read(ref _totalResponseTimeMs) / count;
        }
    }

    /// <summary>최대 응답 시간(ms).</summary>
    public long MaxResponseMs => Interlocked.Read(ref _maxResponseTimeMs);

    /// <summary>마지막 오류 메시지.</summary>
    public string LastError => _lastError;

    /// <summary>마지막 오류 발생 시각 (HH:mm:ss.fff).</summary>
    public string LastErrorTime => _lastErrorTime;

    /// <summary>통계 수집 시작 시각.</summary>
    public DateTime StartedAt { get; } = DateTime.Now;

    /// <summary>통계 수집 경과 시간.</summary>
    public TimeSpan Elapsed => DateTime.Now - StartedAt;

    #endregion

    #region §3 ─ 내부 기록 (Infrastructure 에서만 호출)

    /// <summary>
    /// WriteAsync 성공 시 호출하여 전송 카운터를 증가시킵니다.
    /// * <para>Interlocked.Increment: 원자적으로 1씩 증가시킵니다.</para> 
    /// </summary>
    internal void RecordSent() => Interlocked.Increment(ref _totalSent);

    /// <summary>
    /// DeviceFrameReceived 이벤트 발생 시 호출하여 수신 카운터를 증가시킵니다.
    /// </summary>
    internal void RecordReceived() => Interlocked.Increment(ref _totalReceived);

    /// <summary>
    /// NetConnectionManager.HandleErrorAsync 진입 시 호출하여 재접속 카운터를 증가시킵니다.
    /// </summary>
    internal void RecordReconnect() => Interlocked.Increment(ref _totalReconnects);

    /// <summary>
    /// NetChannelBase.WriteAsync 실패 시 호출하여 Write 재전송 카운터를 증가시킵니다.
    /// </summary>
    internal void RecordWriteRetry() => Interlocked.Increment(ref _totalWriteRetries);

    /// <summary>
    /// DeviceErrorOccurred 이벤트 발생 시 호출하여 오류 카운터를 증가시키고, 마지막 오류 메시지와 시각을 기록합니다.
    /// </summary>
    internal void RecordError(string msg)
    {
        Interlocked.Increment(ref _totalErrors);
        _lastError = msg;
        _lastErrorTime = DateTime.Now.ToString("HH:mm:ss.fff");
    }

    /// <summary>
    /// RequestAsync 완료 시 호출하여 응답 시간(ms)을 기록합니다. 평균과 최대값 계산에 활용됩니다.
    /// </summary>
    internal void RecordResponse(long elapsedMs)
    {
        Interlocked.Increment(ref _totalResponseCount);
        Interlocked.Add(ref _totalResponseTimeMs, elapsedMs);

        // 최대값 CAS(Compare-And-Swap) 업데이트 — 스레드 안전
        // 현재 최대값보다 크고, 다른 스레드가 먼저 업데이트하지 않은 경우에만 갱신
        long cur;
        do { cur = Interlocked.Read(ref _maxResponseTimeMs); }
        while (elapsedMs > cur &&
               Interlocked.CompareExchange(ref _maxResponseTimeMs, elapsedMs, cur) != cur);
    }

    #endregion

    #region §4 ─ 리셋 / 스냅샷

    /// <summary>모든 카운터를 초기화합니다.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalSent, 0);
        Interlocked.Exchange(ref _totalReceived, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
        Interlocked.Exchange(ref _totalReconnects, 0);
        Interlocked.Exchange(ref _totalWriteRetries, 0);
        Interlocked.Exchange(ref _totalResponseTimeMs, 0);
        Interlocked.Exchange(ref _totalResponseCount, 0);
        Interlocked.Exchange(ref _maxResponseTimeMs, 0);
        _lastError = string.Empty;
        _lastErrorTime = string.Empty;
    }

    /// <summary>
    /// 현재 통계의 불변 스냅샷을 반환합니다.
    /// <para>WPF DataGrid 바인딩 또는 로그 기록에 활용합니다.</para>
    /// </summary>
    public NetStatisticsSnapshot Snapshot() => new(
        TotalSent, TotalReceived, TotalErrors, TotalReconnects,
        TotalWriteRetries, AvgResponseMs, MaxResponseMs,
        LastError, LastErrorTime, StartedAt, Elapsed);

    /// <inheritdoc/>
    public override string ToString()
        => $"전송={TotalSent} 수신={TotalReceived} 오류={TotalErrors} " +
           $"재접속={TotalReconnects} 평균응답={AvgResponseMs:F1}ms 최대={MaxResponseMs}ms";

    #endregion
}

/// <summary>
/// 특정 시점의 통계 불변 스냅샷.
/// <para>WPF DataGrid 바인딩용 읽기 전용 레코드입니다.</para>
/// </summary>
public sealed record NetStatisticsSnapshot(
    long TotalSent,
    long TotalReceived,
    long TotalErrors,
    long TotalReconnects,
    long TotalWriteRetries,
    double AvgResponseMs,
    long MaxResponseMs,
    string LastError,
    string LastErrorTime,
    DateTime StartedAt,
    TimeSpan Elapsed);