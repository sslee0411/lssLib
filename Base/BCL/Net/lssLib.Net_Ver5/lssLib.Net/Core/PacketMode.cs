// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/PacketMode.cs
//  역할: 내부 큐 패킷 처리 종류 열거형 (internal)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>내부 큐 패킷 처리 종류. NetDispatchPipeline 내부 분기 전용.</summary>
internal enum PacketMode
{
    /// <summary>단방향 쓰기. 응답 대기 없음. TCS=null.</summary>
    Write = 0,

    /// <summary>요청-응답. TCS 를 통해 RequestAsync 호출자에게 결과 전달.</summary>
    Request = 1,

    /// <summary>주기적 Read. 수신 채널 + DeviceFrameReceived 이벤트로 전달.</summary>
    PeriodicRead = 2,

    /// <summary>재전송. NetPacket.ToRetry() 생성. Priority=Critical 로 자동 승격.</summary>
    Retry = 3
}