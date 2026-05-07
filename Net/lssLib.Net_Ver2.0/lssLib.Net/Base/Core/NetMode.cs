// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetMode.cs
//  역할: 통신 형태 / 연결 상태 / 내부 패킷 처리 종류 열거형
//
//  ┌─ NetMode 선택 기준 ──────────────────────┐
//  │                                                                 │
//  │  장비가 먼저 데이터를 보내오는가?  → Passive                   │
//  │  우리가 먼저 요청해야 데이터가 오는가? → RequestResponse       │
//  │                                                                 │
//  │  Passive 예시        : 온도 센서, 바코드 리더, GPS 수신기       │
//  │  RequestResponse 예시: Modbus PLC, 인버터, 계측기               │
//  -─────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net.Base.Core;

/// <summary>
/// 통신 형태.
/// </summary>
/// <remarks>
/// <para>파생 채널 클래스(<see cref="NetChannelBase"/>)는 반드시 하나의 Mode 를 선언해야 합니다.</para>
///
/// <b>형태 선택 기준:</b>
/// <list type="table">
///   <listheader><term>형태</term><description>장비 특성</description></listheader>
///   <item>
///     <term><b>Passive</b></term>
///     <description>
///       장비가 스스로 데이터를 전송합니다. 우리는 수신만 합니다.
///       내부적으로 <see cref="INetTransport.DataReceived"/> 이벤트를 통해 데이터를 수집합니다.
///       예: 온도 센서(주기 전송), 바코드 리더(스캔 시 전송), GPS 수신기.
///     </description>
///   </item>
///   <item>
///     <term><b>RequestResponse</b></term>
///     <description>
///       우리가 요청 프레임을 보내야 장비가 응답합니다.
///       <see cref="Config.NetDeviceConfigBase.ReadCommands"/> 목록을 주기적으로 전송하고,
///       <see cref="Config.NetDeviceConfigBase.WriteCommands"/> 또는 외부 <c>WriteAsync</c> 는
///       Read 보다 높은 우선순위로 처리됩니다.
///       예: Modbus PLC, 인버터, 디지털 계측기.
///     </description>
///   </item>
/// </list>
///
/// <b>두 형태 모두 공통:</b>
/// <list type="bullet">
///   <item><description><c>WriteAsync</c> — 단방향 전송 가능</description></item>
///   <item><description><c>RequestAsync</c> — 단발 요청-응답 가능</description></item>
///   <item><description>Heartbeat — Low 우선순위로 전송 가능</description></item>
///   <item><description>연결 상태 가드 — <c>IsConnected</c> 가 false 이면 모든 통신 스킵</description></item>
/// </list>
/// </remarks>
public enum NetMode
{
    /// <summary>
    /// 형태 1 — 수동 수신 (Passive).
    /// <para>장비가 먼저 데이터를 보내오면 수신합니다.</para>
    /// <para>
    /// 동작: <see cref="INetTransport.DataReceived"/> 이벤트 → <see cref="INetProtocol.TryDecode"/>
    /// → 수신 채널(<c>ReadAllAsync</c>) 또는 <c>DeviceFrameReceived</c> 이벤트로 상위 계층 전달.
    /// </para>
    /// <para>사용 예: 온도 센서, 바코드 리더, 무선 수신기, GPS, RFID.</para>
    /// </summary>
    Passive = 0,

    /// <summary>
    /// 형태 2 — 요청-응답 (RequestResponse).
    /// <para>우리가 요청 프레임을 보내고 응답을 기다립니다.</para>
    /// <para>
    /// 동작: <c>PeriodicReadAsync</c> 가 <see cref="Config.NetDeviceConfigBase.ReadCommands"/> 를
    /// <see cref="Config.NetDeviceConfigBase.PeriodicInterval"/> 주기로 순차/병렬 전송합니다.
    /// 외부 <c>WriteAsync</c> 는 <see cref="NetPriority.Write"/> 로 Read 보다 항상 먼저 처리됩니다.
    /// </para>
    /// <para>사용 예: Modbus PLC, 인버터, 계측기, 온도 조절기.</para>
    /// </summary>
    RequestResponse = 1
}
