// ====================================================================
//  lssLib.Binary — BinaryExtensions
//  byte[] / BufferParser / Struct / decimal 확장 메서드 모음
//
//  ── byte[] → BufferParser ────────────────────────────────────────
//  bytes.ToParser()                      → BufferParser 생성
//  "AA BB".ToParser()                    → HEX 문자열에서 생성
//
//  ── BufferParser 훅 체이닝 ──────────────────────────────────────
//  bp.WithLog(log)                       → 읽기 로그 주입
//  bp.WithXorDecrypt(key)                → XOR 복호화
//  bp.WithPreprocess(fn)                 → 커스텀 전처리
//  bp.WithOffset(baseOffset)             → 슬라이딩 윈도우
//  bp.WithStats(dict)                    → 읽기 통계 수집
//  bp.OnParseDone(callback)              → 파싱 완료 콜백
//
//  ── byte[] Endian 확장 ──────────────────────────────────────────
//  bytes.ReadUInt16BE(offset)
//  bytes.ReadFloatBE(offset)
//  bytes.ReadDecimalLE(offset)           ← decimal 16바이트 읽기
//  value.ToBigEndianBytes()
//  value.ToLittleEndianBytes()
//  myDecimal.ToBytes()                   ← decimal → 16바이트 LE
//  myDecimal.ToBigEndianBytes()          ← decimal → 16바이트 BE
//  myDecimal.Decompose()                 ← 부호/자릿수/내부값 분해
//
//  ── decimal 배열 변환 ───────────────────────────────────────────
//  decimals.ToLEBytes()                  ← decimal[] → byte[]
//  decimals.ToBEBytes()                  ← decimal[] → byte[]
//  bytes.ToDecimalLEArray(offset, count) ← byte[] → decimal[]
//  bytes.ToDecimalBEArray(offset, count) ← byte[] → decimal[]
//
//  ── Struct 확장 ─────────────────────────────────────────────────
//  bytes.To<MyStruct>()                  ← byte[] → Struct
//  myStruct.ToBytes()                    ← Struct → byte[]
//  myStruct.Dump()                       ← HEX 덤프 + 필드 목록
// ====================================================================

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace lssLib.Binary
{
    /// <summary>
    /// byte[], BufferParser, Struct, decimal 에 대한 확장 메서드 모음.
    /// Abstractions(추상 기반 클래스) 없이 확장 메서드만으로 모든 기능을 제공합니다.
    /// </summary>
    public static class BinaryExtensions
    {
        // ────────────────────────────────────────────────────────────
        //  [ 1 ] byte[] / string → BufferParser 생성
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// byte[] 를 BufferParser 로 변환합니다.
        /// <example><code>
        /// byte[] raw = { 0xAA, 0x03, 0x00, 0x10 };
        /// var bp = raw.ToParser();
        /// byte stx = bp.ReadUInt8(0);   // 0xAA
        /// </code></example>
        /// </summary>
        public static BufferParser ToParser(this byte[] bytes)
        {
            return BufferParser.From(bytes);
        }

        /// <summary>
        /// HEX 문자열을 BufferParser 로 변환합니다.
        /// 공백·하이픈·0x 접두사 자동 제거.
        /// <example><code>
        /// var bp = "AA BB CC DD".ToParser();
        /// var bp = "AA-BB-CC".ToParser();
        /// var bp = "0xAA0xBB".ToParser();
        /// </code></example>
        /// </summary>
        public static BufferParser ToParser(this string hex)
            => BufferParser.FromHex(hex);

        // ────────────────────────────────────────────────────────────
        //  [ 2 ] BufferParser 훅 확장 — 체이닝 가능
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 읽기 로그를 주입합니다. 각 ReadXxx 호출 전후에 log 콜백이 실행됩니다.
        /// <example><code>
        /// var log = new StringBuilder();
        /// var bp = raw.ToParser()
        ///     .WithLog(s => log.AppendLine(s));
        ///
        /// // Console 에 직접 출력
        /// var bp = raw.ToParser().WithLog(Console.WriteLine);
        ///
        /// bp.Parse(schema);
        /// Console.WriteLine(log);
        /// // 출력:
        /// //   Read [UInt8      ] offset=  0 → 0xAA(170)
        /// //   Read [FloatBE    ] offset=  4 → 10.0
        /// </code></example>
        /// </summary>
        public static BufferParser WithLog(this BufferParser bp, Action<string> log)
        {
            bp.HookBefore += (o, sz, nm) => log($"  Read [{nm,-12}] offset={o,4}");
            bp.HookAfter += (o, sz, nm, v) => log($" → {BufResult.FormatVal(v)}");
            return bp;
        }

        /// <summary>
        /// 읽기 전후 커스텀 콜백을 등록합니다.
        /// <example><code>
        /// var bp = raw.ToParser().WithHook(
        ///     onBefore: (offset, size, typeName) =>
        ///         Debug.WriteLine($"Before: {typeName} @ {offset}"),
        ///     onAfter: (offset, size, typeName, value) =>
        ///         Debug.WriteLine($"After: {typeName} = {value}")
        /// );
        /// </code></example>
        /// </summary>
        public static BufferParser WithHook(this BufferParser bp,
            Action<int, int, string> onBefore, Action<int, int, string, object> onAfter)
        {
            bp.HookBefore += onBefore;
            bp.HookAfter += onAfter;
            return bp;
        }

        /// <summary>
        /// 스키마 파싱 완료 후 콜백을 등록합니다.
        /// 결과 검증, 타임스탬프 주입, CRC 확인 등에 활용합니다.
        /// <example><code>
        /// var bp = raw.ToParser()
        ///     .OnParseDone((result, schema) =>
        ///     {
        ///         // STX 확인
        ///         if (result.GetOr<byte>("STX") != 0xAA)
        ///             throw new InvalidDataException("STX 불일치");
        ///
        ///         // 파싱 시각 기록
        ///         Console.WriteLine($"파싱 완료: {schema.Fields.Count}개 필드");
        ///     });
        /// </code></example>
        /// </summary>
        public static BufferParser OnParseDone(this BufferParser bp,
            Action<BufResult, BufSchema> callback)
        {
            bp.HookParsed += callback;
            return bp;
        }

        /// <summary>
        /// XOR 복호화를 전처리로 적용합니다.
        /// 암호화된 프레임을 상속 없이 투명하게 처리합니다.
        /// <example><code>
        /// const byte KEY = 0xAA;
        /// byte[] encrypted = GetFromDevice();
        ///
        /// // XOR 복호화 후 파싱 — 원본 byte[] 는 변경하지 않음
        /// var result = encrypted.ToParser()
        ///     .WithXorDecrypt(KEY)
        ///     .Parse(schema);
        ///
        /// // 복호화 확인
        /// var bp = encrypted.ToParser().WithXorDecrypt(KEY);
        /// Console.WriteLine(bp.ToHex()); // 복호화된 값 출력
        /// </code></example>
        /// </summary>
        public static BufferParser WithXorDecrypt(this BufferParser bp, byte key)
        {
            bp.Preprocessor = raw =>
            {
                var dec = new byte[raw.Length];
                for (int i = 0; i < raw.Length; i++) dec[i] = (byte)(raw[i] ^ key);
                return dec;
            };
            return bp;
        }

        /// <summary>
        /// 커스텀 전처리 함수를 적용합니다.
        /// AES 복호화, 압축 해제, 체크섬 제거 등에 사용합니다.
        /// <example><code>
        /// // AES 복호화 후 파싱
        /// var bp = encrypted.ToParser()
        ///     .WithPreprocess(raw => AesDecrypt(raw, key, iv));
        ///
        /// // 앞 4바이트(헤더) 건너뛰기
        /// var bp = raw.ToParser()
        ///     .WithPreprocess(data => data.Skip(4).ToArray());
        /// </code></example>
        /// </summary>
        public static BufferParser WithPreprocess(this BufferParser bp,
            Func<byte[], byte[]> transform)
        {
            bp.Preprocessor = transform;
            return bp;
        }

        /// <summary>
        /// baseOffset 이후 슬라이스를 기준 버퍼로 사용합니다.
        /// 멀티 프레임 처리, 헤더 건너뛰기에 유용합니다.
        /// <example><code>
        /// // 10바이트 헤더 이후부터 파싱
        /// var bp = raw.ToParser().WithOffset(10);
        ///
        /// // 수신 버퍼가 여러 프레임을 포함할 때
        /// int frameStart = FindFrameStart(raw);
        /// var bp = raw.ToParser().WithOffset(frameStart);
        /// </code></example>
        /// </summary>
        public static BufferParser WithOffset(this BufferParser bp, int baseOffset)
        {
            bp.Preprocessor = raw => raw.AsSpan(baseOffset).ToArray();
            return bp;
        }

        /// <summary>
        /// 읽기 통계를 수집합니다. 타입 이름 → 호출 횟수.
        /// <example><code>
        /// var stats = new Dictionary<string, int>();
        /// raw.ToParser().WithStats(stats).Parse(schema);
        ///
        /// foreach (var (type, count) in stats)
        ///     Console.WriteLine($"  {type}: {count}회");
        /// // UInt8:  2회
        /// // FloatBE: 1회
        /// // DecimalLE: 1회
        /// </code></example>
        /// </summary>
        public static BufferParser WithStats(this BufferParser bp, Dictionary<string, int> stats)
        {
            bp.HookBefore += (_, _, nm) =>
                stats[nm] = stats.TryGetValue(nm, out int c) ? c + 1 : 1;
            return bp;
        }

        // ────────────────────────────────────────────────────────────
        //  [ 3 ] byte[] Endian 확장 — 직접 읽기
        // ────────────────────────────────────────────────────────────

        /// <summary>byte[] 에서 short (Big-Endian) 을 읽습니다.</summary>
        public static short ReadInt16BE(this byte[] d, int o = 0) => BinaryPrimitives.ReadInt16BigEndian(d.AsSpan(o, 2));
        /// <summary>byte[] 에서 ushort (Big-Endian) 을 읽습니다.</summary>
        public static ushort ReadUInt16BE(this byte[] d, int o = 0) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(o, 2));
        /// <summary>byte[] 에서 int (Big-Endian) 을 읽습니다.</summary>
        public static int ReadInt32BE(this byte[] d, int o = 0) => BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(o, 4));
        /// <summary>byte[] 에서 uint (Big-Endian) 을 읽습니다.</summary>
        public static uint ReadUInt32BE(this byte[] d, int o = 0) => BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o, 4));
        /// <summary>byte[] 에서 long (Big-Endian) 을 읽습니다.</summary>
        public static long ReadInt64BE(this byte[] d, int o = 0) => BinaryPrimitives.ReadInt64BigEndian(d.AsSpan(o, 8));

        /// <summary>byte[] 에서 short (Little-Endian) 을 읽습니다.</summary>
        public static short ReadInt16LE(this byte[] d, int o = 0) => BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(o, 2));
        /// <summary>byte[] 에서 ushort (Little-Endian) 을 읽습니다.</summary>
        public static ushort ReadUInt16LE(this byte[] d, int o = 0) => BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o, 2));
        /// <summary>byte[] 에서 int (Little-Endian) 을 읽습니다.</summary>
        public static int ReadInt32LE(this byte[] d, int o = 0) => BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o, 4));
        /// <summary>byte[] 에서 uint (Little-Endian) 을 읽습니다.</summary>
        public static uint ReadUInt32LE(this byte[] d, int o = 0) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o, 4));
        /// <summary>byte[] 에서 long (Little-Endian) 을 읽습니다.</summary>
        public static long ReadInt64LE(this byte[] d, int o = 0) => BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(o, 8));

        /// <summary>
        /// byte[] 에서 float (Big-Endian) 을 읽습니다.
        /// <example><code>
        /// // Modbus 레지스터에서 온도값 읽기
        /// float temp = responseBytes.ReadFloatBE(offset:3);
        /// </code></example>
        /// </summary>
        public static float ReadFloatBE(this byte[] d, int o = 0)
        { var t = d.AsSpan(o, 4).ToArray(); if (BitConverter.IsLittleEndian) Array.Reverse(t); return BitConverter.ToSingle(t); }
        /// <summary>byte[] 에서 float (Little-Endian) 을 읽습니다.</summary>
        public static float ReadFloatLE(this byte[] d, int o = 0) => BitConverter.ToSingle(d, o);
        /// <summary>byte[] 에서 double (Big-Endian) 을 읽습니다.</summary>
        public static double ReadDoubleBE(this byte[] d, int o = 0)
        { var t = d.AsSpan(o, 8).ToArray(); if (BitConverter.IsLittleEndian) Array.Reverse(t); return BitConverter.ToDouble(t); }
        /// <summary>byte[] 에서 double (Little-Endian) 을 읽습니다.</summary>
        public static double ReadDoubleLE(this byte[] d, int o = 0) => BitConverter.ToDouble(d, o);

        /// <summary>
        /// byte[] (16바이트 Little-Endian) 에서 decimal 을 읽습니다.
        /// <example><code>
        /// // 금융 데이터 읽기
        /// decimal price = priceBytes.ReadDecimalLE(offset:0);   // 1234.56m
        ///
        /// // Modbus 확장 레지스터에서 decimal 읽기 (16바이트)
        /// decimal amount = responseBytes.ReadDecimalLE(offset:3);
        /// </code></example>
        /// </summary>
        public static decimal ReadDecimalLE(this byte[] d, int o = 0)
        {
            if (d.Length < o + 16) throw new ArgumentOutOfRangeException(nameof(o));
            return new decimal(new[]
            {
                BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o,    4)),
                BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o+4,  4)),
                BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o+8,  4)),
                BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o+12, 4)),
            });
        }

        /// <summary>
        /// byte[] (16바이트 Big-Endian) 에서 decimal 을 읽습니다.
        /// <example><code>
        /// decimal v = responseBytes.ReadDecimalBE(offset:0);
        /// </code></example>
        /// </summary>
        public static decimal ReadDecimalBE(this byte[] d, int o = 0)
        {
            if (d.Length < o + 16) throw new ArgumentOutOfRangeException(nameof(o));
            return new decimal(new[]
            {
                BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(o,    4)),
                BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(o+4,  4)),
                BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(o+8,  4)),
                BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(o+12, 4)),
            });
        }

        // ────────────────────────────────────────────────────────────
        //  [ 4 ] 값 → byte[] 변환
        // ────────────────────────────────────────────────────────────

        /// <summary>short → Big-Endian byte[2]</summary>
        public static byte[] ToBigEndianBytes(this short v) { var b = new byte[2]; BinaryPrimitives.WriteInt16BigEndian(b, v); return b; }
        /// <summary>ushort → Big-Endian byte[2]</summary>
        public static byte[] ToBigEndianBytes(this ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, v); return b; }
        /// <summary>int → Big-Endian byte[4]</summary>
        public static byte[] ToBigEndianBytes(this int v) { var b = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, v); return b; }
        /// <summary>uint → Big-Endian byte[4]</summary>
        public static byte[] ToBigEndianBytes(this uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); return b; }
        /// <summary>long → Big-Endian byte[8]</summary>
        public static byte[] ToBigEndianBytes(this long v) { var b = new byte[8]; BinaryPrimitives.WriteInt64BigEndian(b, v); return b; }
        /// <summary>short → Little-Endian byte[2]</summary>
        public static byte[] ToLittleEndianBytes(this short v) { var b = new byte[2]; BinaryPrimitives.WriteInt16LittleEndian(b, v); return b; }
        /// <summary>ushort → Little-Endian byte[2]</summary>
        public static byte[] ToLittleEndianBytes(this ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); return b; }
        /// <summary>int → Little-Endian byte[4]</summary>
        public static byte[] ToLittleEndianBytes(this int v) { var b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); return b; }
        /// <summary>uint → Little-Endian byte[4]</summary>
        public static byte[] ToLittleEndianBytes(this uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }
        /// <summary>long → Little-Endian byte[8]</summary>
        public static byte[] ToLittleEndianBytes(this long v) { var b = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, v); return b; }

        // ────────────────────────────────────────────────────────────
        //  [ 5 ] decimal ↔ byte[] 변환
        //  .NET decimal = 16바이트
        //  GetBits → int[4] = [lo(32), mid(32), hi(32), flags(32)]
        //  flags: bit31=부호, bits16-23=소수점자릿수(0~28)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// decimal → 16바이트 (Little-Endian, .NET 기본 형식).
        /// <para>내부: GetBits [lo,mid,hi,flags] 각 int 를 LE 로 직렬화.</para>
        /// <example><code>
        /// byte[] raw = 123.456m.ToBytes();
        /// // → 40 E2 01 00 00 00 00 00 00 00 00 00 00 00 03 00
        /// //   [lo=0x0001E240][mid=0][hi=0][flags=0x00030000(scale=3)]
        ///
        /// decimal v = raw.ReadDecimalLE();  // 123.456m 복원
        /// </code></example>
        /// </summary>
        public static byte[] ToBytes(this decimal value)
        {
            var bits = decimal.GetBits(value);
            var buf = new byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), bits[0]);  // lo
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4, 4), bits[1]);  // mid
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8, 4), bits[2]);  // hi
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(12, 4), bits[3]);  // flags
            return buf;
        }

        /// <summary>
        /// decimal → 16바이트 (Big-Endian).
        /// <example><code>
        /// byte[] raw = 123.456m.ToBigEndianBytes();
        /// decimal v  = raw.ReadDecimalBE();  // 123.456m 복원
        /// </code></example>
        /// </summary>
        public static byte[] ToBigEndianBytes(this decimal value)
        {
            var bits = decimal.GetBits(value);
            var buf = new byte[16];
            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0, 4), bits[0]);
            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(4, 4), bits[1]);
            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(8, 4), bits[2]);
            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(12, 4), bits[3]);
            return buf;
        }

        // ────────────────────────────────────────────────────────────
        //  [ 6 ] decimal[] ↔ byte[] 변환
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// decimal[] → byte[] (각 원소 16바이트 LE, 총 count × 16바이트).
        /// <example><code>
        /// decimal[] prices = { 1234.56m, 789.00m, 100.50m };
        /// byte[] raw = prices.ToLEBytes();  // 48바이트
        ///
        /// // 복원
        /// decimal[] restored = raw.ToDecimalLEArray(offset:0, count:3);
        /// </code></example>
        /// </summary>
        public static byte[] ToLEBytes(this decimal[] values)
        {
            var buf = new byte[values.Length * 16];
            for (int i = 0; i < values.Length; i++)
            {
                var bits = decimal.GetBits(values[i]);
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i * 16, 4), bits[0]);
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i * 16 + 4, 4), bits[1]);
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i * 16 + 8, 4), bits[2]);
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i * 16 + 12, 4), bits[3]);
            }
            return buf;
        }

        /// <summary>
        /// decimal[] → byte[] (각 원소 16바이트 BE, 총 count × 16바이트).
        /// <example><code>
        /// decimal[] prices = { 1234.56m, 789.00m };
        /// byte[] raw = prices.ToBEBytes();  // 32바이트
        ///
        /// decimal[] restored = raw.ToDecimalBEArray(offset:0, count:2);
        /// </code></example>
        /// </summary>
        public static byte[] ToBEBytes(this decimal[] values)
        {
            var buf = new byte[values.Length * 16];
            for (int i = 0; i < values.Length; i++)
            {
                var bits = decimal.GetBits(values[i]);
                BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(i * 16, 4), bits[0]);
                BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(i * 16 + 4, 4), bits[1]);
                BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(i * 16 + 8, 4), bits[2]);
                BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(i * 16 + 12, 4), bits[3]);
            }
            return buf;
        }

        /// <summary>
        /// byte[] → decimal[] (각 원소 16바이트 LE).
        /// <example><code>
        /// byte[] raw = { /* 48바이트 */ };
        /// decimal[] prices = raw.ToDecimalLEArray(offset:0, count:3);
        /// // prices[0]=1234.56m, prices[1]=789.00m, prices[2]=100.50m
        ///
        /// // 버퍼 중간에서 읽기
        /// decimal[] values = raw.ToDecimalLEArray(offset:20, count:2);
        /// </code></example>
        /// </summary>
        public static decimal[] ToDecimalLEArray(this byte[] d, int offset, int count)
        {
            if (d.Length < offset + count * 16)
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"버퍼 크기 부족: 필요={offset + count * 16} 실제={d.Length}");
            var a = new decimal[count];
            for (int i = 0; i < count; i++) a[i] = d.ReadDecimalLE(offset + i * 16);
            return a;
        }

        /// <summary>
        /// byte[] → decimal[] (각 원소 16바이트 BE).
        /// <example><code>
        /// decimal[] values = raw.ToDecimalBEArray(offset:0, count:2);
        /// </code></example>
        /// </summary>
        public static decimal[] ToDecimalBEArray(this byte[] d, int offset, int count)
        {
            if (d.Length < offset + count * 16)
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"버퍼 크기 부족: 필요={offset + count * 16} 실제={d.Length}");
            var a = new decimal[count];
            for (int i = 0; i < count; i++) a[i] = d.ReadDecimalBE(offset + i * 16);
            return a;
        }

        // ────────────────────────────────────────────────────────────
        //  [ 7 ] decimal 내부 구조 분해
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// decimal 의 부호·소수점 자릿수·내부 정수값을 분해합니다.
        /// <example><code>
        /// var info = 123.456m.Decompose();
        /// Console.WriteLine(info);
        /// // decimal 123.456  Sign=양수  Scale=3
        /// //   GetBits = [0x0001E240, 0x00000000, 0x00000000, 0x00030000]
        /// //   Bytes(LE) = 40 E2 01 00 00 00 00 00 00 00 00 00 00 00 03 00
        ///
        /// var info2 = (-0.01m).Decompose();
        /// // IsNegative=true, Scale=2
        ///
        /// // 최대값 확인
        /// var maxInfo = decimal.MaxValue.Decompose();
        /// // decimal 79228162514264337593543950335
        /// </code></example>
        /// </summary>
        public static DecimalInfo Decompose(this decimal value)
        {
            var bits = decimal.GetBits(value);
            int flags = bits[3];
            bool isNeg = (flags & unchecked((int)0x80000000)) != 0;
            int scale = (flags >> 16) & 0x7F;
            ulong lo96 = ((ulong)(uint)bits[1] << 32) | (uint)bits[0];
            uint hi96 = (uint)bits[2];
            return new DecimalInfo(value, isNeg, scale, lo96, hi96, bits);
        }

        // ────────────────────────────────────────────────────────────
        //  [ 8 ] byte[] 유틸
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// byte[] → HEX 문자열.
        /// <example><code>
        /// string hex = bytes.ToHexString();         // "AA BB CC DD"
        /// string hex = bytes.ToHexString("-");      // "AA-BB-CC-DD"
        /// string hex = bytes.ToHexString("");       // "AABBCCDD"
        /// </code></example>
        /// </summary>
        public static string ToHexString(this byte[] d, string sep = " ")
            => string.Join(sep, d.Select(b => b.ToString("X2")));

        /// <summary>
        /// ushort 바이트 순서를 뒤집습니다.
        /// <example><code>
        /// ushort swapped = ((ushort)0x1234).SwapEndian();  // 0x3412
        /// </code></example>
        /// </summary>
        public static ushort SwapEndian(this ushort v) => BinaryPrimitives.ReverseEndianness(v);

        /// <summary>uint 바이트 순서를 뒤집습니다.</summary>
        public static uint SwapEndian(this uint v) => BinaryPrimitives.ReverseEndianness(v);

        /// <summary>
        /// 호스트 바이트 순서 → 네트워크 바이트 순서 (Big-Endian) 변환.
        /// <example><code>
        /// ushort netShort = ((ushort)12345).ToNetworkOrder();
        /// uint   netUint  = ((uint)123456789).ToNetworkOrder();
        /// </code></example>
        /// </summary>
        public static ushort ToNetworkOrder(this ushort v)
            => BitConverter.IsLittleEndian ? v.SwapEndian() : v;
        /// <summary>호스트 → 네트워크 바이트 순서 변환 (uint).</summary>
        public static uint ToNetworkOrder(this uint v)
            => BitConverter.IsLittleEndian ? v.SwapEndian() : v;

        // ────────────────────────────────────────────────────────────
        //  [ 9 ] Struct 확장 — Marshal 기반
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// byte[] → 구조체 (Marshal 역직렬화).
        /// [StructLayout(LayoutKind.Sequential, Pack=1)] 구조체 권장.
        /// <example><code>
        /// [StructLayout(LayoutKind.Sequential, Pack=1)]
        /// struct Packet { public byte Header; public ushort Length; public float Value; }
        ///
        /// Packet pkt  = rawBytes.To<Packet>();
        /// Packet pkt2 = rawBytes.To<Packet>(offset:10);  // offset 지정
        /// </code></example>
        /// </summary>
        public static T To<T>(this byte[] bytes, int offset = 0) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (offset + size > bytes.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(bytes, offset, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally { if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr); }
        }

        /// <summary>
        /// ReadOnlySpan → 구조체 (zero-copy, 고성능).
        /// <example><code>
        /// ReadOnlySpan<byte> span = buffer.AsSpan(offset, Marshal.SizeOf<Packet>());
        /// Packet pkt = span.ToStruct<Packet>();
        /// </code></example>
        /// </summary>
        public static T ToStruct<T>(this ReadOnlySpan<byte> span) where T : struct
        {
            if (span.Length < Marshal.SizeOf<T>()) throw new ArgumentException("Span 크기 부족");
            return MemoryMarshal.Read<T>(span);
        }

        /// <summary>
        /// 구조체 → byte[] (Marshal 직렬화).
        /// <example><code>
        /// var pkt = new Packet { Header=0xAA, Length=10, Value=3.14f };
        /// byte[] raw = pkt.ToBytes();  // 7바이트 (Pack=1 기준)
        /// </code></example>
        /// </summary>
        public static byte[] ToBytes<T>(this T structure) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buf = new byte[size];
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(structure, ptr, false);
                Marshal.Copy(ptr, buf, 0, size);
            }
            finally { if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr); }
            return buf;
        }

        /// <summary>
        /// 구조체의 HEX 덤프 + 필드 목록을 문자열로 반환합니다 (디버그용).
        /// <example><code>
        /// Console.WriteLine(pkt.Dump());
        /// // [Packet]  7 bytes
        /// //   HEX  : AA 0A 00 C3 F5 48 40
        /// //   Header   (Byte   ) = 170
        /// //   Length   (UInt16 ) = 10
        /// //   Value    (Single ) = 3.14
        /// </code></example>
        /// </summary>
        public static string Dump<T>(this T structure) where T : struct
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{typeof(T).Name}]  {Marshal.SizeOf<T>()} bytes");
            sb.AppendLine($"  HEX  : {structure.ToBytes().ToHexString()}");
            foreach (var f in typeof(T).GetFields())
                sb.AppendLine($"  {f.Name,-22} ({f.FieldType.Name,-8}) = {f.GetValue(structure)}");
            return sb.ToString();
        }

        /// <summary>
        /// 두 구조체를 바이트 단위로 동등 비교합니다.
        /// <example><code>
        /// bool same = pkt1.ByteEquals(pkt2);  // 모든 바이트 일치 여부
        /// </code></example>
        /// </summary>
        public static bool ByteEquals<T>(this T a, T b) where T : struct
            => a.ToBytes().SequenceEqual(b.ToBytes());
    }

    // ── decimal 분해 결과 ─────────────────────────────────────────────

    /// <summary>
    /// decimal 내부 구조 분해 결과.
    /// <see cref="BinaryExtensions.Decompose"/> 로 생성됩니다.
    /// </summary>
    public record DecimalInfo(
        decimal Value,
        bool IsNegative,
        int Scale,      // 소수점 자릿수 0~28
        ulong Lo96,       // 하위 64비트 정수
        uint Hi96,       // 상위 32비트 정수
        int[] Bits)       // GetBits 원본 [lo, mid, hi, flags]
    {
        private string SignStr => IsNegative ? "음수" : "양수";

        /// <summary>분해 결과를 읽기 쉬운 형태로 반환합니다.</summary>
        public override string ToString()
            => $"decimal {Value:G}  Sign={SignStr}  Scale={Scale}\n" +
               $"  GetBits = [{string.Join(", ", Bits.Select(b => $"0x{b:X8}"))}]\n" +
               $"  Bytes(LE) = {Value.ToBytes().ToHexString()}";
    }
}