// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetPacket.cs
//  역할: 내부 우선순위 큐 패킷 (NetChannelBase 전용)
//
//  ┌─ 패킷 생명 주기 ─── ──────────────────┐
//  │                                                             │
//  │  [생성] CreateWrite / CreateRequest / CreatePeriodicRead    │
//  │      │                                                     │
//  │      ▼                                                     │
//  │  [진입] Channel<NetPacket> 에 WriteAsync 로 투입            │
//  │      │                                                     │
//  │      ▼                                                     │
//  │  [재정렬] PriorityQueue 에서 Priority 기준으로 정렬         │
//  │      │                                                     │
//  │      ▼                                                     │
//  │  [처리] DispatchPacketAsync                                 │
//  │      ├─ 성공 → 결과 전달 (TCS.SetResult / 수신 채널)     │
//  │      └─ 실패 → ToRetry() 로 Critical 승격 후 재투입      │
//  └──────────-----------───────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net.Base.Core;

/// <summary>
/// 내부 처리 큐를 흐르는 패킷 단위.
/// 하나의 메세지(패킷) 단위
/// </summary>
/// <remarks>
/// <para>
/// <b>외부에서 직접 생성하지 않습니다.</b>
/// <see cref="NetChannelBase"/> 내부에서만 팩토리 메서드로 생성합니다.
/// </para>
///
/// <para>
/// <b>패킷 모드별 처리 흐름:</b>
/// </para>
/// <list type="table">
///   <listheader><term>PacketMode</term><description>처리 방식</description></listheader>
///   <item>
///     <term>Write</term>
///     <description>
///       Transport.WriteAsync 만 호출합니다. 응답을 기다리지 않습니다.
///       TCS 는 null 입니다.
///     </description>
///   </item>
///   <item>
///     <term>Request</term>
///     <description>
///       WriteAsync 후 ReadAsync 로 응답을 기다립니다.
///       TCS 를 통해 호출자(<c>RequestAsync</c>)에게 결과를 전달합니다.
///     </description>
///   </item>
///   <item>
///     <term>PeriodicRead</term>
///     <description>
///       WriteAsync 후 ReadAsync 로 응답을 수신합니다.
///       TCS 는 null 이며, 수신 채널과 <c>DeviceFrameReceived</c> 이벤트로 전달합니다.
///     </description>
///   </item>
///   <item>
///     <term>Retry</term>
///     <description>
///       <see cref="ToRetry"/> 에 의해 생성됩니다.
///       Write 와 동일하게 처리되지만 Priority 가 Critical(0) 입니다.
///     </description>
///   </item>
/// </list>
/// </remarks>
internal sealed class NetPacket
{
    #region §1 ─ 프로퍼티

    /// <summary>
    /// 전송할 프로토콜 인코딩 완료 바이트.
    /// <para><see cref="INetProtocol.Encode"/> 를 통해 헤더·CRC 가 이미 추가된 상태입니다.</para>
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// 처리 우선순위.
    /// <para><see cref="PriorityQueue{TElement,TPriority}"/> 에서
    /// 이 값이 작을수록 먼저 꺼냅니다.</para>
    /// </summary>
    public required NetPriority Priority { get; init; }

    /// <summary>
    /// 패킷 처리 종류.
    /// <para><c>DispatchPacketAsync</c> 에서 이 값으로 분기합니다.</para>
    /// </summary>
    public required PacketMode Mode { get; init; }

    /// <summary>
    /// 요청-응답 모드에서 응답 결과를 호출자에게 전달할 TCS.
    /// <para>
    /// <see cref="PacketMode.Request"/> 일 때만 값이 있습니다.
    /// 다른 모드에서는 <c>null</c> 입니다.
    /// </para>
    /// <para>
    /// 처리 완료 시 <c>TrySetResult</c> 를 통해
    /// <c>RequestAsync</c> 의 <c>await</c> 를 깨웁니다.
    /// </para>
    /// </summary>
    public TaskCompletionSource<NetResult>? Tcs { get; init; }

