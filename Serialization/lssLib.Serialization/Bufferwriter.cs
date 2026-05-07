// ====================================================================
//  lssLib.Binary — BufferWriter
//  바이너리 프레임 빌더 (BufferParser 의 반대 방향, 쓰기 전용)
//
//  [기본 사용]
//  var bw = BufferWriter.Create(capacity:64);
//  bw.WriteUInt8(0xAA)
//    .WriteUInt16BE(256)
//    .WriteFloatBE(3.14f)
//    .WriteDecimalLE(123.456m)
//    .WriteStringAscii("Hello", fixedLen:16);
//  byte[] frame = bw.ToArray();
//
//  [decimal 배열]
//  decimal[] prices = { 1234.56m, 789.00m };
//  bw.WriteDecimalLEArray(prices);     // 각 원소 16바이트
//
//  [PatchByte — 체크섬 사후 삽입]
//  bw.WritePad(1);                     // 체크섬 자리 예약
//  byte cs = bw.ToArray().Sum8(0, bw.Length - 1);
//  bw.PatchByte(offset: bw.Length - 1, cs);
//
//  [PatchUInt16BE — Length 필드 사후 삽입]
//  bw.PatchUInt16BE(offset:1, (ushort)(bw.Length - 3));
//
//  [ToParser — 왕복 검증]
//  decimal v = bw.ToParser().ReadDecimalLE(0);
// ====================================================================

using System.Buffers.Binary;
using System.Text;

namespace lssLib.Binary
{
    /// <summary>
    /// 바이너리 프레임 빌더 (쓰기 방향).
    /// <see cref="BufferParser"/> 의 반대 역할. 체이닝 API 로 프레임을 생성합니다.
    /// <example><code>
    /// // 기본 사용
    /// byte[] frame = BufferWriter.Create()
    ///     .WriteUInt8(0xAA)
    ///     .WriteUInt16BE(256)
    ///     .WriteFloatBE(3.14f)
    ///     .WriteDecimalLE(123.456m)        // 16바이트
    ///     .WriteStringAscii("Hello", 16)   // 16바이트 (패딩)
    ///     .WritePad(4)
    ///     .ToArray();
    ///
    /// // decimal 배열 쓰기
    /// decimal[] prices = { 1234.56m, 789.00m, 100.50m };
    /// byte[] raw = BufferWriter.Create()
    ///     .WriteUInt8(0xAA)
    ///     .WriteDecimalLEArray(prices)     // 48바이트
    ///     .ToArray();  // 총 49바이트
    ///
    /// // PatchByte — 체크섬 나중에 삽입
    /// var bw = BufferWriter.Create()
    ///     .WriteUInt8(0xAA).WriteFloatBE(3.14f).WritePad(1);
    /// byte cs = bw.ToArray().Sum8(0, bw.Length - 1);
    /// bw.PatchByte(offset: bw.Length - 1, cs);
    ///
    /// // ToParser — 쓴 내용을 바로 검증
    /// decimal v = BufferWriter.Create()
    ///     .WriteDecimalLE(123.456m)
    ///     .ToParser()
    ///     .ReadDecimalLE(0);   // 123.456m (손실 없음)
    /// </code></example>
    /// </summary>
    public sealed class BufferWriter
    {
        private readonly List<byte> _buf;

        private BufferWriter(int capacity) => _buf = new List<byte>(capacity);

        /// <summary>
        /// 새 BufferWriter 를 생성합니다.
        /// <example><code>
        /// var bw = BufferWriter.Create();
        /// var bw = BufferWriter.Create(capacity:128);
        /// </code></example>
        /// </summary>
        public static BufferWriter Create(int capacity = 64) => new(capacity);

        /// <summary>현재 기록된 바이트 수.</summary>
        public int Length => _buf.Count;

        // ── 정수 쓰기 ─────────────────────────────────────────────────

        /// <summary>sbyte (1바이트) 를 씁니다.</summary>
        public BufferWriter WriteInt8(sbyte v) { _buf.Add((byte)v); return this; }

        /// <summary>
        /// byte (1바이트) 를 씁니다.
        /// <example><code>
        /// bw.WriteUInt8(0xAA);  // STX
        /// </code></example>
        /// </summary>
        public BufferWriter WriteUInt8(byte v) { _buf.Add(v); return this; }

