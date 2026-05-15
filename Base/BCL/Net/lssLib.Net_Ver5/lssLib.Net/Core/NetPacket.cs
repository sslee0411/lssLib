// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetPacket.cs
//  역할: 내부 우선순위 큐 패킷 (NetChannelBase 전용, internal)
//
//  패킷 생명 주기:
//    CreateWrite / CreateRequest / CreatePeriodicRead
//      → Channel<NetPacket> 에 WriteAsync 로 투입
//        → ProcessLoopAsync 에서 Priority 순 TryRead
//          → DispatchAsync 에서 실제 전송 처리
//            → 실패 시 ToRetry() → Critical 승격 재투입
// ══════════════════════════════════════════════════════════════════════
//┌─────────────────────────────────────┐
//│  외부 호출                → NetPacket 종류     → 내부 동작             │
//├─────────────────────────────────────┤
//│  WriteAsync(data)         → Write             → 전송만 (끝)            │
//│  WriteAsync(expectRes=T)  → Write+응답        → 전송 + 응답 수신       │
//│  RequestAsync(data)       → Request           → 전송 + 응답 → 반환    │
//│  PeriodicInterval 루프    → PeriodicRead      → 전송 + 응답 → 이벤트  │
//│  HeartbeatInterval 루프   → Heartbeat(=Write) → 전송만 (끝)            │
//│  Write 실패 재시도        → Retry(=Critical)  → 전송만 (최우선)        │
//└─────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════
//┌─────────────────────────────────────┐
//│ ① Write — 보내고 끝                                                     │
//├─────────────────────────────────────┤
//│// 외부 호출                                                              │
//│using lssLib.Net;                                                         │
//│                                                                          │
//│await channel.WriteAsync(setpointFrame);                                  │
//│                                                                          │
//│// 내부에서 생성되는 NetPacket                                            │
//│NetPacket.CreateWrite(data, NetPriority.Write, ct)                        │
//│// → Transport.WriteAsync(data)  ←  이게 전부, 응답 없음                │
//│// → 완료                                                                │
//├─────────────────────────────────────┤
//│사용 예: Modbus FC06 쓰기, 비상 정지 명령, 센서 설정값 전달               │
//└─────────────────────────────────────┘
//┌─────────────────────────────────────┐
//│② Request — 보내고 받아서 반환                                           │
//├─────────────────────────────────────┤
//│// 외부 호출                                                              │
//│using lssLib.Net;                                                         │
//│                                                                          │
//│NetResult r = await channel.RequestAsync(queryFrame, timeout: 500ms);     │
//│                                                                          │
//│ 내부에서 생성되는 NetPacket                                              │
//│NetPacket.CreateRequest(data, tcs, ct)                                    │
//│// → Transport.WriteAsync(data)                                          │
//│// → Transport.ReadAsync()        ← 응답 대기                           │
//│// → tcs.SetResult(NetResult.Ok(decoded))   ← await 깨움                │
//│// → RequestAsync 반환 (호출자에게 결과 직접 전달)                       │
//├─────────────────────────────────────┤
//│사용 예: 단발 Modbus 읽기, HTTP 요청 후 응답 확인, 장비 상태 일회성 조회  │
//└─────────────────────────────────────┘
//┌─────────────────────────────────────┐
//│ ③ PeriodicRead — 보내고 받아서 이벤트                                   │
//├─────────────────────────────────────┤
//│// 외부에서 설정만 하면 내부에서 자동 생성                                │
//│using lssLib.Net;                                                         │
//│using System.Text;                                                        │
//│                                                                          │
//│cfg.AddReadCommand(modbusReadFrame);                                      │
//│cfg.PeriodicInterval = TimeSpan.FromMilliseconds(100);                    │
//│                                                                          │
//│// NetScheduler가 100ms마다 자동으로 생성                                 │
//│NetPacket.CreatePeriodicRead(encoded, ct)                                 │
//│// → Transport.WriteAsync(READ_CMD)                                      │
//│// → Transport.ReadAsync()         ← 응답 대기                          │
//│// → DeviceFrameReceived 이벤트   ← 호출자에게 직접 반환 X              │
//│//                                    이벤트로만 통보                     │
//│Request 와 차이점: Request는 await로 직접 받고, PeriodicRead는 이벤트로만 통보
//├─────────────────────────────────────┤
//│사용 예: 100ms마다 Modbus 레지스터 읽기, 주기적 센서 폴링                 │
//└─────────────────────────────────────┘
//┌─────────────────────────────────────┐
//│ ④ Retry — 실패한 Write 재시도 (자동)                                    │
//├─────────────────────────────────────┤
//│// 사용자 코드에서 직접 생성하지 않음                                     │
//│// DispatchAsync 에서 Write 실패 시 자동 생성                             │
//│                                                                          │
//│// 기존 Write 패킷이 실패하면                                             │
//│existingPacket.ToRetry()                                                  │
//│// → Priority = Critical(0)  ← 최우선으로 승격                          │
//│// → RetryCount++                                                        │
//│// → 큐 맨 앞으로 재투입                                                 │
//└─────────────────────────────────────┘
//┌─────────────────────────────────────┐
//│요약                                                                      │
//├─────────────────────────────────────┤
//│Write        → "이 데이터 보내, 결과는 필요 없어"                        │
//│Request      → "이 데이터 보내고, 응답 줘 (await로 직접 받겠어)"         │
//│PeriodicRead → "이 명령 주기적으로 보내고, 응답 오면 이벤트 발생시켜줘"  │
//│Retry        → "아까 실패한 Write, 최우선으로 다시 보내줘 (자동)"        │
//└─────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════
namespace lssLib.Net;

