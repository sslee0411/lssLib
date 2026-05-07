// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · ScheduledTask.cs
//  역할: 스케줄 작업 상태 및 제어 핸들
// ══════════════════════════════════════════════════════════

namespace lssLib.Messaging;

/// <summary>
/// <see cref="AsyncScheduler"/>에 등록된 작업의 상태와 제어 핸들.
/// </summary>
/// <remarks>
/// <see cref="AsyncScheduler.Schedule"/>의 반환값으로 제공됩니다.
/// <see cref="Pause"/>, <see cref="Resume"/>, <see cref="Cancel"/> 메서드로 작업을 제어합니다.
/// </remarks>
/// <example><code>
/// var task = AsyncScheduler.Instance.Schedule(async ct =>
/// {
///     await SensorService.PollAsync(ct);
/// }, ScheduleOptions.Recurring(TimeSpan.FromSeconds(5), "SensorPoll"));
///
/// Console.WriteLine(task.TaskId);    // "A3F2B1C0"
/// Console.WriteLine(task.RunCount);  // 실행 횟수
///
/// task.Pause();    // 일시 정지
/// task.Resume();   // 재개
/// task.Cancel();   // 완전 종료
/// </code></example>
public sealed class ScheduledTask
{
    #region §1 ─ 내부 필드

    private readonly CancellationTokenSource _cts;
    private volatile bool _paused;
    private int _runCount;

    #endregion

    #region §2 ─ 생성자 (internal — AsyncScheduler에서만 생성)

    internal ScheduledTask(string name, string category, CancellationTokenSource cts)
    {
        TaskId = Guid.NewGuid().ToString("N")[..8].ToUpper();
        Name = name.Length > 0 ? name : TaskId;
        Category = category;
        _cts = cts;
    }

    #endregion

    #region §3 ─ 공개 프로퍼티

    /// <summary>작업 고유 ID (8자리 대문자 16진수)</summary>
    public string TaskId { get; }

    /// <summary>작업 이름 (<see cref="ScheduleOptions.Name"/>)</summary>
    public string Name { get; }

    /// <summary>로그 카테고리 (<see cref="ScheduleOptions.Category"/>)</summary>
    public string Category { get; }

    /// <summary>현재 일시 정지 여부</summary>
    public bool IsPaused => _paused;

    /// <summary>취소(종료) 여부</summary>
    public bool IsCancelled => _cts.IsCancellationRequested;

    /// <summary>활성 여부 (일시 정지 또는 취소되지 않은 상태)</summary>
    public bool IsActive => !_paused && !_cts.IsCancellationRequested;

    /// <summary>누적 실행 횟수</summary>
    public int RunCount => _runCount;

    /// <summary>마지막 실행 시작 시각</summary>
    public DateTime? LastRunAt { get; internal set; }

    /// <summary>다음 실행 예정 시각</summary>
    public DateTime? NextRunAt { get; internal set; }

    /// <summary>마지막 실행에서 발생한 예외 (성공 시 null)</summary>
    public Exception? LastError { get; internal set; }

    #endregion

    #region §4 ─ 제어 메서드

    /// <summary>
    /// 작업을 일시 정지합니다.
    /// 현재 실행 중인 반복이 완료된 후 다음 실행을 건너뜁니다.
    /// </summary>
    public void Pause() => _paused = true;

    /// <summary>일시 정지된 작업을 재개합니다.</summary>
    public void Resume() => _paused = false;

    /// <summary>
    /// 작업을 완전히 종료합니다. 취소 후에는 재개할 수 없습니다.
    /// </summary>
    public void Cancel() => _cts.Cancel();

    #endregion

    #region §5 ─ 내부 메서드

    internal void IncrementRunCount() => Interlocked.Increment(ref _runCount);
    internal CancellationToken Token => _cts.Token;

    #endregion

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{TaskId}] {Name}  runs={RunCount}  " +
        $"paused={IsPaused}  cancelled={IsCancelled}  " +
        $"next={NextRunAt:HH:mm:ss}";
}