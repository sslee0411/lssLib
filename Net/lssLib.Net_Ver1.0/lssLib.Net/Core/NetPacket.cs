// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · NetPacket.cs
//  역할: 내부 우선순위 큐 패킷 (외부 노출 없음)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 내부 처리 큐를 흐르는 패킷 단위.
/// <para>외부에서 직접 생성하지 않습니다. <see cref="NetChannelBase"/> 내부에서만 사용됩니다.</para>
/// </summary>
internal sealed class NetPacket
{
    #region §1 ─ 필드

    /// <summary>전송할 (인코딩 완료된) 바이트.</summary>
    public required byte[] Data { get; init; }

    /// <summary>처리 우선순위.</summary>
    public required NetPriority Priority { get; init; }

    /// <summary>패킷 처리 종류.</summary>
    public required PacketMode Mode { get; init; }

    /// <summary>
    /// 요청-응답 모드에서 응답 결과를 전달할 TCS.
    /// Fire-and-forget Write 인 경우 <c>null</c>.
    /// </summary>
    public TaskCompletionSource<NetResult>? Tcs { get; init; }

    /// <summary>취소 토큰.</summary>
    public CancellationToken Ct { get; init; }

    /// <summary>생성 시각 (큐 대기 시간 측정용).</summary>
    public DateTime CreatedAt { get; } = DateTime.Now;

    /// <summary>Write 실패 시 재시도 횟수 추적.</summary>
    public int RetryCount { get; set; }

    #endregion

    #region §2 ─ 팩토리

    /// <summary>Fire-and-forget Write 패킷을 생성합니다.</summary>
    internal static NetPacket CreateWrite(byte[] data, NetPriority priority, CancellationToken ct) => new()
    {
        Data = data,
        Priority = priority,
        Mode = PacketMode.Write,
        Ct = ct
    };

    /// <summary>요청-응답 패킷을 생성합니다.</summary>
    internal static NetPacket CreateRequest(byte[] data, TaskCompletionSource<NetResult> tcs, CancellationToken ct) => new()
    {
        Data = data,
        Priority = NetPriority.Write,
        Mode = PacketMode.Request,
        Tcs = tcs,
        Ct = ct
    };

    /// <summary>주기적 Read 패킷을 생성합니다.</summary>
    internal static NetPacket CreatePeriodicRead(byte[] data, CancellationToken ct) => new()
    {
        Data = data,
        Priority = NetPriority.Read,
        Mode = PacketMode.PeriodicRead,
        Ct = ct
    };

    /// <summary>재전송 패킷을 생성합니다 (기존 패킷의 Priority 를 Critical 로 승격).</summary>
    internal NetPacket ToRetry() => new()
    {
        Data = Data,
        Priority = NetPriority.Critical,
        Mode = PacketMode.Retry,
        Tcs = Tcs,
        Ct = Ct,
        RetryCount = RetryCount + 1
    };

    #endregion
}