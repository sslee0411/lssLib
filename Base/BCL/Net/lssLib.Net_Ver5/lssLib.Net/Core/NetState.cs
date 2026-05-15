// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetState.cs
// ══════════════════════════════════════════════════════════════════════
namespace lssLib.Net;

/// <summary>전송 계층 연결 상태.</summary>
public enum NetState
{
    /// <summary>연결 끊김. 초기 상태.</summary>
    Disconnected = 0,
    /// <summary>연결 시도 중.</summary>
    Connecting = 1,
    /// <summary>연결됨. IsConnected=true 인 유일한 상태.</summary>
    Connected = 2,
    /// <summary>재접속 대기 중. 지수 백오프 대기 구간.</summary>
    Reconnecting = 3,
    /// <summary>오류 상태. 재접속 한도 초과 또는 복구 불가.</summary>
    Error = 4,
    /// <summary>Dispose 완료.</summary>
    Disposed = 5
}