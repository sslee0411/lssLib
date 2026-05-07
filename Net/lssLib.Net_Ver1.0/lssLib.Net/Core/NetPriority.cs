// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · NetPriority.cs
//  역할: 통신 큐 우선순위 열거형 (Write > Read 원칙)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 통신 패킷 처리 우선순위.
/// 값이 작을수록 먼저 처리됩니다.
/// </summary>
/// <remarks>
/// 우선순위 원칙:
/// <list type="bullet">
///   <item><description>Write 요청은 주기적 Read 요청보다 항상 먼저 처리됩니다.</description></item>
///   <item><description>Heartbeat 는 낮은 우선순위로, 통신 공백 시에만 전송됩니다.</description></item>
///   <item><description>Retry(재전송)는 Critical 우선순위로 즉시 처리됩니다.</description></item>
/// </list>
/// </remarks>
public enum NetPriority
{
    /// <summary>긴급 — 재전송, 에러 복구. 큐를 즉시 선점합니다.</summary>
    Critical = 0,

    /// <summary>쓰기 — 외부에서 요청된 Write. Read 보다 항상 우선합니다.</summary>
    Write = 1,

    /// <summary>읽기 — 요청-응답 모드의 주기적 Read 요청.</summary>
    Read = 2,

    /// <summary>낮음 — Heartbeat, 진단, 통계 수집.</summary>
    Low = 3
}

