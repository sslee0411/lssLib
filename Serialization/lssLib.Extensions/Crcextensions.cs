// ====================================================================
//  lssLib.Extensions — CrcExtensions
//  byte[] 확장 메서드로 CRC / Checksum 계산
//
//  [지원 알고리즘]
//  CRC-8  (0x07)              임베디드, Dallas/Maxim 센서
//  CRC-16/IBM (0x8005)        산업용 통신
//  CRC-16/CCITT (0x1021)      BLE, XMODEM, SD카드
//  CRC-16/Modbus (0xA001)     Modbus RTU 전용
//  CRC-32 (0xEDB88320)        ZIP, Ethernet, PNG
//  Sum8 / Sum8Twos            단순 합산, 2의 보수
//  Sum16                      16bit 합산
//  XOR                        단순 패리티
//  Fletcher-16                TCP/IP 네트워크
//  CRC-8/Sensirion (0x31)     SHT3x / SHT4x 전용
//
//  [사용법]
//  uint   crc = data.Crc32();
//  byte   c8  = data.Crc8();
//  ushort mod = data.Crc16Modbus();
//  byte   sht = new byte[]{hi,lo}.Crc8Sht();
//  byte[] w   = data.AppendCrc32();
//  bool   ok  = dataWithCrc.VerifyCrc32();
//  uint   fc  = await "path.bin".Crc32File();
// ====================================================================

using System.IO;

namespace lssLib.Extensions
{
    /// <summary>
    /// byte[] CRC / Checksum 확장 메서드 모음.
    /// offset / length 파라미터로 부분 범위 계산이 가능합니다.
    /// </summary>
    public static class CrcExtensions
    {
        // ── 룩업 테이블 (정적 초기화) ────────────────────────────────
        private static readonly byte[] _t8 = BuildCrc8(0x07);
        private static readonly ushort[] _t16 = BuildCrc16(0x8005);
        private static readonly ushort[] _t16cc = BuildCrc16(0x1021);
        private static readonly uint[] _t32 = BuildCrc32();

        private static byte[] BuildCrc8(byte p)
        {
            var t = new byte[256];
            for (int i = 0; i < 256; i++)
            { byte c = (byte)i; for (int j = 0; j < 8; j++) c = (c & 0x80) != 0 ? (byte)((c << 1) ^ p) : (byte)(c << 1); t[i] = c; }
            return t;
        }
        private static ushort[] BuildCrc16(ushort p)
        {
            var t = new ushort[256];
            for (int i = 0; i < 256; i++)
            { ushort c = (ushort)(i << 8); for (int j = 0; j < 8; j++) c = (c & 0x8000) != 0 ? (ushort)((c << 1) ^ p) : (ushort)(c << 1); t[i] = c; }
            return t;
        }
        private static uint[] BuildCrc32()
        {
            const uint p = 0xEDB88320u; var t = new uint[256];
            for (uint i = 0; i < 256; i++) { uint c = i; for (int j = 0; j < 8; j++) c = (c & 1) != 0 ? (c >> 1) ^ p : c >> 1; t[i] = c; }
            return t;
        }

        // ────────────────────────────────────────────────────────────
        //  CRC-8
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// CRC-8 계산 (다항식 0x07, 초기값 0x00).
        /// Dallas/Maxim 1-Wire, 임베디드 센서에 사용합니다.
        /// <para>offset: 시작 위치 (기본 0), length: 바이트 수 (기본 전체).</para>
        /// <example><code>
        /// byte[] data = { 0xAA, 0x01, 0x00, 0x0C };
        /// byte crc = data.Crc8();   // 전체 계산
        ///
        /// // 부분 범위 계산
        /// byte crc2 = data.Crc8(offset:1, length:3);  // data[1..3]
        /// </code></example>
        /// </summary>
        public static byte Crc8(this byte[] d, int offset = 0, int? length = null)
        {
            byte c = 0;
            Iter(d, offset, length, b => c = _t8[c ^ b]);
            return c;
        }

