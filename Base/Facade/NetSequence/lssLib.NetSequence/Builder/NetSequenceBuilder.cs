// ══════════════════════════════════════════════════════════════════════
//  lssLib.NetSequence · Builder/NetSequenceBuilder.cs
//  역할: lssLib.Sequence 빌더를 상속한 Net 전용 Fluent 빌더
//
//  ┌─ 빌더 계층 구조 ───────────────────────────────────────────────┐
//  │  SequenceBuilderBase<TStep, TBuilder>  (lssLib.Sequence)       │
//  │    └─ NetSequenceBuilder               ← 단일 장비 순차 빌더   │
//  │                                                                 │
//  │  GroupSequenceBuilderBase<TStep, TBuilder>  (lssLib.Sequence)  │
//  │    └─ NetGroupSequenceBuilder          ← 다중 장비 그룹 빌더   │
//  │                                                                 │
//  │  [진입점 — 정적 클래스]                                         │
//  │    NetSequence.For(deviceId)   → NetSequenceBuilder            │
//  │    NetSequence.Create(name)    → NetGroupSequenceBuilder        │
//  └─────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.Sequence;

namespace lssLib.NetSequence;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  §1 NetSequence — 정적 진입점
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// lssLib.NetSequence 빌더 진입점 (정적 팩토리).
/// </summary>
/// <example><code>
/// // 단일 장비 순차
/// var seq = NetSequence.For(deviceId: 1)
///     .Write("모터 기동",   [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
///     .Delay(500)
///     .Request("상태 확인", [0x01, 0x03, 0x00, 0x64, 0x00, 0x01],
///         validator: r => r.IsOk)
///     .Build("모터 시퀀스");
///
/// // 다중 장비 그룹
/// var seq = NetSequence.Create("공정")
///     .Then(StepExecutionMode.Sequential)
///         .AddWrite(1, "밸브 열기",  [0x01, 0x06, ...])
///         .AddWrite(2, "펌프 기동",  [0x02, 0x06, ...])
///     .Then(StepExecutionMode.Parallel)
///         .AddRequest(3, "압력",     [0x03, 0x03, ...], r => r.IsOk)
///         .AddRequest(4, "온도",     [0x04, 0x03, ...], r => r.IsOk)
///     .Build();
/// </code></example>
public static class NetSequence
{
    /// <summary>단일 장비 순차 시퀀스 빌더를 시작합니다.</summary>
    public static NetSequenceBuilder For(int deviceId)
        => new(deviceId);

