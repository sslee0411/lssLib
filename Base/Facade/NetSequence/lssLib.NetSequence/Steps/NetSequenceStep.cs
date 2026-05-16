// ══════════════════════════════════════════════════════════════════════
//  lssLib.NetSequence · Steps/NetSequenceStep.cs
//  역할: lssLib.Sequence.SequenceStepBase 를 상속한 Net 전용 스텝
//
//  ┌─ 계층 구조 ─────────────────────────────────────────────────────┐
//  │  SequenceStepBase      (lssLib.Sequence — 범용, BCL only)       │
//  │    └─ NetSequenceStepBase  ← Net 공통 추상 (DeviceId + 채널조회)│
//  │         ├─ NetWriteStep    ← WriteAsync 전용                    │
//  │         ├─ NetRequestStep  ← RequestAsync + 응답 검증           │
//  │         └─ NetDelayStep    ← 순수 대기                          │
//  └─────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.Sequence;

namespace lssLib.NetSequence;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  §1 NetSequenceStepBase — Net 스텝 공통 추상 베이스
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// lssLib.Net 도메인 전용 스텝 추상 베이스.
/// DeviceId 프로퍼티와 채널 조회 헬퍼를 공통으로 제공합니다.
/// </summary>
public abstract class NetSequenceStepBase : SequenceStepBase
{
    /// <summary>
    /// 대상 장비 ID.
    /// <see cref="NetSequenceContext"/> 가 NetDeviceRegistry 에서 채널을 조회합니다.
    /// </summary>
    public int DeviceId { get; init; }

    /// <summary>
    /// ISequenceContext 에서 NetChannelBase 를 안전하게 꺼냅니다.
    /// 없거나 연결 안 된 경우 SequenceStepResult.Fail 을 반환합니다.
    /// </summary>
    protected bool TryGetChannel(
        ISequenceContext context,
        out NetChannelBase? channel,
        out SequenceStepResult? failResult)
    {
        channel = null;
        failResult = null;

        var device = context.GetDevice(DeviceId);
        if (device is null)
        {
            failResult = SequenceStepResult.Fail(this,
                $"Device#{DeviceId} 가 Registry 에 없습니다.");
            return false;
        }
        if (device is not NetChannelBase ch)
        {
            failResult = SequenceStepResult.Fail(this,
                $"Device#{DeviceId} 가 NetChannelBase 타입이 아닙니다.");
            return false;
        }
        if (!ch.IsConnected)
        {
            failResult = SequenceStepResult.Fail(this,
                $"Device#{DeviceId}({ch.DeviceName}) 연결 없음 ({ch.State})");
            return false;
        }
        channel = ch;
        return true;
    }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  §2 NetWriteStep — 단방향 전송 스텝
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// 단방향 전송 스텝 (응답 없음).
/// </summary>
/// <remarks>
/// <b>직접 생성:</b>
/// <code>
/// var step = new NetWriteStep("모터 기동")
/// {
///     DeviceId = 1,
///     Data     = [0x01, 0x06, 0x00, 0x01, 0x00, 0x01],
///     Priority = NetPriority.Write
/// };
/// </code>
///
/// <b>빌더를 통한 생성 (권장):</b>
/// <code>
/// NetSequenceBuilder.For(1).Write("모터 기동", [0x01, 0x06, ...])
/// </code>
/// </remarks>
public sealed class NetWriteStep : NetSequenceStepBase
{
    private readonly string _stepName;
    public override string StepName => _stepName;

    /// <summary>전송할 원시 페이로드 (프로토콜 인코딩 전).</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>전송 우선순위. 기본값: Write(1).</summary>
    public NetPriority Priority { get; init; } = NetPriority.Write;

    public NetWriteStep(string stepName = "Write") => _stepName = stepName;

    protected override async Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        if (!TryGetChannel(context, out var channel, out var fail)) return fail!;

        await channel!.WriteAsync(Data, Priority, false, ct).ConfigureAwait(false);
        context.Log($"[Write] Device#{DeviceId}({channel.DeviceName}) {Data.Length}B 전송");
        return SequenceStepResult.Ok(this);
    }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  §3 NetRequestStep — 요청-응답 + 검증 스텝
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// 요청-응답 스텝 (응답 수신 + 검증).
/// </summary>
/// <remarks>
/// <b>직접 생성:</b>
/// <code>
/// var step = new NetRequestStep("상태 확인")
/// {
///     DeviceId          = 1,
///     Data              = [0x01, 0x03, 0x00, 0x64, 0x00, 0x01],
///     Timeout           = TimeSpan.FromMilliseconds(500),
///     MaxRetries        = 3,
///     ResponseValidator = r => r.IsOk &amp;&amp; r.Data![4] == 0x01
/// };
/// </code>
///
/// <b>응답을 다음 스텝에서 읽기:</b>
/// <code>
/// // RequestStep 실행 후 StepIndex 키로 컨텍스트에 자동 저장됨
/// var netResult = context.GetVariable&lt;NetResult&gt;($"response_{step.StepIndex}");
/// </code>
/// </remarks>
public sealed class NetRequestStep : NetSequenceStepBase
{
    private readonly string _stepName;
    public override string StepName => _stepName;

    /// <summary>전송할 요청 페이로드.</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>응답 대기 타임아웃. null=채널 기본값(RequestTimeout).</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>전송 우선순위. 기본값: Write(1).</summary>
    public NetPriority Priority { get; init; } = NetPriority.Write;

    /// <summary>
    /// 응답 데이터 검증 함수.
    /// <para>null=검증 없음 (IsOk 만 확인).</para>
    /// <code>ResponseValidator = r => r.IsOk &amp;&amp; r.Data![4] == 0x01</code>
    /// </summary>
    public Func<NetResult, bool>? ResponseValidator { get; init; }

    public NetRequestStep(string stepName = "Request") => _stepName = stepName;

    protected override async Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        if (!TryGetChannel(context, out var channel, out var fail)) return fail!;

        var netResult = await channel!
            .RequestAsync(Data, Timeout, ct)
            .ConfigureAwait(false);

        bool validated = netResult.IsOk &&
            (ResponseValidator is null || ResponseValidator(netResult));

        if (!validated)
        {
            string errMsg = netResult.IsError
                ? netResult.Error!.Message : "응답 검증 실패";
            context.LogError($"[Request] Device#{DeviceId} {errMsg}");
            return SequenceStepResult.Fail(this, errMsg, outputData: netResult);
        }

        context.Log(
            $"[Request] Device#{DeviceId}({channel.DeviceName}) " +
            $"응답 {netResult.Data!.Length}B ✔");

        // 응답 값 컨텍스트 저장 → 다음 스텝에서 GetVariable<NetResult>("response_{StepIndex}")
        context.SetVariable($"response_{StepIndex}", netResult);
        return SequenceStepResult.Ok(this, outputData: netResult);
    }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  §4 NetDelayStep — 순수 대기 스텝
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>순수 대기 스텝. 전송 없이 지정 시간만큼 대기합니다.</summary>
public sealed class NetDelayStep : NetSequenceStepBase
{
    private readonly string _stepName;
    public override string StepName => _stepName;

    public NetDelayStep(string stepName = "Delay") => _stepName = stepName;

    protected override Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
        => Task.FromResult(SequenceStepResult.Ok(this));
}