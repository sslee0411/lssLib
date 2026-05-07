// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetMode.cs
//  역할: 통신 형태 열거형
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>통신 형태. 파생 채널은 반드시 하나를 선언해야 합니다.</summary>
/// <remarks>
/// <list type="bullet">
///   <item><description><b>Passive</b> — 장비가 먼저 보내옴. 예) 센서, 바코드, GPS.</description></item>
///   <item><description><b>RequestResponse</b> — 우리가 요청해야 응답. 예) Modbus, 인버터.</description></item>
/// </list>
/// </remarks>
public enum NetMode
{
    /// <summary>수동 수신. INetTransport.DataReceived 이벤트 기반 수집.</summary>
    Passive = 0,

    /// <summary>요청-응답. ReadCommands 를 PeriodicInterval 주기로 전송.</summary>
    RequestResponse = 1
}