    /// <summary>다중 장비 그룹 시퀀스 빌더를 시작합니다.</summary>
    public static NetGroupSequenceBuilder Create(string name = "Sequence")
        => new(name);
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  §2 NetSequenceBuilder — 단일 장비 Fluent 빌더
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// 단일 장비 대상 Fluent 빌더.
/// <c>NetSequence.For(deviceId)</c> 로 시작합니다.
/// </summary>
public sealed class NetSequenceBuilder
    : SequenceBuilderBase<NetSequenceStepBase, NetSequenceBuilder>
{
    private readonly int _defaultDeviceId;

    internal NetSequenceBuilder(int defaultDeviceId)
        => _defaultDeviceId = defaultDeviceId;

    /// <summary>Write 스텝 추가 (단방향 전송, 응답 없음).</summary>
    /// <param name="stepName">스텝 이름</param>
    /// <param name="data">전송 페이로드</param>
    /// <param name="delayMs">실행 전 대기(ms). 0=즉시.</param>
    /// <param name="priority">큐 우선순위</param>
    /// <param name="maxRetries">최대 재시도 횟수</param>
    /// <param name="deviceId">대상 장비 ID. null=기본 DeviceId.</param>
    /// <param name="onCompleted">완료 콜백</param>
    public NetSequenceBuilder Write(
        string stepName,
        byte[] data,
        int delayMs = 0,
        NetPriority priority = NetPriority.Write,
        int maxRetries = 0,
        int? deviceId = null,
        Action<SequenceStepResult>? onCompleted = null)
        => AddStep(new NetWriteStep(stepName)
        {
            DeviceId = deviceId ?? _defaultDeviceId,
            Data = data,
            Priority = priority,
            Delay = TimeSpan.FromMilliseconds(delayMs),
            MaxRetries = maxRetries,
            OnCompleted = onCompleted
        });

    /// <summary>Request 스텝 추가 (요청-응답, 검증 포함).</summary>
    /// <param name="stepName">스텝 이름</param>
    /// <param name="data">요청 페이로드</param>
    /// <param name="validator">응답 검증 함수. null=검증 없음.</param>
    /// <param name="timeoutMs">응답 타임아웃(ms). 0=채널 기본값.</param>
    /// <param name="retries">최대 재시도 횟수</param>
    /// <param name="retryDelayMs">재시도 간 대기(ms)</param>
    /// <param name="delayMs">실행 전 대기(ms)</param>
    /// <param name="priority">큐 우선순위</param>
    /// <param name="deviceId">대상 장비 ID. null=기본 DeviceId.</param>
    /// <param name="onCompleted">완료 콜백</param>
    public NetSequenceBuilder Request(
        string stepName,
        byte[] data,
        Func<NetResult, bool>? validator = null,
        int timeoutMs = 0,
        int retries = 0,
        int retryDelayMs = 200,
        int delayMs = 0,
        NetPriority priority = NetPriority.Write,
        int? deviceId = null,
        Action<SequenceStepResult>? onCompleted = null)
        => AddStep(new NetRequestStep(stepName)
        {
            DeviceId = deviceId ?? _defaultDeviceId,
            Data = data,
            Timeout = timeoutMs > 0
                                ? TimeSpan.FromMilliseconds(timeoutMs) : null,
            MaxRetries = retries,
            RetryDelay = TimeSpan.FromMilliseconds(retryDelayMs),
            Delay = TimeSpan.FromMilliseconds(delayMs),
            Priority = priority,
            ResponseValidator = validator,
            OnCompleted = onCompleted
        });

    /// <summary>Delay 스텝 추가 (ms 단위).</summary>
    public NetSequenceBuilder Delay(int ms, string name = "Delay")
        => AddDelay(TimeSpan.FromMilliseconds(ms), name);

    /// <summary>Delay 스텝 추가 (TimeSpan 단위).</summary>
    public NetSequenceBuilder Delay(TimeSpan duration, string name = "Delay")
        => AddDelay(duration, name);
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  §3 NetGroupSequenceBuilder — 다중 장비 그룹 빌더
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// 다중 장비 연계 그룹 빌더.
/// <c>NetSequence.Create(name)</c> 로 시작합니다.
/// </summary>
public sealed class NetGroupSequenceBuilder
    : GroupSequenceBuilderBase<NetSequenceStepBase, NetGroupSequenceBuilder>
{
    internal NetGroupSequenceBuilder(string name) : base(name) { }

    /// <summary>새 그룹을 시작합니다.</summary>
    public new NetGroupSequenceBuilder Then(
        StepExecutionMode mode = StepExecutionMode.Sequential,
        string name = "")
        => base.Then(mode, name);

    /// <summary>현재 그룹에 Write 스텝을 추가합니다.</summary>
    public NetGroupSequenceBuilder AddWrite(
        int deviceId,
        string stepName,
        byte[] data,
        int delayMs = 0,
        NetPriority priority = NetPriority.Write,
        int maxRetries = 0,
        Action<SequenceStepResult>? onCompleted = null)
        => AddStepToCurrentGroup(new NetWriteStep(stepName)
        {
            DeviceId = deviceId,
            Data = data,
            Priority = priority,
            Delay = TimeSpan.FromMilliseconds(delayMs),
            MaxRetries = maxRetries,
            OnCompleted = onCompleted
        });

    /// <summary>현재 그룹에 Request 스텝을 추가합니다.</summary>
    public NetGroupSequenceBuilder AddRequest(
        int deviceId,
        string stepName,
        byte[] data,
        Func<NetResult, bool>? validator = null,
        int timeoutMs = 0,
        int retries = 0,
        int retryDelayMs = 200,
        int delayMs = 0,
        NetPriority priority = NetPriority.Write,
        Action<SequenceStepResult>? onCompleted = null)
        => AddStepToCurrentGroup(new NetRequestStep(stepName)
        {
            DeviceId = deviceId,
            Data = data,
            Timeout = timeoutMs > 0
                                ? TimeSpan.FromMilliseconds(timeoutMs) : null,
            MaxRetries = retries,
            RetryDelay = TimeSpan.FromMilliseconds(retryDelayMs),
            Delay = TimeSpan.FromMilliseconds(delayMs),
            Priority = priority,
            ResponseValidator = validator,
            OnCompleted = onCompleted
        });

    /// <summary>현재 그룹에 Delay 스텝을 추가합니다 (ms 단위).</summary>
    public NetGroupSequenceBuilder AddDelay(int ms, string name = "Delay")
        => AddDelay(TimeSpan.FromMilliseconds(ms), name);
}