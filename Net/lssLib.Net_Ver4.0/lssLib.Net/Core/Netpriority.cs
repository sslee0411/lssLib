// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetPriority.cs
//  역할: 통신 큐 우선순위 열거형
//
//  ┌─ 우선순위 결정 원칙 ──────────────────────┐
//  │                                                                  │
//  │  Critical(0) ─── 재전송·에러복구 ── 큐 즉시 선점           │
//  │  Write(1)    ─── 외부 명령        ── Read 보다 항상 먼저    │
//  │  Read(2)     ─── 주기 폴링        ── Write 없을 때만 처리   │
//  │  Low(3)      ─── Heartbeat        ── 통신 공백에만 전송     │
//  │                                                                  │
//  │  PriorityQueue<NetPacket, int> 에서 값이 작을수록 먼저 처리      │
//  └─────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 통신 패킷 처리 우선순위.
/// </summary>
/// <remarks>
/// <para>
/// <b>핵심 원칙: Write 는 Read 보다 항상 먼저 처리됩니다.</b>
/// </para>
/// <para>
/// 내부적으로 <see cref="System.Collections.Generic.PriorityQueue{TElement,TPriority}"/> 를
/// 사용하며, 값이 작을수록 먼저 꺼내집니다.
/// </para>
///
/// <b>각 우선순위별 사용 시점:</b>
/// <list type="table">
///   <listheader><term>우선순위</term><description>사용 시점</description></listheader>
///   <item>
///     <term><b>Critical(0)</b></term>
///     <description>
///       Write 전송 실패 후 재전송 패킷에 자동 적용됩니다.
///       <see cref="NetPacket.ToRetry"/> 호출 시 자동 승격됩니다.
///       재접속 후 Write 재투입 시에도 Critical 을 사용합니다.
///     </description>
///   </item>
///   <item>
///     <term><b>Write(1)</b></term>
///     <description>
///       외부에서 <c>channel.WriteAsync()</c> 또는 <c>channel.RequestAsync()</c> 를
///       호출할 때 기본으로 사용됩니다.
///       주기 Read 요청이 큐에 쌓여 있어도 Write 가 항상 먼저 처리됩니다.
///     </description>
///   </item>
///   <item>
///     <term><b>Read(2)</b></term>
///     <description>
///       내부 주기 Read 루프(<c>PeriodicReadAsync</c>)에서 자동 생성됩니다.
///       외부에서 직접 사용할 필요는 없습니다.
///     </description>
///   </item>
///   <item>
///     <term><b>Low(3)</b></term>
///     <description>
///       Heartbeat 전송에 사용됩니다.
///       Write / Read 가 없는 통신 공백 구간에만 전송되어 장비 과부하를 방지합니다.
///     </description>
///   </item>
/// </list>
///
/// <b>사용 예시:</b>
/// <code>
/// // 일반 Write (기본값, 생략 가능)
/// await channel.WriteAsync(frame, NetPriority.Write);
///
/// // 긴급 Write (최우선 처리 — 재전송 외 직접 사용 가능)
/// await channel.WriteAsync(emergencyFrame, NetPriority.Critical);
///
/// // 낮은 우선순위 진단 데이터
/// await channel.WriteAsync(diagFrame, NetPriority.Low);
/// </code>
/// </remarks>
public enum NetPriority
{
    /// <summary>
    /// 긴급 (0) — Write 실패 재전송, 재접속 후 Write 재투입, 에러 복구 명령.
    /// <para>큐에 다른 패킷이 있어도 즉시 선점합니다.</para>
    /// <para><see cref="NetPacket.ToRetry"/> 에 의해 자동 승격됩니다. 외부에서도 직접 지정 가능합니다.</para>
    /// </summary>
    Critical = 0,

    /// <summary>
    /// 쓰기 (1) — 외부 WriteAsync / RequestAsync 기본 우선순위.
    /// <para>Read(2) 보다 항상 먼저 처리됩니다.</para>
    /// <para><c>WriteAsync</c> 의 기본값이므로 대부분의 경우 명시 불필요합니다.</para>
    /// </summary>
    Write = 1,

    /// <summary>
    /// 읽기 (2) — 주기적 Read 요청.
    /// <para>내부 <c>PeriodicReadAsync</c> 루프에서 자동 생성됩니다.</para>
    /// <para>Write 가 없을 때만 처리됩니다. 외부에서 직접 사용할 일은 없습니다.</para>
    /// </summary>
    Read = 2,

    /// <summary>
    /// 낮음 (3) — Heartbeat, 진단, 통계 수집.
    /// <para>모든 Write / Read 패킷이 처리된 후 통신 공백 구간에만 전송됩니다.</para>
    /// <para>장비 처리 부하를 최소화하는 용도로 사용합니다.</para>
    /// </summary>
    Low = 3
}