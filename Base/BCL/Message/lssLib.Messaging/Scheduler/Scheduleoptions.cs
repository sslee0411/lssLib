// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · ScheduleOptions.cs
//  역할: 스케줄 작업 설정 옵션 클래스
// ══════════════════════════════════════════════════════════

namespace lssLib.Messaging;

/// <summary>
/// <see cref="AsyncScheduler"/>에 작업을 등록할 때 사용하는 설정 클래스.
/// </summary>
/// <remarks>
/// 기본값으로 즉시 시작하는 무한 반복 작업을 표현합니다.
/// <see cref="MaxRuns"/>를 양수로 설정하면 N회 실행 후 자동 종료됩니다.
/// </remarks>
/// <example><code>
/// // 5초 지연 후 10초마다 최대 100회
/// var options = new ScheduleOptions
/// {
///     Name         = "HeartbeatCheck",
///     InitialDelay = TimeSpan.FromSeconds(5),
///     Interval     = TimeSpan.FromSeconds(10),
///     MaxRuns      = 100,
///     Category     = "Network"
/// };
///
/// // 매일 오전 9시 실행 (DailyAt 팩토리 사용)
/// var daily = ScheduleOptions.DailyAt(TimeSpan.FromHours(9), "DailyReport");
/// </code></example>
public sealed class ScheduleOptions
{
    #region §1 ─ 기본 설정

    /// <summary>작업 이름 (로그·조회에 사용). 미설정 시 TaskId가 표시됩니다.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>최초 실행 전 대기 시간 (기본값: <see cref="TimeSpan.Zero"/> — 즉시 시작)</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 반복 실행 간격.
    /// <see cref="Timeout.InfiniteTimeSpan"/> (-1ms) 설정 시 1회만 실행 후 종료합니다.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 최대 실행 횟수 (-1 = 무한 반복, 기본값).
    /// 양수 설정 시 해당 횟수 실행 후 작업이 자동 종료됩니다.
    /// </summary>
    public int MaxRuns { get; set; } = -1;

    /// <summary>로그 기록 시 사용할 카테고리 (기본값: "Scheduler")</summary>
    public string Category { get; set; } = "Scheduler";

    /// <summary>
    /// 실행 중 예외 발생 시 재시도 여부 (기본값: true).
    /// false 설정 시 예외 발생 즉시 작업이 종료됩니다.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    #endregion

    #region §2 ─ 팩토리 메서드

    /// <summary>
    /// 1회만 실행하는 옵션을 생성합니다.
    /// </summary>
    /// <param name="delay">실행 전 대기 시간</param>
    /// <param name="name">작업 이름</param>
    public static ScheduleOptions Once(TimeSpan delay, string name = "") => new()
    {
        Name = name,
        InitialDelay = delay,
        Interval = Timeout.InfiniteTimeSpan,
        MaxRuns = 1,
    };

    /// <summary>
    /// 매일 지정한 시각에 실행하는 옵션을 생성합니다.
    /// </summary>
    /// <param name="timeOfDay">하루 중 실행 시각 (예: <c>TimeSpan.FromHours(9)</c> = 오전 9시)</param>
    /// <param name="name">작업 이름</param>
    public static ScheduleOptions DailyAt(TimeSpan timeOfDay, string name = "")
    {
        var now = DateTime.Now;
        var target = DateTime.Today.Add(timeOfDay);
        if (target <= now) target = target.AddDays(1);   // 이미 지났으면 내일

        return new ScheduleOptions
        {
            Name = name,
            InitialDelay = target - now,
            Interval = TimeSpan.FromDays(1),
            MaxRuns = -1,
        };
    }

    /// <summary>
    /// 즉시 시작하여 지정 간격으로 무한 반복하는 옵션을 생성합니다.
    /// </summary>
    /// <param name="interval">반복 간격</param>
    /// <param name="name">작업 이름</param>
    public static ScheduleOptions Recurring(TimeSpan interval, string name = "") => new()
    {
        Name = name,
        InitialDelay = TimeSpan.Zero,
        Interval = interval,
        MaxRuns = -1,
    };

    #endregion
}