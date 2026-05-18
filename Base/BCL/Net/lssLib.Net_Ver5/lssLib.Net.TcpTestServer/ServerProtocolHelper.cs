// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net.TcpTestServer · ServerProtocolHelper.cs
//  역할: BinaryProtocol 호환 프레임 빌더 / 파서 (lssLib.Net 미참조)
//
//  프레임 구조 (lssLib.Net BinaryProtocol 동일):
//  ┌────┬────┬─────┬──────┬─────┐
//  │ STX 1B │ FC  1B │ LEN  2B  │  DATA  N B │ CRC32 4B │
//  └────┴────┴─────┴──────┴─────┘
//  STX = 0xAA, FC = 0x01, LEN = Big-Endian, CRC32 = Little-Endian
//  CRC 범위: STX ~ DATA 포함 (CRC 필드 제외)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net.TcpTestServer;

/// <summary>
/// BinaryProtocol 호환 프레임 빌더 / 파서.
/// lssLib.Net을 참조하지 않는 독립 헬퍼입니다.
/// </summary>
internal static class ServerProtocolHelper
{
    #region §1 ─ 상수

    public const byte STX = 0xAA;
    public const byte FC = 0x01;
    public const int OVERHEAD = 8;   // STX(1) + FC(1) + LEN(2) + CRC(4)

    #endregion

    #region §2 ─ 프레임 빌더

    /// <summary>페이로드를 BinaryProtocol 프레임으로 조립합니다.</summary>
    public static byte[] BuildFrame(byte[] payload)
    {
        int frameLen = OVERHEAD + payload.Length;
        var frame = new byte[frameLen];

        frame[0] = STX;
        frame[1] = FC;
        frame[2] = (byte)(payload.Length >> 8);    // LEN Hi
        frame[3] = (byte)(payload.Length & 0xFF);  // LEN Lo

        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

        uint crc = ComputeCrc32(frame, 0, 4 + payload.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(crc), 0, frame, 4 + payload.Length, 4);

        return frame;
    }

    #endregion

    #region §3 ─ 프레임 파서

    /// <summary>
    /// 원시 바이트에서 페이로드를 추출합니다.
    /// STX 검증 + 길이 검증 + CRC-32 검증을 수행합니다.
    /// </summary>
    public static bool TryParseFrame(byte[] raw, out byte[] payload)
    {
        payload = Array.Empty<byte>();

        if (raw.Length < OVERHEAD || raw[0] != STX) return false;

        int dataLen = (raw[2] << 8) | raw[3];
        int frameLen = OVERHEAD + dataLen;

        if (raw.Length < frameLen) return false;

        uint rxCrc = BitConverter.ToUInt32(raw, 4 + dataLen);
        uint calcCrc = ComputeCrc32(raw, 0, 4 + dataLen);

        if (rxCrc != calcCrc) return false;

        payload = raw[4..(4 + dataLen)];
        return true;
    }

    /// <summary>
    /// 스트림 누적 버퍼에서 완전한 프레임을 추출합니다.
    /// 추출된 바이트는 buffer에서 제거됩니다.
    /// </summary>
    public static bool TryExtractFrame(List<byte> buffer, out byte[] payload)
    {
        payload = Array.Empty<byte>();

        // STX 위치 탐색 — STX 이전 쓰레기 바이트 제거
        int stxIdx = buffer.IndexOf(STX);
        if (stxIdx < 0) { buffer.Clear(); return false; }
        if (stxIdx > 0) buffer.RemoveRange(0, stxIdx);

        // 헤더 최소 크기 대기
        if (buffer.Count < OVERHEAD) return false;

        int dataLen = (buffer[2] << 8) | buffer[3];
        int frameLen = OVERHEAD + dataLen;

        // 데이터 도착 대기
        if (buffer.Count < frameLen) return false;

        var frame = buffer.GetRange(0, frameLen).ToArray();

        if (!TryParseFrame(frame, out payload))
        {
            // CRC 오류 — STX 한 바이트 건너뛰고 재탐색
            buffer.RemoveAt(0);
            return false;
        }

        buffer.RemoveRange(0, frameLen);
        return true;
    }

    #endregion

    #region §4 ─ 페이로드 빌더 (Push 전용)

    /// <summary>
    /// Push 모드 센서 데이터 페이로드를 조립합니다.
    /// 구조: [FrameId:uint 4B LE][Temp:float 4B LE][Humidity:float 4B LE] = 12B
    /// </summary>
    public static byte[] BuildSensorPayload(uint frameId, float temp, float humidity)
    {
        var buf = new byte[12];
        Buffer.BlockCopy(BitConverter.GetBytes(frameId), 0, buf, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(temp), 0, buf, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(humidity), 0, buf, 8, 4);
        return buf;
    }

    /// <summary>
    /// Echo 모드 응답 페이로드를 조립합니다.
    /// 구조: [FC:byte 1B][DataLen:byte 1B][FrameId:uint 4B LE][Temp:float 4B LE][Humidity:float 4B LE] = 14B
    /// </summary>
    public static byte[] BuildEchoResponsePayload(uint frameId, float temp, float humidity)
    {
        const byte RESP_FC = 0x03;
        const byte DATA_LEN = 12;          // FrameId(4) + Temp(4) + Humidity(4)

        var buf = new byte[14];
        buf[0] = RESP_FC;
        buf[1] = DATA_LEN;
        Buffer.BlockCopy(BitConverter.GetBytes(frameId), 0, buf, 2, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(temp), 0, buf, 6, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(humidity), 0, buf, 10, 4);
        return buf;
    }

    #endregion

    #region §5 ─ CRC-32 (BinaryProtocol 동일 알고리즘)

    private static uint ComputeCrc32(byte[] data, int offset, int length)
    {
        const uint POLY = 0xEDB88320u;
        uint crc = 0xFFFFFFFF;

        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) == 1 ? (crc >> 1) ^ POLY : crc >> 1;
        }

        return ~crc;
    }

    #endregion
}