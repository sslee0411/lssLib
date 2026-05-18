// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Contracts/ISequenceStep.cs
//  역할: 모든 시퀀스 스텝의 핵심 계약 인터페이스
//
//  ┌─ Node-RED 와의 유사성 ──────────────────────────────────────────┐
//  │                                                                  │
//  │  Node-RED 에서 각 노드가 msg 객체를 받아 처리하고                │
//  │  다음 노드로 전달하듯,                                           │
//  │                                                                  │
//  │  ISequenceStep 도 ISequenceContext (=msg) 를 받아               │
//  │  처리 후 SequenceStepResult 를 반환합니다.                       │
//  │                                                                  │
//  │  Node-RED Node:  process(msg) → msg                             │
//  │  ISequenceStep:  ExecuteAsync(context, ct) → StepResult         │
//  │                                                                  │
//  └──────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Sequence;

/// <summary>
/// 모든 시퀀스 스텝의 핵심 계약 인터페이스.
/// </summary>
/// <remarks>
/// <para>
/// Node-RED 의 노드처럼, 이 인터페이스를 구현하면 어떤 작업이든 시퀀스 스텝이 될 수 있습니다:
/// </para>
/// <list type="bullet">
///   <item><description>lssLib.Net 장비 제어 (NetWriteStep, NetRequestStep)</description></item>
///   <item><description>DB 쿼리 실행 (DbQueryStep)</description></item>
///   <item><description>HTTP 호출 (HttpCallStep)</description></item>
///   <item><description>조건 분기 (ConditionalStep)</description></item>
///   <item><description>변수 설정 (SetVariableStep)</description></item>
///   <item><description>로그 기록 (LogStep)</description></item>
///   <item><description>순수 대기 (DelayStep)</description></item>
/// </list>
///
/// <b>최소 구현 예시:</b>
/// <code>
/// public class LogStep : ISequenceStep
/// {
///     public int      StepIndex { get; set; }
///     public string   StepName  { get; init; } = "Log";
///     public TimeSpan Delay     { get; init; } = TimeSpan.Zero;
///
///     private readonly string _message;
///     public LogStep(string message) => _message = message;
///
///     public Task<SequenceStepResult> ExecuteAsync(
///         ISequenceContext context, CancellationToken ct)
///     {
///         context.Log(_message);
///         return Task.FromResult(SequenceStepResult.Ok(this));
///     }
/// }
/// </code>
/// </remarks>
public interface ISequenceStep
{
    /// <summary>
    /// 스텝 고유 번호 (0-based, SequenceBuilder 가 자동 부여).
    /// </summary>
    int StepIndex { get; set; }

    /// <summary>
    /// 스텝 이름 (로그·UI 표시용).
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// 이 스텝 실행 전 대기 시간 (이전 스텝 완료 후).
    /// <para>Zero=즉시 실행.</para>
    /// </summary>
    TimeSpan Delay { get; }

    /// <summary>
    /// 스텝을 실행합니다.
    /// </summary>
    /// <param name="context">
    /// 실행 컨텍스트 — 장비 접근, 변수 저장소, 로그 기능을 제공합니다.
    /// </param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>스텝 실행 결과</returns>
    /// <remarks>
    /// <para>
    /// 구현 시 주의사항:
    /// <list type="bullet">
    ///   <item><description><c>OperationCanceledException</c> 은 catch 하지 말고 그대로 전파하세요.</description></item>
    ///   <item><description>예외 발생 시 <see cref="SequenceStepResult.Fail"/> 로 감싸서 반환하세요.</description></item>
    ///   <item><description>Delay 는 SequenceControllerBase 가 처리하므로 직접 구현하지 않아도 됩니다.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Task<SequenceStepResult> ExecuteAsync(ISequenceContext context, CancellationToken ct);
}