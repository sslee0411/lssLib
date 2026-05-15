// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Protocol/BinaryProtocol.cs
//  프레임: [STX:1B][FC:1B][LEN:2B BE][DATA:NB][CRC32:4B LE]
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// lssLib.Binary 기반 표준 바이너리 프레임 프로토콜.
/// </summary>
/// <remarks>
/// <b>프레임 구조 (오버헤드 8B):</b>
/// <code>
/// ┌────────┬───────┬──────────┬────────────┬──────────┐
/// │ STX 1B │ FC 1B │ LEN  2B  │  DATA  N B │ CRC32 4B │
/// └────────┴───────┴──────────┴────────────┴──────────┘
/// CRC-32 계산 범위: STX ~ DATA 포함 (CRC 필드 제외)
/// LEN: Big-Endian (2바이트)
/// CRC32: Little-Endian (4바이트)
/// </code>
///
/// <b>lssLib.Binary 연동 시 주석 해제:</b>
/// <code>
/// // Encode 메서드 lssLib.Binary 버전
/// // public byte[] Encode(byte[] payload)
/// //     => BufferWriter.Create()
/// //         .WriteUInt8(_stx).WriteUInt8(_fc)
/// //         .WriteUInt16BE((ushort)payload.Length)
/// //         .WriteRaw(payload)
/// //         .AppendCrc32()
/// //         .ToArray();
///
/// // TryDecode 후 파싱
/// // var result = payload.ToParser().Parse(MySchema.Default);
/// // float temp = result.GetFloat("Temperature");
/// </code>
/// </remarks>
public class BinaryProtocol : INetProtocol
{
    #region §1 ─ 상수 / 필드

    private readonly byte _stx;
    private readonly byte _fc;
    private const int OVERHEAD = 8;  // STX(1) + FC(1) + LEN(2) + CRC(4)
    private const int OFFSET_FC = 1;
    private const int OFFSET_LEN = 2;
    private const int OFFSET_DATA = 4;

    #endregion

    #region §2 ─ 생성자

    /// <param name="stx">프레임 시작 바이트. 기본: 0xAA.</param>
    /// <param name="fc">기능 코드. 기본: 0x01.</param>
    public BinaryProtocol(byte stx = 0xAA, byte fc = 0x01)
    {
        _stx = stx;
        _fc = fc;
    }

    #endregion

    #region §3 ─ INetProtocol

    /// <inheritdoc/>
    /// <remarks>
    /// 인코딩 순서:
    /// [STX][FC][LEN_Hi][LEN_Lo][DATA...][CRC0][CRC1][CRC2][CRC3]
    /// </remarks>
    public byte[] Encode(byte[] payload)
    {
        var frame = new byte[OVERHEAD + payload.Length];
        frame[0] = _stx;
        frame[OFFSET_FC] = _fc;
        frame[OFFSET_LEN] = (byte)(payload.Length >> 8);    // LEN Hi
        frame[OFFSET_LEN + 1] = (byte)(payload.Length & 0xFF);  // LEN Lo
        Buffer.BlockCopy(payload, 0, frame, OFFSET_DATA, payload.Length);
        // CRC-32: STX ~ DATA 범위 계산
        uint crc = ComputeCrc32(frame, 0, OFFSET_DATA + payload.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(crc), 0,
            frame, OFFSET_DATA + payload.Length, 4);
        return frame;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <list type="number">
    ///   <item><description>최소 길이 확인 (OVERHEAD=8B)</description></item>
    ///   <item><description>STX 검증</description></item>
    ///   <item><description>LEN 필드로 데이터 길이 계산</description></item>
    ///   <item><description>CRC-32 검증</description></item>
    ///   <item><description>DATA 부분 추출하여 payload 반환</description></item>
    /// </list>
    /// </remarks>
    public bool TryDecode(byte[] raw, out byte[] payload)
    {
        payload = Array.Empty<byte>();
        if (raw.Length < OVERHEAD || raw[0] != _stx) return false;

        int dataLen = (raw[OFFSET_LEN] << 8) | raw[OFFSET_LEN + 1];
        if (raw.Length < OVERHEAD + dataLen) return false;

        uint rxCrc = BitConverter.ToUInt32(raw, OFFSET_DATA + dataLen);
        uint calcCrc = ComputeCrc32(raw, 0, OFFSET_DATA + dataLen);
        if (rxCrc != calcCrc) return false;

        payload = raw[OFFSET_DATA..(OFFSET_DATA + dataLen)];
        return true;
    }

    /// <inheritdoc/>
    public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = 0;
        if (buffer.Length < OVERHEAD || buffer[0] != _stx) return false;
        int dataLen = (buffer[OFFSET_LEN] << 8) | buffer[OFFSET_LEN + 1];
        frameLength = OVERHEAD + dataLen;
        return buffer.Length >= frameLength;
    }

    /// <inheritdoc/>
    /// <remarks>빈 페이로드 프레임을 생성합니다 (Keep-Alive 역할).</remarks>
    public byte[]? BuildHeartbeat() => Encode(Array.Empty<byte>());

    #endregion

    #region §4 ─ CRC-32 (BCL 순수 구현)

    /// <summary>
    /// CRC-32 계산 (IEEE 802.3 다항식: 0xEDB88320).
    /// <para>lssLib.Extensions.CrcExtensions.ComputeCrc32() 와 동일한 알고리즘.</para>
    /// </summary>
    private static uint ComputeCrc32(byte[] data, int offset, int length)
    {
        const uint poly = 0xEDB88320u;
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) == 1 ? (crc >> 1) ^ poly : crc >> 1;
        }
        return ~crc;
    }

    #endregion
}