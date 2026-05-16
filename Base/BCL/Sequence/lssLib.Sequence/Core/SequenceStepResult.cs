// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Core/SequenceResults.cs
//  역할: 스텝 / 시퀀스 / 배치 실행 결과 값 타입
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Sequence;

// ══════════════════════════════════════════════════════════════════════
//  §1 스텝 결과
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 단일 스텝의 실행 결과.
/// </summary>
/// <remarks>
/// <b>팩토리 메서드로 생성합니다:</b>
/// <code>
/// return SequenceStepResult.Ok(this);
/// return SequenceStepResult.Fail(this, "연결 없음");
/// return SequenceStepResult.Fail(this, exception);
/// </code>
/// </remarks>
public sealed class SequenceStepResult
{
    #region §1 ─ 프로퍼티

    /// <summary>실행된 스텝 정의.</summary>
    public ISequenceStep Step { get; private init; } = null!;

    /// <summary>스텝 성공 여부.</summary>
    public bool IsSuccess => Status == SequenceStepStatus.Completed;

    /// <summary>스텝 실행 상태.</summary>
    public SequenceStepStatus Status { get; private init; }

    /// <summary>실패 원인 메시지. 성공 시 null.</summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>발생한 예외. 성공 시 null.</summary>
    public Exception? Exception { get; private init; }

    /// <summary>
    /// 스텝 실행 결과 데이터 (응답 바이트 등).
    /// <para>구현체에서 자유롭게 사용합니다. lssLib.Net: NetResult 저장.</para>
    /// </summary>
    public object? OutputData { get; private init; }

    /// <summary>실행 소요 시간.</summary>
    public TimeSpan Elapsed { get; private init; }

    /// <summary>실제 재시도 횟수.</summary>
    public int RetryCount { get; private init; }

    /// <summary>스텝 시작 시각.</summary>
    public DateTime StartedAt { get; private init; }

    #endregion

    #region §2 ─ 팩토리

    /// <summary>성공 결과를 생성합니다.</summary>
    public static SequenceStepResult Ok(
        ISequenceStep step,
        object? outputData = null,
        TimeSpan elapsed = default,
        int retryCount = 0,
        DateTime startedAt = default)
        => new()
        {
            Step = step,
            Status = SequenceStepStatus.Completed,
            OutputData = outputData,
            Elapsed = elapsed,
            RetryCount = retryCount,
            StartedAt = startedAt == default ? DateTime.Now : startedAt
        };

    /// <summary>실패 결과를 생성합니다 (메시지).</summary>
    public static SequenceStepResult Fail(
        ISequenceStep step,
        string errorMessage,
        object? outputData = null,
        TimeSpan elapsed = default,
        int retryCount = 0,
        DateTime startedAt = default)
        => new()
        {
            Step = step,
            Status = SequenceStepStatus.Failed,
            ErrorMessage = errorMessage,
            OutputData = outputData,
            Elapsed = elapsed,
            RetryCount = retryCount,
            StartedAt = startedAt == default ? DateTime.Now : startedAt
        };

    /// <summary>실패 결과를 생성합니다 (예외).</summary>
    public static SequenceStepResult Fail(
        ISequenceStep step,
        Exception exception,
        TimeSpan elapsed = default,
        int retryCount = 0,
        DateTime startedAt = default)
        => new()
        {
            Step = step,
            Status = SequenceStepStatus.Failed,
            ErrorMessage = exception.Message,
            Exception = exception,
            Elapsed = elapsed,
            RetryCount = retryCount,
            StartedAt = startedAt == default ? DateTime.Now : startedAt
        };

    /// <summary>취소 결과를 생성합니다.</summary>
    public static SequenceStepResult Cancelled(
        ISequenceStep step,
        TimeSpan elapsed = default,
        DateTime startedAt = default)
        => new()
        {
            Step = step,
            Status = SequenceStepStatus.Cancelled,
            ErrorMessage = "취소됨",
            Elapsed = elapsed,
            StartedAt = startedAt == default ? DateTime.Now : startedAt
        };

    #endregion

    #region §3 ─ 내부 타이밍 적용 (SequenceStepBase 전용)

    /// <summary>
    /// 실행 후 타이밍 정보를 적용한 새 결과를 반환합니다.
    /// ExecuteCoreAsync 결과에 Elapsed / RetryCount / StartedAt 을 덮어씁니다.
    /// </summary>
    internal SequenceStepResult ApplyTiming(
        TimeSpan elapsed, int retryCount, DateTime startedAt)
        => new()
        {
            Step = Step,
            Status = Status,
            ErrorMessage = ErrorMessage,
            Exception = Exception,
            OutputData = OutputData,
            Elapsed = elapsed,
            RetryCount = retryCount,
            StartedAt = startedAt
        };

