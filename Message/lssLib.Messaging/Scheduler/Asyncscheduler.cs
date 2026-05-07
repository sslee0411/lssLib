// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · AsyncScheduler.cs
//  역할: 비동기 작업 스케줄러 싱글톤
// ══════════════════════════════════════════════════════════

//using lssLib.Log;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace lssLib.Messaging;

/// <summary>
/// 지연·반복·일별 실행을 지원하는 비동기 작업 스케줄러 싱글톤.
/// </summary>
/// <remarks>
/// <para>각 작업은 독립적인 Task 루프로 실행되므로 상호 영향이 없습니다.</para>
/// <para>작업 추가 후 반환되는 <see cref="ScheduledTask"/> 핸들을 통해
/// Pause / Resume / Cancel 을 제어합니다.</para>
/// <para>스케줄러 전체 정지는 <see cref="StopAsync"/>를 사용하며,
/// 등록된 모든 작업에 취소 신호를 보냅니다.</para>
/// </remarks>
/// <example><code>
/// // 1. 5초마다 센서 폴링 (무한 반복)
/// var task = AsyncScheduler.Instance.Schedule(async ct =>
/// {
///     var data = await sensor.ReadAsync(ct);
///     LogManager.Instance.Info("Sensor", $"온도: {data.Temp}°C");
/// }, ScheduleOptions.Recurring(TimeSpan.FromSeconds(5), "SensorPoll"));
///
/// // 2. 3초 뒤 1회 실행
/// AsyncScheduler.Instance.Schedule(async ct =>
/// {
///     await InitializeAsync(ct);
/// }, ScheduleOptions.Once(TimeSpan.FromSeconds(3), "InitTask"));
///
/// // 3. 매일 오전 2시 정기 정리
/// AsyncScheduler.Instance.Schedule(async ct =>
/// {
///     await CleanupOldLogsAsync(ct);
/// }, ScheduleOptions.DailyAt(TimeSpan.FromHours(2), "NightlyCleanup"));
///
/// // 4. 일시 정지 / 재개
/// task.Pause();
/// await Task.Delay(10_000);
/// task.Resume();
///
/// // 5. 앱 종료 시
/// await AsyncScheduler.Instance.StopAsync();
/// </code></example>
public sealed class AsyncScheduler
{
    #region §1 ─ 싱글톤

    private static readonly Lazy<AsyncScheduler> _lazy = new(() => new AsyncScheduler());

    /// <summary>스레드 안전 싱글톤 인스턴스 (Lazy&lt;T&gt; 기반)</summary>
    public static AsyncScheduler Instance => _lazy.Value;

    private AsyncScheduler() { }

    #endregion

    #region §2 ─ 필드

    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private const string LOG_SOURCE = "AsyncScheduler";

    #endregion

    #region §3 ─ 작업 등록

    /// <summary>
    /// 지정한 옵션으로 비동기 작업을 스케줄러에 등록합니다.
    /// </summary>
    /// <param name="action">실행할 비동기 작업</param>
    /// <param name="options">스케줄 설정 (<see cref="ScheduleOptions"/> 참고)</param>
    /// <returns>작업 제어 핸들 (<see cref="ScheduledTask"/>)</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 또는 <paramref name="options"/>가 null인 경우</exception>
    /// <example><code>
    /// var task = AsyncScheduler.Instance.Schedule(async ct =>
    /// {
    ///     await PollSensorAsync(ct);
    /// }, ScheduleOptions.Recurring(TimeSpan.FromSeconds(5), "Sensor"));
    ///
    /// Console.WriteLine(task.TaskId);  // "A3F2B1C0"
    /// </code></example>
    public ScheduledTask Schedule(
        Func<CancellationToken, Task> action,
        ScheduleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        options ??= new ScheduleOptions();

        var cts = new CancellationTokenSource();
        var task = new ScheduledTask(options.Name, options.Category, cts);

        _tasks[task.TaskId] = task;

        // 독립 Task 루프 실행 (fire-and-forget — 예외는 내부에서 로그 처리)
        _ = RunTaskLoopAsync(task, action, options);

        //LogManager.Instance.Info(LOG_SOURCE,
        //    $"Schedule [{task.Name}] id={task.TaskId}  " +
        //    $"delay={options.InitialDelay.TotalSeconds:F1}s  " +
        //    $"interval={options.Interval.TotalSeconds:F1}s  " +
        //    $"maxRuns={options.MaxRuns}");

        return task;
    }

