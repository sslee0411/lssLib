// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetStatistics.cs
//  역할: 장비별 통신 통계 수집 (Interlocked 스레드 안전)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 장비 채널의 통신 통계를 수집합니다.
/// </summary>
/// <remarks>
/// <b>수집 항목:</b> 전송·수신·오류 카운터, 평균·최대 응답 시간, 재접속·재전송 횟수, 마지막 오류.
///
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

    private long _totalSent;            // 총 전송 패킷 수
    private long _totalReceived;        // 총 수신 프레임 수
    private long _totalErrors;          //  총 오류 수 (전송 실패 + CRC 오류 등)
    private long _totalReconnects;      // 총 재접속 시도 수
    private long _totalWriteRetries;    // 총 Write 재전송 수 (Write 실패 시 재시도)
    private long _totalResponseTimeMs;  //  총 응답 시간 누적 (ms, RequestAsync 기준)
    private long _totalResponseCount;   //  총 응답 횟수 (RequestAsync 기준, 평균 계산용)
    private long _maxResponseTimeMs;    //  최대 응답 시간 (ms, RequestAsync 기준)
    private volatile string _lastError = string.Empty;      // 마지막 오류 메시지 (전송 실패, CRC 오류 등)
    private volatile string _lastErrorTime = string.Empty;  // 마지막 오류 발생 시각 (HH:mm:ss.fff)

    #endregion

    #region §2 ─ 공개 프로퍼티

    /// <summary>
    /// 총 전송 성공 횟수.
    ///  Interlocked.Read 
    ///    - 값을 읽는 도중에 다른 스레드가 값을 수정하지 못하도록 하거나, 
    ///    - 수정 중인 값을 읽지 않도록 보장
    /// </summary>
    public long TotalSent => Interlocked.Read(ref _totalSent);

    /// <summary>총 수신 프레임 횟수.</summary>
    public long TotalReceived => Interlocked.Read(ref _totalReceived);

    /// <summary>총 오류 횟수 (전송 실패 + CRC 오류 등).</summary>
    public long TotalErrors => Interlocked.Read(ref _totalErrors);

    /// <summary>총 재접속 횟수.</summary>
    public long TotalReconnects => Interlocked.Read(ref _totalReconnects);

    /// <summary>총 Write 재전송 횟수.</summary>
    public long TotalWriteRetries => Interlocked.Read(ref _totalWriteRetries);

    /// <summary>평균 응답 시간 (ms). RequestAsync 기준.</summary>
    public double AvgResponseMs
    {
        get
        {
            long count = Interlocked.Read(ref _totalResponseCount);
            return count == 0 ? 0 : (double)Interlocked.Read(ref _totalResponseTimeMs) / count;
        }
    }

    /// <summary>최대 응답 시간 (ms).</summary>
    public long MaxResponseMs => Interlocked.Read(ref _maxResponseTimeMs);

    /// <summary>마지막 오류 메시지.</summary>
    public string LastError => _lastError;

    /// <summary>마지막 오류 발생 시각.</summary>
    public string LastErrorTime => _lastErrorTime;

    /// <summary>통계 수집 시작 시각.</summary>
    public DateTime StartedAt { get; } = DateTime.Now;

    /// <summary>통계 수집 경과 시간.</summary>
    public TimeSpan Elapsed => DateTime.Now - StartedAt;

    #endregion

    #region §3 ─ 내부 기록 (NetDispatchPipeline / NetConnectionManager 에서 호출)

    /// <summary>
    /// 전송 성공 시 호출하여 전송 카운터를 증가시킵니다.
    /// </summary>
    internal void RecordSent() => Interlocked.Increment(ref _totalSent);

    /// <summary>
    /// 수신 프레임 처리 시 호출하여 수신 카운터를 증가시킵니다.
    /// </summary>
    internal void RecordReceived() => Interlocked.Increment(ref _totalReceived);

    /// <summary>
    /// 재접속 시도 시 호출하여 재접속 카운터를 증가시킵니다.
    /// </summary>
    internal void RecordReconnect() => Interlocked.Increment(ref _totalReconnects);

    /// <summary>
    /// Write 실패 시 호출하여 Write 재전송 카운터를 증가시킵니다.
    /// </summary>
    internal void RecordWriteRetry() => Interlocked.Increment(ref _totalWriteRetries);

    /// <summary>
    /// 오류 발생 시 호출하여 오류 카운터를 증가시키고, 마지막 오류 메시지와 시각을 기록합니다.
    /// </summary>
    /// <param name="msg"></param>
    internal void RecordError(string msg)
    {
        Interlocked.Increment(ref _totalErrors);
        _lastError = msg;
        _lastErrorTime = DateTime.Now.ToString("HH:mm:ss.fff");
    }

    /// <summary>
    /// RequestAsync 응답 처리 시 호출하여 응답 시간(ms)을 기록합니다. 평균과 최대값이 자동 계산됩니다.
    /// </summary>
    /// <param name="elapsedMs"></param>
    internal void RecordResponse(long elapsedMs)
    {
        Interlocked.Increment(ref _totalResponseCount);
        Interlocked.Add(ref _totalResponseTimeMs, elapsedMs);
        // 최대값 CAS 업데이트
        long cur;
        do { cur = Interlocked.Read(ref _maxResponseTimeMs); }
        while (elapsedMs > cur &&
               Interlocked.CompareExchange(ref _maxResponseTimeMs, elapsedMs, cur) != cur);
        // 현재 최대값이 여전히 cur인 경우에만 업데이트 (다른 스레드가 이미 더 큰 값을 기록했을 수 있음)
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

    /// <summary>현재 통계의 불변 스냅샷을 반환합니다 (WPF DataGrid 바인딩 등).</summary>
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

/// <summary>특정 시점의 통계 불변 스냅샷.</summary>
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