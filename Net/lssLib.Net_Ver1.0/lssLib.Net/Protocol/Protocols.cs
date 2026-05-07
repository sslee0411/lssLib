// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · RawProtocol.cs
//  역할: 인코딩 없는 pass-through 프로토콜 (테스트 / UDP 용)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net.Protocol;

/// <summary>
/// 인코딩·디코딩 없이 원시 바이트를 그대로 통과시키는 프로토콜.
/// 테스트, UDP, 외부 프레이밍이 이미 완성된 환경에서 사용합니다.
/// </summary>
public sealed class RawProtocol : INetProtocol
{
    /// <inheritdoc/>
    public byte[] Encode(byte[] payload) => payload;

    /// <inheritdoc/>
    public bool TryDecode(byte[] raw, out byte[] payload)
    {
        payload = raw;
        return true;
    }

    /// <inheritdoc/>
    public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = buffer.Length;
        return buffer.Length > 0;
    }

    /// <inheritdoc/>
    public byte[]? BuildHeartbeat() => null;
}


// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · BinaryProtocol.cs
//  역할: lssLib.Binary 기반 STX/Length/Data/CRC-32 프레임 프로토콜
// ══════════════════════════════════════════════════════════════════════

// using lssLib.Binary;
// using lssLib.Extensions;  // CrcExtensions

//namespace lssLib.Net.Protocol;

/// <summary>
/// lssLib.Binary 기반 표준 프레임 프로토콜.
/// </summary>
/// <remarks>
/// 프레임 구조:
/// <code>
/// [STX  : 1B]  0xAA
/// [FC   : 1B]  기능 코드 (파생 클래스에서 정의)
/// [LEN  : 2B BE] 페이로드 길이
/// [DATA : NB]  페이로드 (lssLib.Binary.BufferWriter 로 생성)
/// [CRC32: 4B LE] CRC-32 (STX~DATA 포함 계산)
/// </code>
/// 총 오버헤드: 8바이트
/// </remarks>
/// <example><code>
/// var protocol = new BinaryProtocol(stx: 0xAA, fc: 0x01);
/// byte[] frame = protocol.Encode(payloadBytes);
///
/// if (protocol.TryDecode(received, out var payload))
///     var result = payload.ToParser().Parse(MySchema.Default);
/// </code></example>
public class BinaryProtocol : INetProtocol
{
    #region §1 ─ 필드

    private readonly byte _stx;
    private readonly byte _fc;

    private const int OVERHEAD = 8;  // STX(1)+FC(1)+LEN(2)+CRC(4)
    private const int OFFSET_FC = 1;
    private const int OFFSET_LEN = 2;
    private const int OFFSET_DATA = 4;

    #endregion

    #region §2 ─ 생성자

    /// <param name="stx">프레임 시작 바이트. 기본값: 0xAA.</param>
    /// <param name="fc">기능 코드. 기본값: 0x01.</param>
    public BinaryProtocol(byte stx = 0xAA, byte fc = 0x01)
    {
        _stx = stx;
        _fc = fc;
    }

    #endregion

    #region §3 ─ INetProtocol 구현

    /// <inheritdoc/>
    public byte[] Encode(byte[] payload)
    {
        // lssLib.Binary.BufferWriter 사용 예시
        // (실제 구현 시 using lssLib.Binary; 추가 후 사용)
        //
        // return BufferWriter.Create()
        //     .WriteUInt8(_stx)
        //     .WriteUInt8(_fc)
        //     .WriteUInt16BE((ushort)payload.Length)
        //     .WriteRaw(payload)
        //     .AppendCrc32()         // lssLib.Extensions.CrcExtensions
        //     .ToArray();

        // ── 임시 순수 BCL 구현 (lssLib.Binary 없이 빌드 가능하도록) ──
        var frame = new byte[OVERHEAD + payload.Length];
        frame[0] = _stx;
        frame[OFFSET_FC] = _fc;
        frame[OFFSET_LEN] = (byte)(payload.Length >> 8);
        frame[OFFSET_LEN + 1] = (byte)(payload.Length & 0xFF);
        Buffer.BlockCopy(payload, 0, frame, OFFSET_DATA, payload.Length);

        uint crc = ComputeCrc32Simple(frame, 0, OFFSET_DATA + payload.Length);
        var crcBytes = BitConverter.GetBytes(crc);
        Buffer.BlockCopy(crcBytes, 0, frame, OFFSET_DATA + payload.Length, 4);

        return frame;
    }

    /// <inheritdoc/>
    public bool TryDecode(byte[] raw, out byte[] payload)
    {
        payload = Array.Empty<byte>();
        if (raw.Length < OVERHEAD) return false;
        if (raw[0] != _stx) return false;

        int dataLen = (raw[OFFSET_LEN] << 8) | raw[OFFSET_LEN + 1];
        if (raw.Length < OVERHEAD + dataLen) return false;

        // CRC 검증
        uint rxCrc = BitConverter.ToUInt32(raw, OFFSET_DATA + dataLen);
        uint calcCrc = ComputeCrc32Simple(raw, 0, OFFSET_DATA + dataLen);
        if (rxCrc != calcCrc) return false;

        payload = raw[OFFSET_DATA..(OFFSET_DATA + dataLen)];
        return true;
    }

    /// <inheritdoc/>
    public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = 0;
        if (buffer.Length < OVERHEAD) return false;
        if (buffer[0] != _stx) return false;

        int dataLen = (buffer[OFFSET_LEN] << 8) | buffer[OFFSET_LEN + 1];
        frameLength = OVERHEAD + dataLen;
        return buffer.Length >= frameLength;
    }

    /// <inheritdoc/>
    public byte[]? BuildHeartbeat()
        => Encode(Array.Empty<byte>());  // 페이로드 없는 Heartbeat 프레임

    #endregion

    #region §4 ─ 내부 유틸

    // 순수 BCL CRC-32 (lssLib.Extensions.CrcExtensions 사용 시 대체)
    private static uint ComputeCrc32Simple(byte[] data, int offset, int length)
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