    /// <summary>
    /// 동기 Action을 스케줄러에 등록합니다.
    /// </summary>
    /// <param name="action">실행할 동기 작업</param>
    /// <param name="options">스케줄 설정</param>
    /// <returns>작업 제어 핸들</returns>
    public ScheduledTask Schedule(Action action, ScheduleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Schedule(_ => { action(); return Task.CompletedTask; }, options);
    }

    #endregion

    #region §4 ─ 편의 메서드

    /// <summary>
    /// 지정한 지연 후 1회 실행합니다.
    /// </summary>
    /// <param name="delay">실행 전 대기 시간</param>
    /// <param name="action">실행할 작업</param>
    /// <param name="name">작업 이름</param>
    /// <returns>작업 제어 핸들</returns>
    /// <example><code>
    /// AsyncScheduler.Instance.ScheduleOnce(TimeSpan.FromSeconds(3), async ct =>
    /// {
    ///     await InitializeDeviceAsync(ct);
    /// }, "DeviceInit");
    /// </code></example>
    public ScheduledTask ScheduleOnce(
        TimeSpan delay,
        Func<CancellationToken, Task> action,
        string name = "")
        => Schedule(action, ScheduleOptions.Once(delay, name));

    /// <summary>
    /// 지정한 간격으로 즉시 무한 반복 실행합니다.
    /// </summary>
    /// <param name="interval">반복 간격</param>
    /// <param name="action">실행할 작업</param>
    /// <param name="name">작업 이름</param>
    /// <returns>작업 제어 핸들</returns>
    /// <example><code>
    /// var heartbeat = AsyncScheduler.Instance.ScheduleRecurring(
    ///     TimeSpan.FromSeconds(10),
    ///     async ct => await SendHeartbeatAsync(ct),
    ///     "Heartbeat");
    /// </code></example>
    public ScheduledTask ScheduleRecurring(
        TimeSpan interval,
        Func<CancellationToken, Task> action,
        string name = "")
        => Schedule(action, ScheduleOptions.Recurring(interval, name));

    /// <summary>
    /// 매일 지정한 시각에 실행합니다.
    /// </summary>
    /// <param name="timeOfDay">하루 중 실행 시각 (예: <c>TimeSpan.FromHours(9)</c> = 오전 9시)</param>
    /// <param name="action">실행할 작업</param>
    /// <param name="name">작업 이름</param>
    /// <returns>작업 제어 핸들</returns>
    /// <example><code>
    /// AsyncScheduler.Instance.ScheduleDailyAt(
    ///     TimeSpan.FromHours(2),
    ///     async ct => await CleanupOldLogsAsync(ct),
    ///     "NightlyCleanup");
    /// </code></example>
    public ScheduledTask ScheduleDailyAt(
        TimeSpan timeOfDay,
        Func<CancellationToken, Task> action,
        string name = "")
        => Schedule(action, ScheduleOptions.DailyAt(timeOfDay, name));

    #endregion

    #region §5 ─ 작업 제어

