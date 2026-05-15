// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Protocol/BinaryProtocol.cs
//  프레임: [STX:1B][FC:1B][LEN:2B BE][DATA:NB][CRC32:4B LE]
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;

namespace lssLib.Net.Implementation;

/// <summary>
/// lssLib.Binary 기반 표준 바이너리 프레임 프로토콜.
/// </summary>
/// <remarks>
/// 프레임 구조 (오버헤드 8B):
/// <code>
/// ┌────────┬────────┬──────────┬────────────┬──────────┐
/// │ STX 1B │ FC  1B │ LEN  2B  │  DATA  N B │ CRC32 4B │
/// └────────┴────────┴──────────┴────────────┴──────────┘
/// CRC-32 계산 범위: STX ~ DATA 포함 (CRC 필드 제외)
/// </code>
///
/// lssLib.Binary 연동 시 주석 해제:
/// <code>
/// // public byte[] Encode(byte[] payload)
/// //     => BufferWriter.Create()
/// //         .WriteUInt8(_stx).WriteUInt8(_fc)
/// //         .WriteUInt16BE((ushort)payload.Length)
/// //         .WriteRaw(payload)
/// //         .AppendCrc32()
/// //         .ToArray();
/// </code>
/// </remarks>
public class BinaryProtocol : INetProtocol
{
    #region §1 ─ 상수 / 필드

    private readonly byte _stx;
    private readonly byte _fc;
    private const int OVERHEAD = 8;
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
    public byte[] Encode(byte[] payload)
    {
        var frame = new byte[OVERHEAD + payload.Length];

        frame[0] = _stx;
        frame[OFFSET_FC] = _fc;
        frame[OFFSET_LEN] = (byte)(payload.Length >> 8);
        frame[OFFSET_LEN + 1] = (byte)(payload.Length & 0xFF);

        Buffer.BlockCopy(payload, 0, frame, OFFSET_DATA, payload.Length);
        uint crc = ComputeCrc32(frame, 0, OFFSET_DATA + payload.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(crc), 0,
            frame, OFFSET_DATA + payload.Length, 4);
        return frame;
    }

    /// <inheritdoc/>
    public bool TryDecode(byte[] raw, out byte[] payload)
    {
        payload = Array.Empty<byte>();

        if (raw.Length < OVERHEAD || raw[0] != _stx){
            return false;
        }

        int dataLen = (raw[OFFSET_LEN] << 8) | raw[OFFSET_LEN + 1];
        if (raw.Length < OVERHEAD + dataLen){
            return false;
        }

        uint rxCrc = BitConverter.ToUInt32(raw, OFFSET_DATA + dataLen);
        uint calcCrc = ComputeCrc32(raw, 0, OFFSET_DATA + dataLen);
        
        if (rxCrc != calcCrc){
            return false;
        }
        payload = raw[OFFSET_DATA..(OFFSET_DATA + dataLen)];
        return true;
    }

    /// <inheritdoc/>
    public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = 0;
        if (buffer.Length < OVERHEAD || buffer[0] != _stx) { 
            return false; 
        }
        
        int dataLen = (buffer[OFFSET_LEN] << 8) | buffer[OFFSET_LEN + 1];
        frameLength = OVERHEAD + dataLen;
        
        return buffer.Length >= frameLength;
    }

    /// <inheritdoc/>
    public byte[]? BuildHeartbeat() => Encode(Array.Empty<byte>());

    #endregion

    #region §4 ─ CRC-32 (순수 BCL)

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