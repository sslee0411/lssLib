namespace lssLib.Net.Base.Core;

/// <summary>
/// 내부 큐 패킷의 처리 종류.
/// </summary>
/// <remarks>
/// <para>이 열거형은 <c>internal</c> 전용입니다. 외부에서 직접 사용하지 않습니다.</para>
/// <para>
/// <see cref="NetPacket"/> 이 <see cref="NetChannelBase.DispatchPacketAsync"/> 에 도달하면
/// 이 모드에 따라 처리 방식이 분기됩니다.
/// </para>
/// </remarks>
internal enum PacketMode
{
    /// <summary>
    /// 단방향 Fire-and-forget 쓰기.
    /// <para>전송 후 응답을 기다리지 않습니다.</para>
    /// <para><see cref="NetChannelBase.WriteAsync"/> 에서 생성됩니다.</para>
    /// </summary>
    Write = 0,

    /// <summary>
    /// 요청 전송 후 응답 대기.
    /// <para><see cref="TaskCompletionSource{TResult}"/>(TCS) 를 통해 호출자에게 결과를 전달합니다.</para>
    /// <para><see cref="NetChannelBase.RequestAsync"/> 에서 생성됩니다.</para>
    /// </summary>
    Request = 1,

    /// <summary>
    /// 주기적 Read 요청.
    /// <para>전송 후 응답을 수신하여 수신 채널과 <c>DeviceFrameReceived</c> 이벤트로 전달합니다.</para>
    /// <para>내부 <c>PeriodicReadAsync</c> 루프에서 자동 생성됩니다.</para>
    /// </summary>
    PeriodicRead = 2,

    /// <summary>
    /// 재전송.
    /// <para>Write 실패 후 <see cref="NetPacket.ToRetry"/> 에 의해 생성됩니다.</para>
    /// <para>우선순위가 <see cref="NetPriority.Critical"/> 로 승격됩니다.</para>
    /// </summary>
    Retry = 3
}