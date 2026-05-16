// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Abstractions/SequenceBase.cs
//  역할: ISequenceDefinition 공통 구현 추상 베이스 + SequenceGroup
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Sequence;

// ── SequenceGroup ────────────────────────────────────────────────────

/// <summary>
/// 시퀀스 내 하나의 실행 단위(그룹).
/// Sequential 또는 Parallel 모드로 소속 스텝들을 실행합니다.
/// </summary>
public sealed class SequenceGroup
{
    /// <summary>그룹 이름 (로그·디버깅용).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 그룹 내 스텝 실행 방식.
    /// <list type="table">
    ///   <item><term>Sequential</term><description>스텝 1개씩 순서대로 (기본값)</description></item>
    ///   <item><term>Parallel</term><description>스텝 동시 실행 (Task.WhenAll)</description></item>
    /// </list>
    /// </summary>
    public StepExecutionMode ExecutionMode { get; init; } = StepExecutionMode.Sequential;

    /// <summary>이 그룹에 속한 스텝 목록.</summary>
    public List<ISequenceStep> Steps { get; init; } = [];

    /// <inheritdoc/>
    public override string ToString()
        => $"[{(string.IsNullOrEmpty(Name) ? ExecutionMode.ToString() : Name)} " +
           $"{ExecutionMode} {Steps.Count}스텝]";
}

// ── SequenceBase ─────────────────────────────────────────────────────

/// <summary>
/// <see cref="ISequenceDefinition"/> 공통 구현 추상 베이스.
/// </summary>
/// <remarks>
/// <para>
/// 파생 클래스는 <see cref="Groups"/> 를 채우면 됩니다.
/// AllSteps 평탄화, ToString 은 이 클래스가 처리합니다.
/// </para>
///
/// <b>파생 클래스 패턴 (직접 상속):</b>
/// <code>
/// public class MySequence : SequenceBase
/// {
///     public MySequence(string name) : base(name) { }
///
///     public MySequence AddGroup(SequenceGroup group)
///     {
///         Groups.Add(group);
///         return this;
///     }
/// }
/// </code>
///
/// <b>일반적으로는 SequenceBuilderBase 를 통해 생성합니다:</b>
/// <code>
/// var seq = new NetSequenceBuilder(defaultDeviceId: 1)
///     .Write("모터 기동",  [0x01, 0x06, ...])
///     .Request("상태 확인",[0x01, 0x03, ...], validator: r => r.IsOk)
///     .Build("모터 시퀀스");
/// </code>
/// </remarks>
public abstract class SequenceBase : ISequenceDefinition
{
    #region §1 ─ ISequenceDefinition

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool ContinueOnError { get; set; } = false;

    /// <inheritdoc/>
    public TimeSpan? TotalTimeout { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<ISequenceStep> AllSteps
        => Groups.SelectMany(g => g.Steps).ToList().AsReadOnly();

    #endregion

    #region §2 ─ 그룹 목록

    /// <summary>시퀀스를 구성하는 그룹 목록. 빌더 또는 파생 클래스에서 채웁니다.</summary>
    internal List<SequenceGroup> Groups { get; } = [];

    #endregion

    #region §3 ─ 이벤트

    /// <summary>시퀀스 완료 시 호출되는 콜백 (성공/실패 모두).</summary>
    public Action<SequenceResult>? OnCompleted { get; set; }

    #endregion

    #region §4 ─ 생성자

    protected SequenceBase(string name)
        => Name = string.IsNullOrWhiteSpace(name) ? "Sequence" : name;

    #endregion

    /// <inheritdoc/>
    public override string ToString()
    {
        int stepCount = AllSteps.Count;
        return $"[{Name}] {Groups.Count}그룹 {stepCount}스텝 " +
               $"ContinueOnError={ContinueOnError}";
    }
}

// ── 기본 구현체 (직접 사용 가능) ─────────────────────────────────────

/// <summary>
/// 가장 단순한 SequenceBase 구현체.
/// 빌더 없이 직접 그룹/스텝을 추가할 때 사용합니다.
/// </summary>
/// <example><code>
/// var seq = new SequenceDefinition("공정 A");
/// var group = new SequenceGroup { ExecutionMode = StepExecutionMode.Sequential };
/// group.Steps.Add(new MyWriteStep { StepIndex = 0, ... });
/// seq.AddGroup(group);
/// </code></example>
public sealed class SequenceDefinition : SequenceBase
{
    public SequenceDefinition(string name) : base(name) { }

    /// <summary>그룹을 추가합니다.</summary>
    public SequenceDefinition AddGroup(SequenceGroup group)
    {
        Groups.Add(group);
        return this;
    }
}