        /// <summary>
        /// CRC-8 커스텀 다항식 계산.
        /// 장비별로 다른 CRC-8 다항식이 사용될 때 활용합니다.
        /// <example><code>
        /// // 다항식 0x31, 초기값 0xFF (Sensirion SHT3x)
        /// byte crc = data.Crc8Custom(poly:0x31, init:0xFF);
        ///
        /// // 다항식 0x9B (CAN FD)
        /// byte crc = data.Crc8Custom(poly:0x9B, init:0x00);
        /// </code></example>
        /// </summary>
        public static byte Crc8Custom(this byte[] d, byte poly, byte init = 0,
            int offset = 0, int? length = null)
        {
            var t = BuildCrc8(poly); byte c = init;
            Iter(d, offset, length, b => c = t[c ^ b]);
            return c;
        }

        /// <summary>
        /// Sensirion SHT3x/SHT4x 전용 CRC-8 (다항식 0x31, 초기값 0xFF).
        /// <example><code>
        /// // SHT3x I²C 응답 검증
        /// byte[] resp = { 0x65, 0x66, 0xNUM }; // [tempHi, tempLo, crc]
        /// bool ok = resp[2] == new byte[]{resp[0], resp[1]}.Crc8Sht();
        ///
        /// // SHT4x 습도 CRC 확인
        /// bool humiOk = respBytes[5] == new byte[]{respBytes[3], respBytes[4]}.Crc8Sht();
        /// </code></example>
        /// </summary>
        public static byte Crc8Sht(this byte[] d)
            => d.Crc8Custom(poly: 0x31, init: 0xFF);

        // ────────────────────────────────────────────────────────────
        //  CRC-16
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// CRC-16/IBM 계산 (다항식 0x8005, 산업용 통신).
        /// USB, Serial, 일부 산업 프로토콜에 사용합니다.
        /// <example><code>
        /// byte[] frame = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x04 };
        /// ushort crc = frame.Crc16();
        /// Console.WriteLine($"CRC-16/IBM: 0x{crc:X4}");
        /// </code></example>
        /// </summary>
        public static ushort Crc16(this byte[] d, int offset = 0, int? length = null)
        {
            ushort c = 0;
            Iter(d, offset, length, b => c = (ushort)((_t16[(c >> 8) ^ b]) ^ (c << 8)));
            return c;
        }

        /// <summary>
        /// CRC-16/CCITT 계산 (다항식 0x1021).
        /// BLE, XMODEM, SD카드, HDLC, IrDA 에 사용합니다.
        /// init=0xFFFF (CCITT 표준) 또는 init=0x0000 선택 가능.
        /// <example><code>
        /// byte[] data = { 0x01, 0x02, 0x03, 0x04 };
        ///
        /// // CCITT 표준 (init=0xFFFF)
        /// ushort crc1 = data.Crc16Ccitt(init:0xFFFF);
        ///
        /// // XMODEM (init=0x0000)
        /// ushort crc2 = data.Crc16Ccitt(init:0x0000);
        ///
        /// // BLE L2CAP 체크섬
        /// ushort bleCrc = blePacket.Crc16Ccitt(init:0xFFFF);
        /// </code></example>
        /// </summary>
        public static ushort Crc16Ccitt(this byte[] d, ushort init = 0,
            int offset = 0, int? length = null)
        {
            ushort c = init;
            Iter(d, offset, length, b => c = (ushort)((_t16cc[(c >> 8) ^ b]) ^ (c << 8)));
            return c;
        }

        /// <summary>
        /// CRC-16/Modbus 계산 (다항식 0xA001, 반전 다항식).
        /// Modbus RTU 전용 CRC. 초기값은 항상 0xFFFF 입니다.
        /// <example><code>
        /// // Modbus FC03 요청 프레임 CRC 계산
        /// byte[] req = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x04 };
        /// ushort crc = req.Crc16Modbus();
        /// // 프레임에 CRC 추가: [0x01,0x03,0x00,0x00,0x00,0x04, lo, hi]
        /// byte[] frame = req.Append([( byte)(crc&0xFF), (byte)(crc>>8)]);
        ///
        /// // 응답 프레임 검증 (마지막 2바이트 = CRC LE)
        /// bool ok = resp.Crc16Modbus(0, resp.Length-2) ==
        ///           (ushort)(resp[^2] | (resp[^1]<<8));
        /// </code></example>
        /// </summary>
        public static ushort Crc16Modbus(this byte[] d, int offset = 0, int? length = null)
        {
            ushort c = 0xFFFF;
            Iter(d, offset, length, b =>
            {
                c ^= b;
                for (int j = 0; j < 8; j++) c = (c & 1) != 0 ? (ushort)((c >> 1) ^ 0xA001) : (ushort)(c >> 1);
            });
            return c;
        }

