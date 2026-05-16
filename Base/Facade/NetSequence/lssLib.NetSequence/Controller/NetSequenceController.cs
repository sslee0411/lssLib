// ══════════════════════════════════════════════════════════════════════
//  lssLib.NetSequence · Controller/NetSequenceController.cs
//  역할: lssLib.Sequence.SequenceControllerBase 의 Net 구현체
//
//  ┌─ 계층 구조 ─────────────────────────────────────────────────────┐
//  │  ISequenceExecutor              (lssLib.Sequence 계약)          │
//  │    └─ SequenceControllerBase    (lssLib.Sequence 공통 엔진)     │
//  │         └─ NetSequenceController ← 이 파일 (Net 특화)           │
//  └─────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using lssLib.Sequence;

namespace lssLib.NetSequence;

/// <summary>
/// lssLib.Net 전용 시퀀스 실행 컨트롤러.
/// </summary>
/// <remarks>
/// <b>기본 사용:</b>
/// <code>
/// var controller = new NetSequenceController();
/// var context    = new NetSequenceContext(
///     logAction: msg => LogManager.Instance.Info("Seq", msg));
///
/// // 이벤트 구독 (SequenceControllerBase 에서 선언)
/// controller.StepCompleted     += r => Console.WriteLine(r);
/// controller.SequenceStarted   += s => Console.WriteLine($"▶ {s.Name}");
/// controller.SequenceCompleted += r => Console.WriteLine($"■ {r}");
///
/// // 단일 시퀀스
/// SequenceResult result = await controller.RunAsync(sequence, context, ct);
///
/// // 배치 (여러 시퀀스 순서대로)
/// SequenceBatchResult batch = await controller.RunAllAsync(
///     [seqA, seqB, seqC], context, continueOnError: false, ct);
/// </code>
///
/// <b>WPF 진행률 표시 (BeforeStep/AfterStep 훅):</b>
/// <code>
/// public class WpfNetSequenceController : NetSequenceController
/// {
///     protected override Task OnBeforeStepAsync(
///         ISequenceStep step, ISequenceContext ctx, CancellationToken ct)
///     {
///         App.Current.Dispatcher.InvokeAsync(() =>
///         {
///             LblStep.Content  = $"[{step.StepIndex + 1}] {step.StepName}";
///             PbProgress.Value = step.StepIndex + 1;
///         });
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </remarks>
public class NetSequenceController : SequenceControllerBase
{
    // ── 기본 구현은 SequenceControllerBase 가 모두 처리 ──────────────
    //
    // SequenceControllerBase 제공 이벤트:
    //   StepCompleted     → 각 스텝 완료 시 (성공/실패 모두)
    //   SequenceStarted   → 시퀀스 시작 시
    //   SequenceCompleted → 시퀀스 완료 시 (성공/실패 모두)
    //
    // RunAsync / RunAllAsync → SequenceControllerBase 구현
    //
    // 필요 시 OnBeforeStepAsync / OnAfterStepAsync 를 override 하세요.
}