// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Core/SequenceEnums.cs
//  역할: 시퀀스 관련 열거형 정의
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Sequence;

/// <summary>
/// 스텝 그룹 내 실행 방식.
/// </summary>
public enum StepExecutionMode
{
    /// <summary>순차 실행 — 스텝을 1개씩 순서대로 실행합니다.</summary>
    Sequential = 0,

    /// <summary>병렬 실행 — 스텝을 모두 동시에 실행합니다 (Task.WhenAll).</summary>
    Parallel = 1
}

/// <summary>
/// 개별 스텝의 실행 상태.
/// </summary>
public enum SequenceStepStatus
{
    /// <summary>아직 실행되지 않음.</summary>
    Pending = 0,

    /// <summary>실행 중.</summary>
    Running = 1,

    /// <summary>성공 완료.</summary>
    Completed = 2,

    /// <summary>실패 (오류 또는 검증 실패).</summary>
    Failed = 3,

    /// <summary>취소됨.</summary>
    Cancelled = 4,

    /// <summary>건너뜀 (ContinueOnError 모드에서 앞선 실패로 인해).</summary>
    Skipped = 5
}

/// <summary>
/// 시퀀스 전체의 실행 상태.
/// </summary>
public enum SequenceStatus
{
    /// <summary>아직 실행되지 않음.</summary>
    Pending = 0,

    /// <summary>실행 중.</summary>
    Running = 1,

    /// <summary>모든 스텝 성공 완료.</summary>
    Completed = 2,

    /// <summary>하나 이상의 스텝 실패.</summary>
    Failed = 3,

    /// <summary>CancellationToken 으로 취소됨.</summary>
    Cancelled = 4,

    /// <summary>TotalTimeout 초과로 중단됨.</summary>
    TimedOut = 5
}