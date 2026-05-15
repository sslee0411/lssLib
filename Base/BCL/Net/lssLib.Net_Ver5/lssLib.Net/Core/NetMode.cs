// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Core/NetMode.cs
// ══════════════════════════════════════════════════════════════════════
namespace lssLib.Net;

/// <summary>통신 형태. 파생 채널은 반드시 하나를 선언해야 합니다.</summary>
public enum NetMode
{
    /// <summary>수동 수신. INetTransport.DataReceived 이벤트 기반. 예) 센서, 바코드, MQTT.</summary>
    Passive = 0,
    /// <summary>요청-응답. ReadCommands 를 PeriodicInterval 주기로 전송. 예) Modbus, 인버터.</summary>
    RequestResponse = 1
}