// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · NetMode.cs
//  역할: 통신 형태 / 연결 상태 / 내부 패킷 모드 열거형
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 통신 형태.
/// <para>파생 채널 클래스는 반드시 하나의 Mode 를 선언해야 합니다.</para>
/// </summary>
public enum NetMode
{
    /// <summary>
    /// 형태 1 — 수동 수신 (Passive).
    /// 장치가 먼저 데이터를 보내오면 수신합니다.
    /// 내부 수신 Channel 로 프레임을 전달합니다.
    /// </summary>
    Passive = 0,

    /// <summary>
    /// 형태 2 — 요청-응답 (RequestResponse).
    /// 우리가 요청 프레임을 보내고 응답을 기다립니다.
    /// 주기적 Read 루프 + Write 우선 처리 방식으로 동작합니다.
    /// </summary>
    RequestResponse = 1
}

/// <summary>
/// 전송 계층 연결 상태.
/// </summary>
public enum NetState
{
    /// <summary>연결 끊김.</summary>
    Disconnected = 0,

    /// <summary>연결 시도 중.</summary>
    Connecting = 1,

    /// <summary>연결됨 — 정상 통신 가능.</summary>
    Connected = 2,

    /// <summary>재접속 시도 중.</summary>
    Reconnecting = 3,

    /// <summary>오류 상태 — ErrorOccurred 이벤트 참조.</summary>
    Error = 4,

    /// <summary>Dispose 완료.</summary>
    Disposed = 5
}

/// <summary>
/// 내부 큐 패킷의 처리 종류. (internal 전용)
/// </summary>
internal enum PacketMode
{
    /// <summary>단방향 쓰기.</summary>
    Write = 0,

    /// <summary>요청 후 응답 대기.</summary>
    Request = 1,

    /// <summary>주기적 Read 요청 (PeriodicReadAsync 에서 생성).</summary>
    PeriodicRead = 2,

    /// <summary>재전송 (Write 실패 시 Critical 로 재투입).</summary>
    Retry = 3
}