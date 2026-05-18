// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Builder/SequenceBuilderBase.cs
//  역할: 범용 시퀀스 빌더 추상 베이스 (Fluent API)
//
//  ┌─ 빌더 계층 구조 ────────────────────────────────────────────────┐
//  │                                                                  │
//  │  SequenceBuilderBase<TStep, TBuilder>  ← 이 파일 (추상)         │
//  │    ├─ NetSequenceBuilder               ← lssLib.Net 구현         │
//  │    ├─ DbSequenceBuilder                ← DB 작업 구현 (예시)     │
//  │    └─ HttpSequenceBuilder              ← HTTP 파이프 (예시)      │
//  │                                                                  │
//  │  GroupSequenceBuilderBase<TStep, TBuilder> ← 다중 장비 빌더     │
//  │    └─ NetGroupSequenceBuilder          ← lssLib.Net 다중 구현    │
//  │                                                                  │
//  └──────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Sequence;

/// <summary>
/// 단일 그룹(순차) 기반 시퀀스 빌더 추상 베이스.
/// <para>단일 장비 순차 제어에 주로 사용합니다.</para>
/// </summary>
/// <typeparam name="TStep">빌더가 생성하는 스텝 타입 (<see cref="SequenceStepBase"/> 파생)</typeparam>
/// <typeparam name="TBuilder">파생 빌더 타입 (Fluent 체이닝을 위한 self-referential)</typeparam>
/// <remarks>
/// <b>파생 클래스 구현 패턴:</b>
/// <code>
/// // lssLib.Net 단일 장비 빌더
/// public sealed class NetSequenceBuilder
///     : SequenceBuilderBase<NetSequenceStepBase, NetSequenceBuilder>
/// {
///     private readonly int _defaultDeviceId;
///
///     public NetSequenceBuilder(int defaultDeviceId) : base()
///         => _defaultDeviceId = defaultDeviceId;
///
///     // 도메인 특화 Write 메서드 추가
///     public NetSequenceBuilder Write(string name, byte[] data,
///         NetPriority priority = NetPriority.Write, ...)
///     {
///         return AddStep(new NetWriteStep
///         {
///             StepName = name,
///             DeviceId = _defaultDeviceId,
///             Data     = data,
///             Priority = priority,
///             ...
///         });
///     }
/// }
/// </code>
/// </remarks>
public abstract class SequenceBuilderBase<TStep, TBuilder>
    where TStep : SequenceStepBase
    where TBuilder : SequenceBuilderBase<TStep, TBuilder>
{
    #region §1 ─ 필드

    /// <summary>단일 순차 그룹. 모든 스텝이 이 그룹에 추가됩니다.</summary>
    private readonly SequenceGroup _group = new()
    {
        ExecutionMode = StepExecutionMode.Sequential
    };

    private int _stepIndex = 0;

    #endregion

    #region §2 ─ 스텝 추가

    /// <summary>
    /// 스텝을 그룹에 추가합니다.
    /// StepIndex 를 자동 부여하고 Fluent 체이닝을 반환합니다.
    /// </summary>
    protected TBuilder AddStep(TStep step)
    {
        step.StepIndex = _stepIndex++;
        _group.Steps.Add(step);
        return (TBuilder)this;
    }

    /// <summary>
    /// 순수 대기(Delay) 스텝을 추가합니다.
    /// <para>구현체 없이 기본 <see cref="DelayStep"/> 을 사용합니다.</para>
    /// </summary>
    public TBuilder AddDelay(TimeSpan duration, string name = "Delay")
    {
        var step = new DelayStep { StepName_ = name, Delay = duration };
        step.StepIndex = _stepIndex++;
        _group.Steps.Add(step);
        return (TBuilder)this;
    }

    #endregion

    #region §3 ─ 빌드

    /// <summary>
    /// 시퀀스를 완성합니다.
    /// </summary>
    /// <param name="name">시퀀스 이름</param>
    /// <param name="continueOnError">오류 발생 시 계속 진행 여부</param>
    /// <param name="totalTimeout">전체 타임아웃. null=무제한.</param>
    /// <param name="onCompleted">완료 콜백</param>
    public SequenceDefinition Build(
        string name = "Sequence",
        bool continueOnError = false,
        TimeSpan? totalTimeout = null,
        Action<SequenceResult>? onCompleted = null)
    {
        if (_group.Steps.Count == 0)
            throw new InvalidOperationException(
                $"시퀀스 '{name}' 에 스텝이 없습니다. 스텝을 먼저 추가하세요.");

        var seq = new SequenceDefinition(name)
        {
            ContinueOnError = continueOnError,
            TotalTimeout = totalTimeout,
            OnCompleted = onCompleted
        };
        seq.AddGroup(_group);
        return seq;
    }

    /// <summary>현재 등록된 스텝 수를 반환합니다.</summary>
    public int StepCount => _group.Steps.Count;

    #endregion
}