        // ────────────────────────────────────────────────────────────
        //  CRC-32
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// CRC-32 계산 (다항식 0xEDB88320).
        /// ZIP, Ethernet, PNG, MPEG-2 에서 광범위하게 사용합니다.
        /// <example><code>
        /// byte[] data = { 0xAA, 0x01, 0x00, 0x0C };
        /// uint crc = data.Crc32();
        /// Console.WriteLine($"CRC-32: 0x{crc:X8}");
        ///
        /// // 부분 범위 계산
        /// uint partial = data.Crc32(offset:1, length:3);
        ///
        /// // 파일 무결성 검증
        /// uint file1 = await "file1.bin".Crc32File();
        /// uint file2 = await "file2.bin".Crc32File();
        /// Console.WriteLine(file1==file2 ? "동일" : "다름");
        /// </code></example>
        /// </summary>
        public static uint Crc32(this byte[] d, int offset = 0, int? length = null)
        {
            uint c = 0xFFFFFFFF;
            Iter(d, offset, length, b => c = (c >> 8) ^ _t32[(c ^ b) & 0xFF]);
            return c ^ 0xFFFFFFFF;
        }

        // ────────────────────────────────────────────────────────────
        //  Sum / XOR / Fletcher
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Sum8: 모든 바이트의 합산, 하위 8비트 반환.
        /// <example><code>
        /// byte sum = data.Sum8();
        ///
        /// // 프레임 체크섬 추가 (Sum8)
        /// byte[] frame = BuildFrame();
        /// byte cs = frame.Sum8();
        /// byte[] withCs = [..frame, cs];
        ///
        /// // 검증: Sum8 결과가 0x00 이면 정상
        /// bool ok = withCs.Sum8() == 0x00;
        /// </code></example>
        /// </summary>
        public static byte Sum8(this byte[] d, int offset = 0, int? length = null)
        {
            uint s = 0;
            Iter(d, offset, length, b => s += b);
            return (byte)(s & 0xFF);
        }

        /// <summary>
        /// Sum8 2의 보수 체크섬. 데이터에 추가하면 합산이 0x00 이 됩니다.
        /// <example><code>
        /// byte cs = data.Sum8Twos();
        /// byte[] frame = [..data, cs];
        /// // 검증: frame.Sum8() == 0x00
        /// </code></example>
        /// </summary>
        public static byte Sum8Twos(this byte[] d, int offset = 0, int? length = null)
            => (byte)((~d.Sum8(offset, length) + 1) & 0xFF);

        /// <summary>
        /// Sum16: 모든 바이트의 합산, 하위 16비트 반환.
        /// <example><code>
        /// ushort sum = data.Sum16();
        /// </code></example>
        /// </summary>
        public static ushort Sum16(this byte[] d, int offset = 0, int? length = null)
        {
            uint s = 0;
            Iter(d, offset, length, b => s += b);
            return (ushort)(s & 0xFFFF);
        }

        /// <summary>
        /// XOR 체크섬. 모든 바이트를 XOR 합산합니다.
        /// NMEA 0183 GPS, 바코드 스캐너 등에서 사용합니다.
        /// <example><code>
        /// byte xor = data.Xor();
        ///
        /// // NMEA 0183 체크섬 검증
        /// // $GPRMC,...*XX  ← XX가 XOR 체크섬
        /// byte cs = Encoding.ASCII.GetBytes(sentence[1..^3]).Xor();
        /// bool ok = cs == byte.Parse(sentence[^2..], NumberStyles.HexNumber);
        /// </code></example>
        /// </summary>
        public static byte Xor(this byte[] d, int offset = 0, int? length = null)
        {
            byte x = 0;
            Iter(d, offset, length, b => x ^= b);
            return x;
        }

        /// <summary>
        /// Fletcher-16 체크섬. TCP/IP, 네트워크 프로토콜에 사용합니다.
        /// CRC보다 빠르면서 Sum8 보다 오류 검출 능력이 좋습니다.
        /// <example><code>
        /// ushort f16 = data.Fletcher16();
        /// // 상위 바이트: sum2, 하위 바이트: sum1
        /// byte sum1 = (byte)(f16 &amp; 0xFF);
        /// byte sum2 = (byte)(f16 >> 8);
        /// </code></example>
        /// </summary>
        public static ushort Fletcher16(this byte[] d, int offset = 0, int? length = null)
        {
            uint s1 = 0, s2 = 0;
            Iter(d, offset, length, b => { s1 = (s1 + b) % 255; s2 = (s2 + s1) % 255; });
            return (ushort)((s2 << 8) | s1);
        }

