// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Interface/ISequenceConfig.cs
//  역할: 커맨드 순차 실행 인터페이스 — Config Lego 브릭 3/4
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 커맨드 순차 실행 인터페이스.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader><term>값</term><description>적합 환경</description></listheader>
///   <item><term><c>true</c></term><description>RS-485/Modbus — 이전 응답 후 다음 전송. 버스 충돌 방지.</description></item>
///   <item><term><c>false</c></term><description>TCP/UDP — 동시 다중 요청 허용. 처리량 향상.</description></item>
/// </list>
/// </remarks>
public interface ISequenceConfig
{
    /// <summary>커맨드 순차 실행 여부. true=하나씩, false=병렬 투입.</summary>
    bool IsSequential { get; }
}