// ══════════════════════════════════════════════════════════════════════
//  GroupSequenceBuilderBase — 다중 그룹(Sequential + Parallel) 빌더
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 다중 그룹 기반 시퀀스 빌더 추상 베이스.
/// <para>다중 장비 연계 또는 복잡한 조건 분기 제어에 사용합니다.</para>
/// </summary>
/// <typeparam name="TStep">빌더가 생성하는 스텝 타입</typeparam>
/// <typeparam name="TBuilder">파생 빌더 타입 (Fluent 체이닝)</typeparam>
/// <remarks>
/// <b>파생 클래스 구현 패턴:</b>
/// <code>
/// public sealed class NetGroupSequenceBuilder
///     : GroupSequenceBuilderBase<NetSequenceStepBase, NetGroupSequenceBuilder>
/// {
///     public NetGroupSequenceBuilder(string name) : base(name) { }
///
///     // 도메인 특화 스텝 추가 메서드
///     public NetGroupSequenceBuilder AddStep(
///         int deviceId, string name, byte[] data, ...)
///     {
///         return AddStepToCurrentGroup(new NetWriteStep { ... });
///     }
/// }
/// </code>
/// </remarks>
public abstract class GroupSequenceBuilderBase<TStep, TBuilder>
    where TStep : SequenceStepBase
    where TBuilder : GroupSequenceBuilderBase<TStep, TBuilder>
{
    #region §1 ─ 필드

    private readonly string _name;
    private readonly List<SequenceGroup> _groups = [];
    private SequenceGroup? _currentGroup;
    private int _stepIndex = 0;

    #endregion

    protected GroupSequenceBuilderBase(string name) => _name = name;

    #region §2 ─ 그룹 제어

    /// <summary>
    /// 새 스텝 그룹을 시작합니다.
    /// </summary>
    /// <param name="mode">그룹 내 실행 방식 (Sequential/Parallel)</param>
    /// <param name="name">그룹 이름 (로그용)</param>
    public TBuilder Then(
        StepExecutionMode mode = StepExecutionMode.Sequential,
        string name = "")
    {
        _currentGroup = new SequenceGroup
        {
            ExecutionMode = mode,
            Name = name
        };
        _groups.Add(_currentGroup);
        return (TBuilder)this;
    }

    #endregion

    #region §3 ─ 스텝 추가

    /// <summary>현재 그룹에 스텝을 추가합니다.</summary>
    protected TBuilder AddStepToCurrentGroup(TStep step)
    {
        EnsureGroup();
        step.StepIndex = _stepIndex++;
        _currentGroup!.Steps.Add(step);
        return (TBuilder)this;
    }

    /// <summary>현재 그룹에 순수 대기 스텝을 추가합니다.</summary>
    public TBuilder AddDelay(TimeSpan duration, string name = "Delay")
    {
        EnsureGroup();
        var step = new DelayStep { StepName_ = name, Delay = duration };
        step.StepIndex = _stepIndex++;
        _currentGroup!.Steps.Add(step);
        return (TBuilder)this;
    }

    #endregion

    #region §4 ─ 빌드

    /// <summary>다중 그룹 시퀀스를 완성합니다.</summary>
    /// <param name="continueOnError">오류 발생 시 계속 진행 여부</param>
    /// <param name="totalTimeout">전체 타임아웃. null=무제한.</param>
    /// <param name="onCompleted">완료 콜백</param>
    public SequenceDefinition Build(
        bool continueOnError = false,
        TimeSpan? totalTimeout = null,
        Action<SequenceResult>? onCompleted = null)
    {
        if (_groups.Count == 0)
            throw new InvalidOperationException(
                $"시퀀스 '{_name}' 에 그룹이 없습니다. Then() 을 먼저 호출하세요.");

        var seq = new SequenceDefinition(_name)
        {
            ContinueOnError = continueOnError,
            TotalTimeout = totalTimeout,
            OnCompleted = onCompleted
        };
        foreach (var g in _groups) seq.AddGroup(g);
        return seq;
    }

    #endregion

    #region §5 ─ 헬퍼

    private void EnsureGroup()
    {
        if (_currentGroup is null)
        {
            _currentGroup = new SequenceGroup
            {
                ExecutionMode = StepExecutionMode.Sequential
            };
            _groups.Add(_currentGroup);
        }
    }

    #endregion
}

// ══════════════════════════════════════════════════════════════════════
//  DelayStep — 순수 대기 스텝 (범용, lssLib.Sequence 내장)
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 순수 대기(Delay) 스텝. 모든 빌더에서 공통으로 사용합니다.
/// </summary>
public sealed class DelayStep : SequenceStepBase
{
    // StepName 을 init 으로 설정하기 위해 내부 필드 사용
    internal string StepName_ { get; init; } = "Delay";
    public override string StepName => StepName_;
    
    protected override Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        // Delay 처리는 SequenceStepBase.ExecuteAsync 가 담당
        // 여기서는 즉시 성공 반환
        return Task.FromResult(SequenceStepResult.Ok(this));
    }
}