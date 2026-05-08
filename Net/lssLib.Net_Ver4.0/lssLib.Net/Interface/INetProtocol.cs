// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Interface/INetProtocol.cs
//  역할: 프로토콜 계층 인터페이스
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 프로토콜 계층 인터페이스.
/// 원시 바이트 ↔ 애플리케이션 페이로드 변환을 담당합니다.
/// </summary>
/// <remarks>
/// <b>구현체 (Protocol/):</b>
/// <c>RawProtocol</c> (pass-through) / <c>BinaryProtocol</c> (STX/FC/LEN/DATA/CRC-32)
///
/// <b>lssLib.Binary 연동 예시:</b>
/// <code>
/// public byte[] Encode(byte[] payload)
///     => BufferWriter.Create()
///         .WriteUInt8(0xAA).WriteUInt8(0x01)
///         .WriteUInt16BE((ushort)payload.Length)
///         .WriteRaw(payload)
///         .AppendCrc32()
///         .ToArray();
/// </code>
/// </remarks>
public interface INetProtocol
{
    /// <summary>
    /// 페이로드를 전송 가능한 프레임으로 인코딩<br/>
    /// 정해진 규칙에 따라 변환 (프로토콜 생성)<br/>
    /// (헤더·CRC 추가)
    /// </summary>
    byte[] Encode(byte[] payload);

    /// <summary>
    /// 수신된 원시 바이트에서 페이로드를 디코딩<br/>
    /// 부호화(Encoding)된 데이터를 원래의 형태(사람이 읽을 수 있는 형태나 본래의 신호)로 <br/>
    /// 다시 변환하는 복호화 또는 해독
    /// </summary>
    /// <param name="raw">수신 원시 바이트</param>
    /// <param name="payload">디코딩된 페이로드. 실패 시 빈 배열.</param>
    /// <returns>CRC 검증 포함 디코딩 성공 여부</returns>
    bool TryDecode(byte[] raw, out byte[] payload);

    /// <summary>
    /// 스트림 누적 버퍼에 완전한 프레임이 있는지 판단합니다.
    /// <para>TCP / Serial 스트림 기반에서 프레임 경계 탐색에 사용합니다.</para>
    /// </summary>
    bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength);

    /// <summary>
    /// Heartbeat 프레임을 생성합니다.
    /// <para>null 반환 시 해당 주기 Heartbeat 전송을 건너뜁니다.</para>
    /// </summary>
    byte[]? BuildHeartbeat();
}