/// <summary>
/// 내부 처리 큐를 흐르는 패킷 단위.
/// </summary>
/// <remarks>
/// <b>외부에서 직접 생성하지 않습니다.</b>
/// NetChannelBase 내부에서만 팩토리 메서드로 생성합니다.
/// </remarks>
internal sealed class NetPacket
{
    #region §1 ─ 프로퍼티

    /// <summary>프로토콜 인코딩 완료 바이트 (헤더·CRC 포함).</summary>
    public required byte[] Data { get; init; }

    /// <summary>처리 우선순위. 값이 작을수록 먼저 처리됩니다.</summary>
    public required NetPriority Priority { get; init; }

    /// <summary>패킷 처리 종류. DispatchAsync 에서 이 값으로 분기합니다.</summary>
    public required PacketMode Mode { get; init; }

    /// <summary>
    /// 전송 후 응답 수신 여부.
    /// true: WriteAsync 후 ReadAsync → DeviceFrameReceived 이벤트.
    /// false: WriteAsync 만 수행 (기본값).
    /// </summary>
    public bool ExpectResponse { get; init; } = false;

    /// <summary>요청-응답 모드에서 결과를 호출자에게 전달할 TCS. Request 패킷에만 존재.</summary>
    public TaskCompletionSource<NetResult>? Tcs { get; init; }

    /// <summary>취소 토큰.</summary>
    public CancellationToken Ct { get; init; }

    /// <summary>패킷 생성 시각 (큐 대기 시간 측정용).</summary>
    public DateTime CreatedAt { get; } = DateTime.Now;

    /// <summary>재전송 누적 횟수. ToRetry() 호출마다 1씩 증가.</summary>
    public int RetryCount { get; set; }

    #endregion

    #region §2 ─ 팩토리 메서드

    /// <summary>Fire-and-forget Write 패킷 생성.</summary>
    internal static NetPacket CreateWrite(byte[] data, NetPriority priority,
        CancellationToken ct, bool expectResponse = false)
        => new()
        {
            Data = data,
            Priority = priority,
            Mode = PacketMode.Write,
            Ct = ct,
            ExpectResponse = expectResponse
        };

    /// <summary>요청-응답 패킷 생성. Priority = Write(1).</summary>
    internal static NetPacket CreateRequest(byte[] data,
        TaskCompletionSource<NetResult> tcs, CancellationToken ct)
        => new()
        {
            Data = data,
            Priority = NetPriority.Write,
            Mode = PacketMode.Request,
            Tcs = tcs,
            Ct = ct
        };

    /// <summary>주기적 Read 요청 패킷 생성. Priority = Read(2).</summary>
    internal static NetPacket CreatePeriodicRead(byte[] data, CancellationToken ct)
        => new()
        {
            Data = data,
            Priority = NetPriority.Read,
            Mode = PacketMode.PeriodicRead,
            Ct = ct
        };

    /// <summary>Heartbeat 패킷 생성. Priority = Low(3).</summary>
    internal static NetPacket CreateHeartbeat(byte[] data, CancellationToken ct,
        bool expectAck = false)
        => new()
        {
            Data = data,
            Priority = NetPriority.Low,
            Mode = PacketMode.Write,
            Ct = ct,
            ExpectResponse = expectAck
        };

    /// <summary>
    /// 재전송 패킷 생성.
    /// Priority = Critical(0) 승격, RetryCount += 1.
    /// </summary>
    internal NetPacket ToRetry() => new()
    {
        Data = Data,
        Priority = NetPriority.Critical,
        Mode = PacketMode.Retry,
        Tcs = Tcs,
        Ct = Ct,
        ExpectResponse = ExpectResponse,
        RetryCount = RetryCount + 1
    };

    #endregion
}