    #endregion
    /// <inheritdoc/>
    public override string ToString()
    {
        string strStatus = Status switch
        {
            SequenceStepStatus.Completed => "✔",
            SequenceStepStatus.Failed => "✘",
            SequenceStepStatus.Cancelled => "⊘",
            SequenceStepStatus.Skipped => "–",
            _ => "?"
        };
        string strRetry = RetryCount > 0 ? $" 재시도={RetryCount}" : "";
        string strErr = ErrorMessage is not null ? $" ({ErrorMessage})" : "";
        return $"[{Step.StepIndex:D2}:{Step.StepName}] {strStatus}" +
               $" {Elapsed.TotalMilliseconds:F0}ms{strRetry}{strErr}";
    }
}

// ══════════════════════════════════════════════════════════════════════
//  §2 시퀀스 전체 결과
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 시퀀스 전체 실행 결과.
/// </summary>
public sealed class SequenceResult
{
    #region §1 ─ 프로퍼티

    /// <summary>시퀀스 이름.</summary>
    public string SequenceName { get; init; } = string.Empty;

    /// <summary>시퀀스 실행 상태.</summary>
    public SequenceStatus Status { get; init; }

    /// <summary>전체 성공 여부.</summary>
    public bool IsSuccess => Status == SequenceStatus.Completed;

    /// <summary>실패 원인 메시지. 성공 시 null.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>최초 실패 스텝. 성공 시 null.</summary>
    public ISequenceStep? FailedStep { get; init; }

    /// <summary>모든 스텝의 개별 실행 결과 (WPF DataGrid 바인딩용).</summary>
    public IReadOnlyList<SequenceStepResult> StepResults { get; init; }
        = Array.Empty<SequenceStepResult>();

    /// <summary>성공한 스텝 수.</summary>
    public int SuccessCount => StepResults.Count(r => r.IsSuccess);

    /// <summary>실패한 스텝 수.</summary>
    public int FailCount => StepResults.Count(r => r.Status == SequenceStepStatus.Failed);

    /// <summary>시퀀스 전체 실행 시간.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>시퀀스 시작 시각.</summary>
    public DateTime StartedAt { get; init; }

    #endregion

    #region §2 ─ 팩토리

    internal static SequenceResult Success(
        string name, List<SequenceStepResult> steps, DateTime startedAt)
        => new()
        {
            SequenceName = name,
            Status = SequenceStatus.Completed,
            StepResults = steps.AsReadOnly(),
            Elapsed = DateTime.Now - startedAt,
            StartedAt = startedAt
        };

    internal static SequenceResult Failure(
        string name, SequenceStatus status, string error,
        ISequenceStep? failedStep,
        List<SequenceStepResult> steps, DateTime startedAt)
        => new()
        {
            SequenceName = name,
            Status = status,
            ErrorMessage = error,
            FailedStep = failedStep,
            StepResults = steps.AsReadOnly(),
            Elapsed = DateTime.Now - startedAt,
            StartedAt = startedAt
        };

    #endregion

    /// <inheritdoc/>
    public override string ToString()
        => $"[{SequenceName}] {Status} 스텝={StepResults.Count} " +
           $"성공={SuccessCount} 실패={FailCount} " +
           $"({Elapsed.TotalMilliseconds:F0}ms)";
}

// ══════════════════════════════════════════════════════════════════════
//  §3 배치 실행 결과
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 여러 시퀀스를 순서대로 실행한 배치 결과.
/// </summary>
public sealed class SequenceBatchResult
{
    /// <summary>전체 성공 여부. 모든 시퀀스가 성공해야 true.</summary>
    public bool IsSuccess => Results.All(r => r.IsSuccess);

    /// <summary>개별 시퀀스 결과 목록.</summary>
    public IReadOnlyList<SequenceResult> Results { get; init; }
        = Array.Empty<SequenceResult>();

    /// <summary>성공한 시퀀스 수.</summary>
    public int SuccessCount => Results.Count(r => r.IsSuccess);

    /// <summary>실패한 시퀀스 수.</summary>
    public int FailCount => Results.Count(r => !r.IsSuccess);

    /// <summary>전체 실행 시간.</summary>
    public TimeSpan TotalElapsed { get; init; }

    /// <inheritdoc/>
    public override string ToString()
        => $"[Batch] {(IsSuccess ? "전체 성공" : "일부 실패")} " +
           $"{Results.Count}시퀀스 성공={SuccessCount} 실패={FailCount} " +
           $"({TotalElapsed.TotalMilliseconds:F0}ms)";
}