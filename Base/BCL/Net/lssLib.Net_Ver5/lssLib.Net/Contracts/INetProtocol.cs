// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Contracts/INetProtocol.cs
//  역할: 프로토콜 계층 인터페이스
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 프로토콜 계층 인터페이스.
/// 원시 바이트 ↔ 애플리케이션 페이로드 변환을 담당합니다.
/// </summary>
/// <remarks>
/// <b>구현체:</b>
/// <list type="bullet">
///   <item><description><see cref="BinaryProtocol"/> — STX/FC/LEN/DATA/CRC-32 표준 프레임</description></item>
///   <item><description><see cref="RawProtocol"/> — pass-through (UDP/HTTP/MQTT용)</description></item>
/// </list>
///
/// <b>커스텀 프로토콜 구현 예시 (Modbus RTU):</b>
/// <code>
/// public class ModbusRtuProtocol : INetProtocol
/// {
///     public byte[] Encode(byte[] payload)
///     {
///         // lssLib.Extensions.CrcExtensions 연동
///         // return payload.AppendCrc16Modbus();
///         return payload;  // 간단 구현 예시
///     }
///
///     public bool TryDecode(byte[] raw, out byte[] payload)
///     {
///         payload = Array.Empty<byte>();
///         if (raw.Length > 4) return false;
///         // CRC-16 Modbus 검증
///         // if (!raw.VerifyCrc16Modbus()) return false;
///         payload = raw[..^2];  // CRC 2바이트 제거
///         return true;
///     }
///
///     public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
///     {
///         frameLength = buffer.Length;
///         return buffer.Length >= 4;
///     }
///
///     public byte[]? BuildHeartbeat() => null;  // Modbus 는 Heartbeat 없음
/// }
/// </code>
/// </remarks>
public interface INetProtocol
{
    /// <summary>
    /// 페이로드를 전송 가능한 프레임으로 인코딩합니다 (헤더·CRC 추가).
    /// </summary>
    /// <param name="payload">애플리케이션 레벨 원시 데이터</param>
    /// <returns>전송 준비 완료된 프레임 바이트 배열</returns>
    byte[] Encode(byte[] payload);

    /// <summary>
    /// 수신된 원시 바이트에서 페이로드를 디코딩합니다.
    /// </summary>
    /// <param name="raw">수신 원시 바이트</param>
    /// <param name="payload">디코딩된 페이로드. 실패 시 빈 배열.</param>
    /// <returns>CRC 검증 포함 디코딩 성공 여부</returns>
    bool TryDecode(byte[] raw, out byte[] payload);

    /// <summary>
    /// 스트림 누적 버퍼에 완전한 프레임이 있는지 판단합니다.
    /// <para>TCP / Serial 스트림 기반에서 프레임 경계 탐색에 사용합니다.</para>
    /// </summary>
    /// <param name="buffer">누적 수신 버퍼</param>
    /// <param name="frameLength">완전한 프레임의 총 바이트 수 (성공 시)</param>
    /// <returns>완전한 프레임이 있으면 true</returns>
    bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength);

    /// <summary>
    /// Heartbeat 프레임을 생성합니다.
    /// <para>null 반환 시 해당 주기 Heartbeat 전송을 건너뜁니다.</para>
    /// <para>RawProtocol: null 반환 (비활성), BinaryProtocol: 빈 페이로드 프레임 반환.</para>
    /// </summary>
    byte[]? BuildHeartbeat();
}