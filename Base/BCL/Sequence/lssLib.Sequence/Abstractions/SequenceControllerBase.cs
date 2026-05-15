// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Abstractions/SequenceControllerBase.cs
//  역할: ISequenceExecutor 공통 구현 추상 베이스
//        그룹 순차/병렬 처리, 결과 집계, 이벤트 발생 공통화
// ══════════════════════════════════════════════════════════════════════

using System.Diagnostics;

namespace lssLib.Sequence;

/// <summary>
/// <see cref="ISequenceExecutor"/> 공통 구현 추상 베이스.
/// </summary>
/// <remarks>
/// <para>
/// 파생 클래스는 아무것도 구현하지 않아도 기본 동작합니다.
/// 필요 시 <see cref="OnBeforeStepAsync"/> / <see cref="OnAfterStepAsync"/> 를 override 합니다.
/// </para>
///
/// <b>lssLib.Net 구현 (최소):</b>
/// <code>
/// public sealed class NetSequenceController : SequenceControllerBase
/// {
///     // 추가 구현 없이도 동작
///     // 필요 시 BeforeStep/AfterStep override
/// }
/// </code>
///
/// <b>커스텀 훅 추가:</b>
/// <code>
/// public class LoggingSequenceController : SequenceControllerBase
/// {
///     protected override Task OnBeforeStepAsync(
///         ISequenceStep step, ISequenceContext ctx, CancellationToken ct)
///     {
///         ctx.Log($"[시작] {step.StepName}");
///         return Task.CompletedTask;
///     }
///
///     protected override Task OnAfterStepAsync(
///         SequenceStepResult result, ISequenceContext ctx, CancellationToken ct)
///     {
///         ctx.Log($"[완료] {result}");
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </remarks>
public abstract class SequenceControllerBase : ISequenceExecutor
{
    #region §1 ─ 이벤트

    /// <summary>
    /// 각 스텝 실행 완료 시 발생 (성공/실패 모두).
    /// <para>⚠ 백그라운드 스레드 — WPF: Dispatcher.InvokeAsync 필요.</para>
    /// <example><code>
    /// controller.StepCompleted += r =>
    ///     Dispatcher.InvokeAsync(() =>
    ///     {
    ///         LblStep.Content  = r.Step.StepName;
    ///         PbProgress.Value = r.Step.StepIndex + 1;
    ///     });
    /// </code></example>
    /// </summary>
    public event Action<SequenceStepResult>? StepCompleted;

    /// <summary>시퀀스 시작 시 발생.</summary>
    public event Action<ISequenceDefinition>? SequenceStarted;

    /// <summary>시퀀스 완료 시 발생 (성공/실패 모두).</summary>
    public event Action<SequenceResult>? SequenceCompleted;

    #endregion

    #region §2 ─ ISequenceExecutor 구현

