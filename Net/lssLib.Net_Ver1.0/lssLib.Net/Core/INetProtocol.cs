// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · INetProtocol.cs
//  역할: 프로토콜 계층 추상화 (lssLib.Binary 기반 인코딩/디코딩)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 프로토콜 계층 인터페이스.
/// 원시 바이트와 애플리케이션 페이로드 사이의 인코딩/디코딩을 담당합니다.
/// </summary>
/// <remarks>
/// 구현 클래스 목록:
/// <list type="bullet">
///   <item><description><c>RawProtocol</c> — 인코딩 없음 (pass-through). 테스트/UDP 용.</description></item>
///   <item><description><c>BinaryProtocol</c> — lssLib.Binary 기반 STX/ETX/Length/CRC 프레임.</description></item>
///   <item><description><c>ModbusRtuProtocol</c> — Modbus RTU 프레임 (확장 예정).</description></item>
/// </list>
/// <para>
/// lssLib.Binary 연동 예시:
/// <code>
/// // BinaryProtocol 내부 Encode 구현 예시
/// public byte[] Encode(byte[] payload)
/// {
///     return BufferWriter.Create()
///         .WriteUInt8(0xAA)                         // STX
///         .WriteUInt16BE((ushort)payload.Length)    // Length
///         .WriteRaw(payload)                        // Data
///         .AppendCrc32()                            // CRC-32 추가
///         .ToArray();
/// }
/// </code>
/// </para>
/// </remarks>
public interface INetProtocol
{
    /// <summary>
    /// 애플리케이션 페이로드를 전송 가능한 프레임으로 인코딩합니다.
    /// </summary>
    /// <param name="payload">원본 데이터 (lssLib.Binary.BufferWriter 로 생성된 바이트 배열)</param>
    /// <returns>헤더·CRC·패딩이 추가된 전송 프레임</returns>
    byte[] Encode(byte[] payload);

    /// <summary>
    /// 수신된 원시 바이트에서 페이로드를 디코딩합니다.
    /// </summary>
    /// <param name="raw">수신된 원시 바이트 (헤더·CRC 포함)</param>
    /// <param name="payload">디코딩된 페이로드. 실패 시 <c>null</c>.</param>
    /// <returns>디코딩 성공 여부</returns>
    bool TryDecode(byte[] raw, out byte[] payload);

    /// <summary>
    /// 수신 버퍼에 완전한 프레임이 있는지 판단합니다.
    /// <para>스트림 기반 전송(TCP, Serial)에서 프레임 경계를 찾는 데 사용됩니다.</para>
    /// </summary>
    /// <param name="buffer">누적 수신 버퍼</param>
    /// <param name="frameLength">완전한 프레임의 길이. 미완성 시 0.</param>
    /// <returns>완전한 프레임 존재 여부</returns>
    bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength);

    /// <summary>
    /// Heartbeat 프레임을 생성합니다.
    /// <para>null 반환 시 Heartbeat 전송을 건너뜁니다.</para>
    /// </summary>
    byte[]? BuildHeartbeat();
}