        // ────────────────────────────────────────────────────────────
        //  CRC-32 Append / Verify
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 데이터 뒤에 CRC-32 를 4바이트(Little-Endian)로 추가합니다.
        /// <example><code>
        /// byte[] data    = { 0xAA, 0x01, 0x00, 0x0C };
        /// byte[] withCrc = data.AppendCrc32();
        /// // [0xAA,0x01,0x00,0x0C, lo,lo,hi,hi] (8바이트)
        ///
        /// // 검증
        /// bool ok = withCrc.VerifyCrc32();  // true
        ///
        /// // 저장 후 검증
        /// await File.WriteAllBytesAsync("data.bin", withCrc);
        /// byte[] loaded = await File.ReadAllBytesAsync("data.bin");
        /// bool valid = loaded.VerifyCrc32();
        /// </code></example>
        /// </summary>
        public static byte[] AppendCrc32(this byte[] d)
        {
            uint crc = d.Crc32();
            var r = new byte[d.Length + 4];
            Array.Copy(d, r, d.Length);
            r[d.Length + 0] = (byte)(crc & 0xFF);
            r[d.Length + 1] = (byte)((crc >> 8) & 0xFF);
            r[d.Length + 2] = (byte)((crc >> 16) & 0xFF);
            r[d.Length + 3] = (byte)((crc >> 24) & 0xFF);
            return r;
        }

        /// <summary>
        /// 데이터의 마지막 4바이트를 CRC-32 로 검증합니다.
        /// AppendCrc32() 로 추가된 CRC 를 검증합니다.
        /// <example><code>
        /// byte[] withCrc = data.AppendCrc32();
        ///
        /// bool ok = withCrc.VerifyCrc32();   // true
        ///
        /// withCrc[0] ^= 0xFF;  // 데이터 훼손
        /// bool fail = withCrc.VerifyCrc32(); // false
        /// </code></example>
        /// </summary>
        public static bool VerifyCrc32(this byte[] d)
        {
            if (d.Length < 4) return false;
            int n = d.Length - 4;
            uint s = (uint)(d[n] | (d[n + 1] << 8) | (d[n + 2] << 16) | (d[n + 3] << 24));
            return s == d.Crc32(0, n);
        }

        // ────────────────────────────────────────────────────────────
        //  파일 CRC-32
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 파일 전체의 CRC-32 를 비동기로 계산합니다.
        /// 대용량 파일 무결성 검증에 사용합니다.
        /// <example><code>
        /// // 파일 무결성 검사
        /// uint crc1 = await "firmware_v1.bin".Crc32File();
        /// uint crc2 = await "firmware_v2.bin".Crc32File();
        /// Console.WriteLine(crc1==crc2 ? "동일한 파일" : "파일 다름");
        ///
        /// // 배포 파일 해시 검증
        /// uint expected = 0xDEADBEEF;
        /// uint actual   = await "setup.exe".Crc32File();
        /// bool ok = expected == actual;
        /// </code></example>
        /// </summary>
        public static async Task<uint> Crc32File(this string path)
            => (await File.ReadAllBytesAsync(path)).Crc32();

        // ────────────────────────────────────────────────────────────
        //  포맷 유틸
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// byte[] → HEX 덤프 문자열.
        /// <example><code>
        /// string hex = data.ToHexDump();       // "AA 01 00 0C"
        /// string hex = data.ToHexDump("-");    // "AA-01-00-0C"
        /// string hex = data.ToHexDump("");     // "AA01000C"
        /// </code></example>
        /// </summary>
        public static string ToHexDump(this byte[] d, string sep = " ")
            => string.Join(sep, d.Select(b => b.ToString("X2")));

        // ── 내부 헬퍼 ────────────────────────────────────────────────
        private static void Iter(byte[] d, int o, int? n, Action<byte> fn)
        {
            int end = o + (n ?? d.Length - o);
            if (o < 0 || end > d.Length)
                throw new ArgumentOutOfRangeException(nameof(o),
                    $"offset={o} length={n ?? d.Length - o} bufLen={d.Length}");
            for (int i = o; i < end; i++) fn(d[i]);
        }
    }
}