        /// <summary>
        /// short (2바이트, Big-Endian) 를 씁니다.
        /// <example><code>
        /// bw.WriteInt16BE(-256);    // [0xFF, 0x00]
        /// </code></example>
        /// </summary>
        public BufferWriter WriteInt16BE(short v) { _buf.Add((byte)(v >> 8)); _buf.Add((byte)(v & 0xFF)); return this; }

        /// <summary>short (2바이트, Little-Endian) 를 씁니다.</summary>
        public BufferWriter WriteInt16LE(short v) { _buf.Add((byte)(v & 0xFF)); _buf.Add((byte)(v >> 8)); return this; }

        /// <summary>
        /// ushort (2바이트, Big-Endian) 를 씁니다.
        /// <example><code>
        /// bw.WriteUInt16BE(0x0100);  // [0x01, 0x00]
        /// </code></example>
        /// </summary>
        public BufferWriter WriteUInt16BE(ushort v) { _buf.Add((byte)(v >> 8)); _buf.Add((byte)(v & 0xFF)); return this; }

        /// <summary>ushort (2바이트, Little-Endian) 를 씁니다.</summary>
        public BufferWriter WriteUInt16LE(ushort v) { _buf.Add((byte)(v & 0xFF)); _buf.Add((byte)(v >> 8)); return this; }