    /// <inheritdoc/>
    public async Task<SequenceResult> RunAsync(
        ISequenceDefinition sequence,
        ISequenceContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTime.Now;
        var stepResults = new List<SequenceStepResult>();

        SequenceStarted?.Invoke(sequence);
        context.Log($"▶ 시퀀스 시작: {sequence.Name}");

        // TotalTimeout 설정
        using var timeoutCts = sequence.TotalTimeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : null;
        if (timeoutCts is not null)
            timeoutCts.CancelAfter(sequence.TotalTimeout!.Value);
        var linkedCt = timeoutCts?.Token ?? ct;

        // SequenceBase 파생 타입일 때 그룹 구조 활용
        var groups = sequence is SequenceBase seqBase
            ? seqBase.Groups
            : [new SequenceGroup
            {
                ExecutionMode = StepExecutionMode.Sequential,
                Steps         = sequence.AllSteps.ToList()
            }];

        try
        {
            foreach (var group in groups)
            {
                if (linkedCt.IsCancellationRequested) break;

                List<SequenceStepResult> groupResults;

                if (group.ExecutionMode == StepExecutionMode.Parallel)
                {
                    // 병렬: 모든 스텝 동시 실행 (Task.WhenAll)
                    var tasks = group.Steps.Select(
                        step => RunStepAsync(step, context, linkedCt));
                    groupResults = [.. await Task.WhenAll(tasks).ConfigureAwait(false)];
                }
                else
                {
                    // 순차: 1개씩 순서대로
                    groupResults = [];
                    foreach (var step in group.Steps)
                    {
                        if (linkedCt.IsCancellationRequested) break;

                        var result = await RunStepAsync(step, context, linkedCt)
                            .ConfigureAwait(false);
                        groupResults.Add(result);

                        if (!result.IsSuccess && !sequence.ContinueOnError) break;
                    }
                }

                stepResults.AddRange(groupResults);

                bool groupFailed = groupResults.Any(r => !r.IsSuccess);
                if (groupFailed && !sequence.ContinueOnError) break;
            }
        }
        catch (OperationCanceledException)
        {
            bool timedOut = sequence.TotalTimeout.HasValue &&
                            DateTime.Now - startedAt >= sequence.TotalTimeout.Value;

            var cancelResult = SequenceResult.Failure(
                sequence.Name,
                timedOut ? SequenceStatus.TimedOut : SequenceStatus.Cancelled,
                timedOut
                    ? $"전체 타임아웃 ({sequence.TotalTimeout!.Value.TotalSeconds:F0}s)"
                    : "취소됨",
                null, stepResults, startedAt);

            context.LogError($"■ {cancelResult}");
            FireCompleted(sequence, cancelResult);
            return cancelResult;
        }

        // ── 최종 결과 집계 ──────────────────────────────────────────
        var failedStep = stepResults.FirstOrDefault(r => !r.IsSuccess);
        SequenceResult seqResult;

        if (failedStep is not null)
        {
            seqResult = SequenceResult.Failure(
                sequence.Name, SequenceStatus.Failed,
                failedStep.ErrorMessage ?? "스텝 실패",
                failedStep.Step, stepResults, startedAt);
            context.LogError($"■ {seqResult}");
        }
        else
        {
            seqResult = SequenceResult.Success(sequence.Name, stepResults, startedAt);
            context.Log($"■ {seqResult}");
        }

        FireCompleted(sequence, seqResult);
        return seqResult;
    }

    /// <inheritdoc/>
    public async Task<SequenceBatchResult> RunAllAsync(
        IEnumerable<ISequenceDefinition> sequences,
        ISequenceContext context,
        bool continueOnError = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        ArgumentNullException.ThrowIfNull(context);

        var sw = Stopwatch.StartNew();
        var results = new List<SequenceResult>();
        var list = sequences.ToList();

        context.Log($"▶ 배치 시작: {list.Count}개 시퀀스");

        foreach (var seq in list)
        {
            if (ct.IsCancellationRequested) break;

            var result = await RunAsync(seq, context, ct).ConfigureAwait(false);
            results.Add(result);

            if (!result.IsSuccess && !continueOnError) break;
        }

        var batchResult = new SequenceBatchResult
        {
            Results = results.AsReadOnly(),
            TotalElapsed = sw.Elapsed
        };

        context.Log($"■ 배치 완료: {batchResult}");
        return batchResult;
    }

    #endregion

    #region §3 ─ 스텝 실행

    /// <summary>단일 스텝 실행 + Before/After 훅 호출.</summary>
    private async Task<SequenceStepResult> RunStepAsync(
        ISequenceStep step,
        ISequenceContext context,
        CancellationToken ct)
    {
        await OnBeforeStepAsync(step, context, ct).ConfigureAwait(false);

        var result = await step.ExecuteAsync(context, lastResult, ct).ConfigureAwait(false);

        StepCompleted?.Invoke(result);
        await OnAfterStepAsync(result, context, ct).ConfigureAwait(false);

        return result;
    }

    #endregion

    #region §4 ─ 훅 (선택 오버라이드)

    /// <summary>
    /// 스텝 실행 전 호출됩니다. 기본: 아무것도 하지 않음.
    /// <para>로그, UI 업데이트, 사전 검증 등에 활용합니다.</para>
    /// </summary>
    protected virtual Task OnBeforeStepAsync(
        ISequenceStep step,
        ISequenceContext context,
        CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// 스텝 실행 후 호출됩니다. 기본: 아무것도 하지 않음.
    /// <para>로그, UI 업데이트, 사후 처리 등에 활용합니다.</para>
    /// </summary>
    protected virtual Task OnAfterStepAsync(
        SequenceStepResult result,
        ISequenceContext context,
        CancellationToken ct)
        => Task.CompletedTask;

    #endregion

    #region §5 ─ 헬퍼

    private void FireCompleted(ISequenceDefinition sequence, SequenceResult result)
    {
        if (sequence is SequenceBase seqBase)
            seqBase.OnCompleted?.Invoke(result);
        SequenceCompleted?.Invoke(result);
    }

    #endregion
}