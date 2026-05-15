// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Contracts/ISequenceExecutor.cs
//  역할: 시퀀스 실행기 계약 + 시퀀스 정의 계약
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Sequence;

/// <summary>
/// 시퀀스 정의 인터페이스.
/// 실행 가능한 시퀀스가 가져야 할 최소 계약입니다.
/// </summary>
public interface ISequenceDefinition
{
    /// <summary>시퀀스 이름.</summary>
    string Name { get; }

    /// <summary>오류 발생 시 계속 진행 여부.</summary>
    bool ContinueOnError { get; }

    /// <summary>전체 시퀀스 타임아웃. null=무제한.</summary>
    TimeSpan? TotalTimeout { get; }

    /// <summary>포함된 모든 스텝 (그룹 구조 무관하게 평탄화).</summary>
    IReadOnlyList<ISequenceStep> AllSteps { get; }
}

/// <summary>
/// 시퀀스 실행기 인터페이스.
/// </summary>
/// <remarks>
/// <para>
/// <b>구현체:</b>
/// <list type="bullet">
///   <item><description><c>SequenceControllerBase</c> — 추상 베이스 (그룹 순차/병렬 처리 포함)</description></item>
///   <item><description><c>NetSequenceController</c> — lssLib.Net 전용 구현</description></item>
/// </list>
/// </para>
///
/// <b>사용 예시:</b>
/// <code>
/// ISequenceExecutor executor = new NetSequenceController(registry);
///
/// // 단일 시퀀스
/// SequenceResult result = await executor.RunAsync(sequence, context, ct);
///
/// // 배치 (순서대로)
/// SequenceBatchResult batch = await executor.RunAllAsync(
///     [seqA, seqB, seqC], context, ct);
/// </code>
/// </remarks>
public interface ISequenceExecutor
{
    /// <summary>단일 시퀀스를 실행합니다.</summary>
    Task<SequenceResult> RunAsync(
        ISequenceDefinition sequence,
        ISequenceContext context,
        CancellationToken ct = default);

    /// <summary>여러 시퀀스를 순서대로 실행합니다 (배치).</summary>
    Task<SequenceBatchResult> RunAllAsync(
        IEnumerable<ISequenceDefinition> sequences,
        ISequenceContext context,
        bool continueOnError = false,
        CancellationToken ct = default);
}