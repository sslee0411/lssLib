// ══════════════════════════════════════════════════════════════════════
//  INetProtocol
// ══════════════════════════════════════════════════════════════════════

//  ┌─ 역할  ─--------------------─────────────────┐
//  │  INetProtocol   ── 바이트 ↔ 페이로드 변환                    │
//  │                    STX/LEN/CRC 프레임 조립·분해                │
//  └─────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════
namespace lssLib.Net.Base.Contracts;

/// <summary>
/// 프로토콜 계층 인터페이스.
/// </summary>
/// <remarks>
/// <para>
/// <b>책임 범위:</b>
/// 애플리케이션 페이로드 ↔ 전송 가능한 프레임 바이트 변환.
/// 물리적 연결 관리는 이 인터페이스의 책임이 아닙니다.
/// </para>
///
/// <b>구현 클래스 목록:</b>
/// <list type="table">
///   <listheader><term>클래스</term><description>용도</description></listheader>
///   <item><term><c>RawProtocol</c></term><description>인코딩 없음 (pass-through). 테스트, UDP, 외부 프레이밍 완성 환경.</description></item>
///   <item><term><c>BinaryProtocol</c></term><description>lssLib.Binary 기반 STX/FC/LEN/DATA/CRC-32 표준 프레임.</description></item>
///   <item><term><c>ModbusRtuProtocol</c></term><description>Modbus RTU 프레임 (예정).</description></item>
/// </list>
///
/// <b>lssLib.Binary 연동 예시 (BinaryProtocol.Encode 구현):</b>
/// <code>
/// // lssLib.Binary + lssLib.Extensions 참조 필요
/// public byte[] Encode(byte[] payload)
///     => BufferWriter.Create()
///         .WriteUInt8(0xAA)                        // STX
///         .WriteUInt8(0x01)                        // FC (기능 코드)
///         .WriteUInt16BE((ushort)payload.Length)   // LEN (빅엔디안)
///         .WriteRaw(payload)                       // DATA
///         .AppendCrc32()                           // CRC-32 (4B LE, 자동 추가)
///         .ToArray();
///
/// // TryDecode (검증 포함)
/// public bool TryDecode(byte[] raw, out byte[] payload)
/// {
///     payload = Array.Empty<byte>();
///     if (!raw.VerifyCrc32()) return false;       // CRC 검증
///     payload = raw[4..^4];                       // 헤더/CRC 제거
///     return true;
/// }
/// </code>
///
/// <b>커스텀 프로토콜 구현 예시:</b>
/// <code>
/// // NMEA GPS 프로토콜: $GPRMC,...*HH\r\n 형식
/// public class NmeaProtocol : INetProtocol
/// {
///     // NMEA 는 ASCII 텍스트이므로 Encode 는 단순
///     public byte[] Encode(byte[] payload) => payload;
///
///     // $로 시작하고 \r\n 으로 끝나는 프레임 감지
///     public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
///     {
///         frameLength = 0;
///         if (buffer[0] != '$') return false;
///         int crlf = buffer.IndexOf("\r\n"u8);
///         if (crlf < 0) return false;
///         frameLength = crlf + 2;
///         return true;
///     }
///
///     public byte[]? BuildHeartbeat() => null;  // NMEA 는 Heartbeat 없음
/// }
/// </code>
/// </remarks>
public interface INetProtocol
{
    /// <summary>
    /// 애플리케이션 페이로드를 전송 가능한 프레임으로 인코딩합니다.
    /// </summary>
    /// <param name="payload">
    /// 원본 페이로드.
    /// lssLib.Binary.BufferWriter 로 생성한 바이트 배열을 그대로 전달합니다.
    /// </param>
    /// <returns>
    /// 헤더(STX, FC, LEN)와 CRC 가 추가된 완전한 전송 프레임.
    /// <see cref="INetTransport.WriteAsync"/> 에 직접 전달할 수 있는 상태입니다.
    /// </returns>
    byte[] Encode(byte[] payload);

    /// <summary>
    /// 수신된 원시 바이트에서 애플리케이션 페이로드를 디코딩합니다.
    /// </summary>
    /// <param name="raw">
    /// 수신된 원시 바이트.
    /// <see cref="INetTransport.DataReceived"/> 이벤트 또는 <c>ReadAsync</c> 결과입니다.
    /// </param>
    /// <param name="payload">
    /// 디코딩된 페이로드 (헤더·CRC 제거됨).
    /// lssLib.Binary.BufferParser 로 파싱할 수 있는 상태입니다.
    /// 실패 시 빈 배열.
    /// </param>
    /// <returns>
    /// CRC 검증 포함 디코딩 성공 여부.
    /// <c>false</c> 이면 <paramref name="payload"/> 를 사용하지 마세요.
    /// </returns>
    bool TryDecode(byte[] raw, out byte[] payload);

    /// <summary>
    /// 수신 버퍼에 완전한 프레임이 있는지 판단합니다.
    /// </summary>
    /// <param name="buffer">
    /// 누적 수신 버퍼.
    /// TCP / Serial 처럼 스트림 기반에서 여러 번 읽어 누적된 바이트입니다.
    /// </param>
    /// <param name="frameLength">
    /// 완전한 프레임의 총 길이 (헤더 + 페이로드 + CRC).
    /// 미완성이면 0.
    /// </param>
    /// <returns>
    /// 버퍼에 완전한 프레임이 있으면 <c>true</c>.
    /// <c>true</c> 이면 <paramref name="frameLength"/> 만큼 잘라 <see cref="TryDecode"/> 에 전달하세요.
    /// </returns>
    /// <remarks>
    /// 스트림 기반 Transport(TCP, Serial)에서 프레임 경계를 찾는 데 사용됩니다.
    /// UDP 처럼 데이터그램 기반이면 항상 <c>true</c> 를 반환해도 됩니다.
    /// </remarks>
    bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength);

    /// <summary>
    /// Heartbeat 프레임을 생성합니다.
    /// </summary>
    /// <returns>
    /// 장비에게 전송할 Heartbeat 프레임.
    /// <c>null</c> 반환 시 Heartbeat 전송을 건너뜁니다.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="Config.NetDeviceConfigBase.HeartbeatInterval"/> 이 설정된 경우
    /// 주기적으로 이 메서드를 호출하여 생성된 프레임을 <see cref="NetPriority.Low"/> 로 전송합니다.
    /// </para>
    /// <para>
    /// Heartbeat 가 필요 없는 프로토콜은 <c>null</c> 을 반환합니다.
    /// BinaryProtocol 은 페이로드 없는 빈 프레임을 Heartbeat 로 사용합니다.
    /// </para>
    /// </remarks>
    byte[]? BuildHeartbeat();
}