    /// <summary>TaskId로 특정 작업을 취소합니다.</summary>
    /// <param name="taskId">취소할 <see cref="ScheduledTask.TaskId"/></param>
    /// <returns>해당 TaskId의 작업이 존재하면 true</returns>
    public bool Cancel(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task)) return false;
        task.Cancel();
        return true;
    }

    /// <summary>등록된 모든 작업을 일시 정지합니다.</summary>
    public void PauseAll()
    {
        foreach (var task in _tasks.Values) task.Pause();
    //    LogManager.Instance.Info(LOG_SOURCE, $"PauseAll — {_tasks.Count}개 작업 일시 정지");
    }

    /// <summary>일시 정지된 모든 작업을 재개합니다.</summary>
    public void ResumeAll()
    {
        foreach (var task in _tasks.Values) task.Resume();
    //    LogManager.Instance.Info(LOG_SOURCE, $"ResumeAll — {_tasks.Count}개 작업 재개");
    }

    /// <summary>
    /// 스케줄러를 정지합니다. 등록된 모든 작업에 취소 신호를 보냅니다.
    /// </summary>
    /// <param name="timeout">최대 대기 시간 (기본값: 5초)</param>
    public async Task StopAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);

    //    LogManager.Instance.Info(LOG_SOURCE,
    //        $"StopAsync — {_tasks.Count}개 작업 종료 중...");

        foreach (var task in _tasks.Values)
            task.Cancel();

        // 모든 작업 종료 대기 (폴링)
        var sw = Stopwatch.StartNew();
        int remaining;

        while ((remaining = _tasks.Values.Count(t => !t.IsCancelled)) > 0
               && sw.Elapsed < timeout)
        {
            await Task.Delay(50);
        }

        _tasks.Clear();

    //    LogManager.Instance.Info(LOG_SOURCE,
    //        remaining == 0
    //            ? "StopAsync 완료 — 모든 작업 종료"
    //            : $"StopAsync 완료 (타임아웃) — {remaining}개 작업 강제 종료");
    }

    #endregion

    #region §6 ─ 상태 조회

    /// <summary>등록된 모든 작업 목록을 반환합니다 (스냅샷).</summary>
    public IReadOnlyList<ScheduledTask> GetTasks()
        => [.. _tasks.Values];

    /// <summary>TaskId로 특정 작업을 조회합니다.</summary>
    /// <param name="taskId">조회할 TaskId</param>
    /// <returns>작업 핸들 (존재하지 않으면 null)</returns>
    public ScheduledTask? GetTask(string taskId)
        => _tasks.TryGetValue(taskId, out var t) ? t : null;

    /// <summary>현재 등록된 작업 수</summary>
    public int TaskCount => _tasks.Count;

    #endregion

    #region §7 ─ 내부 실행 루프

    private async Task RunTaskLoopAsync(
        ScheduledTask scheduledTask,
        Func<CancellationToken, Task> action,
        ScheduleOptions options)
    {
        var ct = scheduledTask.Token;

        try
        {
            // ── 초기 지연 ─────────────────────────────────────────
            if (options.InitialDelay > TimeSpan.Zero)
            {
                scheduledTask.NextRunAt = DateTime.Now + options.InitialDelay;
             //   LogManager.Instance.Debug(LOG_SOURCE,
             //       $"[{scheduledTask.Name}] 초기 대기 {options.InitialDelay.TotalSeconds:F1}s");

                await Task.Delay(options.InitialDelay, ct);
            }

            // ── 실행 루프 ──────────────────────────────────────────
            bool isOnce = options.Interval == Timeout.InfiniteTimeSpan;

            while (!ct.IsCancellationRequested)
            {
                // 최대 실행 횟수 체크
                if (options.MaxRuns > 0 && scheduledTask.RunCount >= options.MaxRuns)
                {
                //    LogManager.Instance.Info(LOG_SOURCE,
                //        $"[{scheduledTask.Name}] MaxRuns({options.MaxRuns}) 도달 — 종료");
                    break;
                }

                // 일시 정지 대기
                if (scheduledTask.IsPaused)
                {
                    await Task.Delay(100, ct);
                    continue;
                }

                // ── 작업 실행 ─────────────────────────────────────
                scheduledTask.LastRunAt = DateTime.Now;
                scheduledTask.IncrementRunCount();

                var sw = Stopwatch.StartNew();
                try
                {
                    await action(ct);
                    sw.Stop();
                    scheduledTask.LastError = null;

                //    LogManager.Instance.Debug(options.Category,
                //        $"[{scheduledTask.Name}] run#{scheduledTask.RunCount}  " +
                //        $"{sw.ElapsedMilliseconds}ms");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    sw.Stop();
                    scheduledTask.LastError = ex;

                //    LogManager.Instance.Error(options.Category,
                //        $"[{scheduledTask.Name}] run#{scheduledTask.RunCount}  " +
                //        $"예외: {ex.Message}");

                    if (!options.ContinueOnError) break;
                }

                // 1회 실행 후 종료
                if (isOnce) break;

                // ── 다음 실행까지 대기 ────────────────────────────
                // DailyAt은 InitialDelay 이후 매일 Interval(24h)로 반복
                scheduledTask.NextRunAt = DateTime.Now + options.Interval;

            //    LogManager.Instance.Debug(LOG_SOURCE,
            //        $"[{scheduledTask.Name}] 다음 실행: {scheduledTask.NextRunAt:HH:mm:ss}");

                await Task.Delay(options.Interval, ct);
            }
        }
        catch (OperationCanceledException)
        {
        //    LogManager.Instance.Debug(LOG_SOURCE,
        //        $"[{scheduledTask.Name}] 취소됨 (run#{scheduledTask.RunCount})");
        }
        catch (Exception ex)
        {
        //    LogManager.Instance.Error(LOG_SOURCE,
        //        $"[{scheduledTask.Name}] 루프 예외: {ex.Message}");
        }
        finally
        {
            // 완료 시 레지스트리에서 제거
            _tasks.TryRemove(scheduledTask.TaskId, out _);

        //    LogManager.Instance.Info(LOG_SOURCE,
        //        $"[{scheduledTask.Name}] 종료 — 총 {scheduledTask.RunCount}회 실행");
        }
    }

    #endregion
}