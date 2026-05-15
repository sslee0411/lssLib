// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetState.cs
//  역할: 전송 계층 연결 상태 열거형
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 전송 계층 연결 상태.
/// </summary>
/// <remarks>
/// 상태 전이:
/// <code>
/// Disconnected → Connecting → Connected → Reconnecting → Connected
///                                                       → Error → Disposed
/// </code>
/// </remarks>
public enum NetState
{
    /// <summary>연결 끊김. 초기 상태 또는 명시적 해제 후. IsConnected=false.</summary>
    Disconnected = 0,

    /// <summary>연결 시도 중. 모든 통신 동작이 스킵됩니다.</summary>
    Connecting = 1,

    /// <summary>연결됨. IsConnected=true 인 유일한 상태.</summary>
    Connected = 2,

    /// <summary>재접속 대기 중. 지수 백오프 대기 구간.</summary>
    Reconnecting = 3,

    /// <summary>오류 상태. 재접속 한도 초과 또는 복구 불가. StopAsync 후 채널 재생성 필요.</summary>
    Error = 4,

    /// <summary>Dispose 완료. 이후 사용 시 ObjectDisposedException.</summary>
    Disposed = 5
}