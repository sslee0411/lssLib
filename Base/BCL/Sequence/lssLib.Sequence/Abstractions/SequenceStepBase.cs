// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Abstractions/SequenceStepBase.cs
//  역할: ISequenceStep 공통 구현 추상 베이스
// ══════════════════════════════════════════════════════════════════════

using System.Diagnostics;

namespace lssLib.Sequence;

/// <summary>
/// <see cref="ISequenceStep"/> 공통 구현 추상 베이스.
/// </summary>
/// <remarks>
/// <para>
/// 파생 클래스는 <see cref="ExecuteCoreAsync"/> 만 구현하면 됩니다.
/// Delay 처리, 재시도 로직, 결과 생성은 이 클래스가 담당합니다.
/// </para>
///
/// <b>파생 클래스 구현 패턴:</b>
/// <code>
/// // lssLib.Net 에서
/// public class NetWriteStep : SequenceStepBase
/// {
///     public int     DeviceId { get; init; }
///     public byte[]  Data     { get; init; } = [];
///     public NetPriority Priority { get; init; } = NetPriority.Write;
///
///     protected override async Task&lt;SequenceStepResult&gt; ExecuteCoreAsync(
///         ISequenceContext context, CancellationToken ct)
///     {
///         var channel = context.GetDevice(DeviceId) as NetChannelBase;
///         if (channel is null || !channel.IsConnected)
///             return SequenceStepResult.Fail(this, $"Device#{DeviceId} 연결 없음");
///
///         await channel.WriteAsync(Data, Priority, false, ct);
///         return SequenceStepResult.Ok(this);
///     }
/// }
/// </code>
///
/// <b>최소 구현 (커스텀 스텝):</b>
/// <code>
/// public class HttpCallStep : SequenceStepBase
/// {
///     public string Url    { get; init; } = string.Empty;
///     public string Method { get; init; } = "GET";
///
///     protected override async Task&lt;SequenceStepResult&gt; ExecuteCoreAsync(
///         ISequenceContext context, CancellationToken ct)
///     {
///         using var http = new HttpClient();
///         var resp = await http.GetAsync(Url, ct);
///         if (!resp.IsSuccessStatusCode)
///             return SequenceStepResult.Fail(this, $"HTTP {(int)resp.StatusCode}");
///         var body = await resp.Content.ReadAsByteArrayAsync(ct);
///         return SequenceStepResult.Ok(this, outputData: body);
///     }
/// }
/// </code>
/// </remarks>
public abstract class SequenceStepBase : ISequenceStep
{
    #region §1 ─ ISequenceStep 구현

    /// <inheritdoc/>
    public int StepIndex { get; set; }

    /// <inheritdoc/>
    public abstract string StepName { get; }

    /// <inheritdoc/>
    public virtual TimeSpan Delay { get; init; } = TimeSpan.Zero;

    /// <summary>최대 재시도 횟수. 0=재시도 없음 (기본값).</summary>
    public virtual int MaxRetries { get; init; } = 0;

    /// <summary>재시도 간 대기 시간. 기본값: 200ms.</summary>
    public virtual TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 스텝 완료 시 호출되는 콜백 (성공/실패 모두).
    /// <para>⚠ 백그라운드 스레드 — WPF: Dispatcher.InvokeAsync 필요.</para>
    /// </summary>
    public Action<SequenceStepResult>? OnCompleted { get; init; }

    /// <summary>
    /// 스텝을 실행합니다.
    /// Delay 처리 후 ExecuteCoreAsync 를 MaxRetries 횟수만큼 재시도합니다.
    /// </summary>
    public async Task<SequenceStepResult> ExecuteAsync(
        ISequenceContext context, SequenceStepResult lastResult, CancellationToken ct)
    {
        var startedAt = DateTime.Now;
        var sw = Stopwatch.StartNew();

        // ① 이전 스텝 완료 후 대기
        if (Delay > TimeSpan.Zero)
        {
            try { await Task.Delay(Delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                var cancelled = SequenceStepResult.Cancelled(this, sw.Elapsed, startedAt);
                OnCompleted?.Invoke(cancelled);
                return cancelled;
            }
        }

        // ② 실행 + 재시도
        int maxAttempts = MaxRetries + 1;
        SequenceStepResult? lastResult = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
            {
                lastResult = SequenceStepResult.Cancelled(this, sw.Elapsed, startedAt);
                break;
            }

            // 재시도 대기
            if (attempt > 0 && RetryDelay > TimeSpan.Zero)
            {
                try { await Task.Delay(RetryDelay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    lastResult = SequenceStepResult.Cancelled(this, sw.Elapsed, startedAt);
                    break;
                }
            }

            try
            {
                lastResult = await ExecuteCoreAsync(context, ct).ConfigureAwait(false);

                // 결과에 소요 시간 / 재시도 횟수 / 시작 시각 보완
                lastResult = lastResult with
                {
                    Elapsed = sw.Elapsed,
                    RetryCount = attempt,
                    StartedAt = startedAt
                };

                if (lastResult.IsSuccess) break;       // 성공 → 재시도 불필요
                if (attempt < maxAttempts - 1) continue; // 실패 → 재시도
            }
            catch (OperationCanceledException)
            {
                lastResult = SequenceStepResult.Cancelled(this, sw.Elapsed, startedAt);
                break;
            }
            catch (Exception ex)
            {
                lastResult = SequenceStepResult.Fail(this, ex, sw.Elapsed, attempt, startedAt);
                if (attempt < maxAttempts - 1) continue;
            }
        }

        lastResult ??= SequenceStepResult.Fail(this, "알 수 없는 오류",
            elapsed: sw.Elapsed, startedAt: startedAt);

        OnCompleted?.Invoke(lastResult);
        return lastResult;
    }

    #endregion

    #region §2 ─ 추상 메서드 (파생 클래스 필수 구현)

    /// <summary>
    /// 실제 스텝 로직을 구현합니다.
    /// <para>Delay, 재시도, 결과 래핑은 <see cref="ExecuteAsync"/> 가 처리합니다.</para>
    /// <para><c>OperationCanceledException</c> 은 catch 하지 말고 전파하세요.</para>
    /// </summary>
    protected abstract Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct);

    #endregion

    /// <inheritdoc/>
    public override string ToString()
        => $"[Step#{StepIndex}:{StepName}]";
}