    /// <summary>
    /// 취소 토큰.
    /// <para>호출자의 CancellationToken 을 그대로 전달받습니다.</para>
    /// </summary>
    public CancellationToken Ct { get; init; }

    /// <summary>
    /// 패킷 생성 시각.
    /// <para>큐 대기 시간 측정 및 디버깅에 활용합니다.</para>
    /// <para>예: <c>(DateTime.Now - packet.CreatedAt).TotalMs</c> 로 처리 지연 측정.</para>
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.Now;

    /// <summary>
    /// 재전송 누적 횟수.
    /// <para>
    /// <see cref="ToRetry"/> 가 호출될 때마다 1씩 증가합니다.
    /// <see cref="Config.NetDeviceConfigBase.MaxRetries"/> 와 비교하여
    /// 재전송 한도를 판단합니다.
    /// </para>
    /// </summary>
    public int RetryCount { get; set; }

    #endregion

    #region §2 ─ 팩토리 메서드

    /// <summary>
    /// Fire-and-forget Write 패킷을 생성합니다.
    /// </summary>
    /// <param name="data">프로토콜 인코딩 완료 바이트 (<see cref="INetProtocol.Encode"/> 결과)</param>
    /// <param name="priority">처리 우선순위 (기본: <see cref="NetPriority.Write"/>)</param>
    /// <param name="ct">취소 토큰</param>
    internal static NetPacket CreateWrite(byte[] data, NetPriority priority, CancellationToken ct)
        => new() 
        { 
            Data = data, 
            Priority = priority, 
            Mode = PacketMode.Write, 
            Ct = ct 
        };

    /// <summary>
    /// 요청-응답 패킷을 생성합니다.
    /// </summary>
    /// <param name="data">프로토콜 인코딩 완료 바이트</param>
    /// <param name="tcs">결과를 호출자에게 전달할 TCS (<c>RequestAsync</c> 에서 생성)</param>
    /// <param name="ct">취소 토큰</param>
    /// <remarks>Priority 는 항상 <see cref="NetPriority.Write"/> 입니다.</remarks>
    internal static NetPacket CreateRequest(byte[] data,
        TaskCompletionSource<NetResult> tcs, CancellationToken ct)
        => new()
        {
            Data = data,
            Priority = NetPriority.Write,   // Request 는 Write 와 동일 우선순위
            Mode = PacketMode.Request,
            Tcs = tcs,
            Ct = ct
        };

    /// <summary>
    /// 주기적 Read 요청 패킷을 생성합니다.
    /// </summary>
    /// <param name="data">프로토콜 인코딩 완료 바이트 (ReadCommands 목록의 항목)</param>
    /// <param name="ct">취소 토큰</param>
    /// <remarks>
    /// Priority 는 항상 <see cref="NetPriority.Read"/> 입니다.
    /// Write 가 없을 때만 처리됩니다.
    /// </remarks>
    internal static NetPacket CreatePeriodicRead(byte[] data, CancellationToken ct)
        => new()
        {
            Data = data,
            Priority = NetPriority.Read,    // Write 보다 낮은 우선순위
            Mode = PacketMode.PeriodicRead,
            Ct = ct
        };

    /// <summary>
    /// 재전송 패킷을 생성합니다.
    /// </summary>
    /// <returns>
    /// 원본 패킷의 데이터를 유지하면서
    /// Priority 를 <see cref="NetPriority.Critical"/> 로 승격하고
    /// RetryCount 를 1 증가시킨 새 패킷.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="NetChannelBase.HandleConnectionErrorAsync"/> 에서
    /// Write 실패 시 호출됩니다.
    /// </para>
    /// <para>
    /// Critical 승격으로 큐에 쌓인 다른 모든 패킷보다 먼저 처리됩니다.
    /// </para>
    /// </remarks>
    internal NetPacket ToRetry() => new()
    {
        Data = Data,
        Priority = NetPriority.Critical,  // 최우선으로 승격
        Mode = PacketMode.Retry,
        Tcs = Tcs,                        // TCS 가 있으면 그대로 유지
        Ct = Ct,
        RetryCount = RetryCount + 1       // 재전송 횟수 누적
    };

    #endregion
}