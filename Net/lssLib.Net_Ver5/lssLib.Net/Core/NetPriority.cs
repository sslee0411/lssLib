// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetPriority.cs
//  역할: 통신 큐 우선순위 열거형
//
//  Channel[4] 인덱스 = 우선순위 값 (값이 작을수록 먼저 처리)
//  Critical(0) > Write(1) > Read(2) > Low/Heartbeat(3)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 통신 패킷 처리 우선순위.
/// </summary>
/// <remarks>
/// <para><b>핵심 원칙: Write 는 Read 보다 항상 먼저 처리됩니다.</b></para>
/// <para>내부적으로 <c>Channel[4]</c> 인덱스와 1:1 매핑됩니다.</para>
///
/// <b>우선순위별 사용 시점:</b>
/// <list type="table">
///   <item><term>Critical(0)</term><description>Write 실패 재전송, 재접속 후 재투입, 비상 정지 명령</description></item>
///   <item><term>Write(1)</term><description>WriteAsync / RequestAsync 기본값 (외부 호출)</description></item>
///   <item><term>Read(2)</term><description>주기 Read 루프 내부 자동 생성 (외부 직접 사용 불필요)</description></item>
///   <item><term>Low(3)</term><description>Heartbeat, 진단 데이터 — 통신 공백 시에만 전송</description></item>
/// </list>
///
/// <b>사용 예시:</b>
/// <code>
/// // 일반 Write (기본값 — 생략 가능)
/// await channel.WriteAsync(frame, NetPriority.Write);
///
/// // 비상 정지 (최우선 처리)
/// await channel.WriteAsync(emergencyStop, NetPriority.Critical);
///
/// // 진단 데이터 (최저 우선순위)
/// await channel.WriteAsync(diagFrame, NetPriority.Low);
/// </code>
/// </remarks>
public enum NetPriority
{
    /// <summary>
    /// 긴급 (0) — Write 실패 재전송, 재접속 후 재투입, 에러 복구 명령.
    /// <para>큐에 다른 패킷이 있어도 즉시 선점합니다.</para>
    /// </summary>
    Critical = 0,

    /// <summary>
    /// 쓰기 (1) — 외부 WriteAsync / RequestAsync 기본 우선순위.
    /// <para>Read(2) 보다 항상 먼저 처리됩니다. WriteAsync 의 기본값이므로 대부분 생략 가능합니다.</para>
    /// </summary>
    Write = 1,

    /// <summary>
    /// 읽기 (2) — 주기적 Read 요청.
    /// <para>내부 PeriodicReadAsync 루프에서 자동 생성됩니다. 외부 직접 사용은 불필요합니다.</para>
    /// </summary>
    Read = 2,

    /// <summary>
    /// 낮음 (3) — Heartbeat, 진단, 통계 수집.
    /// <para>모든 Write / Read 패킷이 처리된 후 통신 공백 구간에만 전송됩니다.</para>
    /// </summary>
    Low = 3
}