        /// <summary>int (4바이트, Big-Endian) 를 씁니다.</summary>
        public BufferWriter WriteInt32BE(int v) { var b = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, v); _buf.AddRange(b); return this; }
        /// <summary>int (4바이트, Little-Endian) 를 씁니다.</summary>
        public BufferWriter WriteInt32LE(int v) { var b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); _buf.AddRange(b); return this; }
        /// <summary>uint (4바이트, Big-Endian) 를 씁니다.</summary>
        public BufferWriter WriteUInt32BE(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); _buf.AddRange(b); return this; }
        /// <summary>uint (4바이트, Little-Endian) 를 씁니다.</summary>
        public BufferWriter WriteUInt32LE(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); _buf.AddRange(b); return this; }
        /// <summary>long (8바이트, Big-Endian) 를 씁니다.</summary>
        public BufferWriter WriteInt64BE(long v) { var b = new byte[8]; BinaryPrimitives.WriteInt64BigEndian(b, v); _buf.AddRange(b); return this; }
        /// <summary>long (8바이트, Little-Endian) 를 씁니다.</summary>
        public BufferWriter WriteInt64LE(long v) { var b = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, v); _buf.AddRange(b); return this; }
        /// <summary>ulong (8바이트, Big-Endian) 를 씁니다.</summary>
        public BufferWriter WriteUInt64BE(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(b, v); _buf.AddRange(b); return this; }
        /// <summary>ulong (8바이트, Little-Endian) 를 씁니다.</summary>
        public BufferWriter WriteUInt64LE(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, v); _buf.AddRange(b); return this; }

        // ── 실수 쓰기 ─────────────────────────────────────────────────

        /// <summary>
        /// float (4바이트, Big-Endian, IEEE 754) 를 씁니다.
        /// <example><code>
        /// bw.WriteFloatBE(3.14f);  // [0x40, 0x48, 0xF5, 0xC3]
        /// </code></example>
        /// </summary>
        public BufferWriter WriteFloatBE(float v)
        {
            var b = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            _buf.AddRange(b);
            return this;
        }

        /// <summary>float (4바이트, Little-Endian, IEEE 754) 를 씁니다.</summary>
        public BufferWriter WriteFloatLE(float v) { _buf.AddRange(BitConverter.GetBytes(v)); return this; }

        /// <summary>double (8바이트, Big-Endian) 를 씁니다.</summary>
        public BufferWriter WriteDoubleBE(double v)
        {
            var b = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            _buf.AddRange(b);
            return this;
        }

        /// <summary>double (8바이트, Little-Endian) 를 씁니다.</summary>
        public BufferWriter WriteDoubleLE(double v) { _buf.AddRange(BitConverter.GetBytes(v)); return this; }

        // ── decimal 쓰기 (16바이트) ───────────────────────────────────

        /// <summary>
        /// decimal (16바이트, Little-Endian) 을 씁니다.
        /// GetBits [lo,mid,hi,flags] 각 4바이트 LE 순서로 직렬화합니다.
        /// <example><code>
        /// bw.WriteDecimalLE(123.456m);
        /// // 40 E2 01 00 00 00 00 00 00 00 00 00 00 00 03 00
        ///
        /// // 왕복 검증
        /// decimal v = BufferWriter.Create()
        ///     .WriteDecimalLE(123.456m)
        ///     .ToParser()
        ///     .ReadDecimalLE(0);  // 123.456m (손실 없음)
        /// </code></example>
        /// </summary>
        public BufferWriter WriteDecimalLE(decimal v)
        {
            var bits = decimal.GetBits(v);
            foreach (var i in bits)
            {
                var b = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(b, i);
                _buf.AddRange(b);
            }
            return this;
        }

        /// <summary>
        /// decimal (16바이트, Big-Endian) 을 씁니다.
        /// <example><code>
        /// bw.WriteDecimalBE(123.456m);
        /// </code></example>
        /// </summary>
        public BufferWriter WriteDecimalBE(decimal v)
        {
            var bits = decimal.GetBits(v);
            foreach (var i in bits)
            {
                var b = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(b, i);
                _buf.AddRange(b);
            }
            return this;
        }

        /// <summary>
        /// decimal[] 배열 (LE) 을 씁니다. 각 원소 16바이트.
        /// <example><code>
        /// decimal[] prices = { 1234.56m, 789.00m, 100.50m };
        /// bw.WriteDecimalLEArray(prices);  // 48바이트 추가
        /// </code></example>
        /// </summary>
        public BufferWriter WriteDecimalLEArray(decimal[] values)
        { foreach (var v in values) WriteDecimalLE(v); return this; }

        /// <summary>
        /// decimal[] 배열 (BE) 을 씁니다. 각 원소 16바이트.
        /// <example><code>
        /// bw.WriteDecimalBEArray(prices);
        /// </code></example>
        /// </summary>
        public BufferWriter WriteDecimalBEArray(decimal[] values)
        { foreach (var v in values) WriteDecimalBE(v); return this; }

        // ── 논리 / 문자열 쓰기 ───────────────────────────────────────

        /// <summary>
        /// bool 을 1바이트 (true=1, false=0) 로 씁니다.
        /// </summary>
        public BufferWriter WriteBool(bool v) { _buf.Add((byte)(v ? 1 : 0)); return this; }

        /// <summary>
        /// ASCII 문자열을 씁니다. fixedLen 지정 시 나머지를 \0 으로 패딩합니다.
        /// <example><code>
        /// bw.WriteStringAscii("Hello");        // 5바이트
        /// bw.WriteStringAscii("Hello", 16);    // 16바이트 (11바이트 \0 패딩)
        /// </code></example>
        /// </summary>
        public BufferWriter WriteStringAscii(string s, int fixedLen = 0)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            if (fixedLen > 0)
            {
                var padded = new byte[fixedLen];
                Array.Copy(b, padded, Math.Min(b.Length, fixedLen));
                _buf.AddRange(padded);
            }
            else _buf.AddRange(b);
            return this;
        }

        /// <summary>
        /// UTF-8 문자열을 씁니다. 한글 등 멀티바이트 지원.
        /// <example><code>
        /// bw.WriteStringUtf8("안녕하세요", fixedLen:32);
        /// </code></example>
        /// </summary>
        public BufferWriter WriteStringUtf8(string s, int fixedLen = 0)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            if (fixedLen > 0)
            {
                var padded = new byte[fixedLen];
                Array.Copy(b, padded, Math.Min(b.Length, fixedLen));
                _buf.AddRange(padded);
            }
            else _buf.AddRange(b);
            return this;
        }

        // ── 원시 쓰기 ─────────────────────────────────────────────────

        /// <summary>byte[] 를 그대로 씁니다.</summary>
        public BufferWriter WriteRaw(byte[] data) { _buf.AddRange(data); return this; }

        /// <summary>ReadOnlySpan 을 씁니다.</summary>
        public BufferWriter WriteRaw(ReadOnlySpan<byte> data) { _buf.AddRange(data.ToArray()); return this; }

        /// <summary>
        /// N바이트 패딩 (기본값 0x00) 을 씁니다. 체크섬·예약 필드 자리 확보에 사용합니다.
        /// <example><code>
        /// bw.WritePad(1);   // 체크섬 자리 예약
        /// bw.WritePad(4);   // 4바이트 0x00
        /// bw.WritePad(2, 0xFF);  // 2바이트 0xFF
        /// </code></example>
        /// </summary>
        public BufferWriter WritePad(int count, byte value = 0x00)
        { _buf.AddRange(Enumerable.Repeat(value, count)); return this; }

        // ── 특정 위치 덮어쓰기 ────────────────────────────────────────

        /// <summary>
        /// 특정 offset 의 바이트를 덮어씁니다.
        /// 체크섬 자리를 미리 예약하고 나중에 계산 값을 삽입할 때 사용합니다.
        /// <example><code>
        /// var bw = BufferWriter.Create()
        ///     .WriteUInt8(0xAA)
        ///     .WriteFloatBE(3.14f)
        ///     .WritePad(1);           // 체크섬 자리 예약
        ///
        /// byte cs = bw.ToArray().Sum8(0, bw.Length - 1);
        /// bw.PatchByte(offset: bw.Length - 1, cs);   // 체크섬 삽입
        /// byte[] frame = bw.ToArray();
        /// </code></example>
        /// </summary>
        public BufferWriter PatchByte(int offset, byte value)
        {
            if (offset < 0 || offset >= _buf.Count)
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"offset={offset} 범위 초과 (현재 크기={_buf.Count})");
            _buf[offset] = value;
            return this;
        }

        /// <summary>
        /// 특정 offset 에 ushort (Big-Endian) 를 덮어씁니다.
        /// Length 필드를 프레임 생성 후 실제 크기로 채울 때 사용합니다.
        /// <example><code>
        /// var bw = BufferWriter.Create()
        ///     .WriteUInt8(0xAA)
        ///     .WriteUInt16BE(0)        // Length 자리 예약
        ///     .WriteFloatBE(3.14f);
        ///
        /// // 실제 데이터 길이 = 전체 - STX(1) - Length(2)
        /// bw.PatchUInt16BE(offset:1, (ushort)(bw.Length - 3));
        /// </code></example>
        /// </summary>
        public BufferWriter PatchUInt16BE(int offset, ushort value)
        {
            if (offset + 2 > _buf.Count)
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"offset={offset} 2바이트 쓰기 불가 (현재 크기={_buf.Count})");
            _buf[offset] = (byte)(value >> 8);
            _buf[offset + 1] = (byte)(value & 0xFF);
            return this;
        }

        // ── 결과 반환 ─────────────────────────────────────────────────

        /// <summary>
        /// 기록된 내용을 byte[] 로 반환합니다.
        /// <example><code>
        /// byte[] frame = bw.ToArray();
        /// </code></example>
        /// </summary>
        public byte[] ToBytes() => _buf.ToArray();

        /// <summary>
        /// 기록된 내용을 BufferParser 로 변환합니다.
        /// 쓴 내용을 즉시 읽어서 왕복 검증할 때 사용합니다.
        /// <example><code>
        /// // decimal 왕복 검증
        /// decimal original = 999999.999999999999999999999m;
        /// decimal restored = BufferWriter.Create()
        ///     .WriteDecimalLE(original)
        ///     .ToParser()
        ///     .ReadDecimalLE(0);
        /// bool ok = original == restored;  // true (손실 없음)
        /// </code></example>
        /// </summary>
        public BufferParser ToParser() => BufferParser.From(ToBytes());

        /// <summary>
        /// 기록된 내용을 HEX 문자열로 반환합니다.
        /// <example><code>
        /// Console.WriteLine(bw.ToHex());  // "AA 03 41 20 00 00"
        /// </code></example>
        /// </summary>
        public string ToHex(string sep = " ")
            => string.Join(sep, _buf.Select(b => b.ToString("X2")));

        /// <summary>"BufferWriter[N bytes]" 형식 문자열.</summary>
        public override string ToString() => $"BufferWriter[{_buf.Count} bytes]";
    }
}