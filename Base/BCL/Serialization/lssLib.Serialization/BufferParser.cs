// ====================================================================
// lssLib.Binary — BufferParser
//  Node-RED buffer-parser 호환 바이너리 파서
//
//  [팩토리]
// var bp = BufferParser.From(bytes);
// var bp = BufferParser.FromHex("AA BB CC DD");
//  var bp = bytes.ToParser();                    ← 확장 메서드
//
//  [직접 읽기]
// byte    v = bp.ReadUInt8(0);
// ushort  v = bp.ReadUInt16BE(2);
// float   v = bp.ReadFloatBE(4);
//  decimal d = bp.ReadDecimalLE(8);             ← 16바이트 고정소수점
// bool    b = bp.ReadBit(offset:1, bit:3);
// float[] a = bp.ReadFloatBEArray(0, count:4);
// decimal[]a = bp.ReadDecimalLEArray(0, count:3);
//
//  [스키마 파싱]
//  var schema = new BufSchema()
//      .Add("stx",    BufType.UInt8,      offset:0)
//          .Add("price",  BufType.DecimalLE,  offset:1)   // 16바이트
//      .Add("qty",    BufType.UInt32LE,   offset:17);
//  var result = bp.Parse(schema);
//  decimal price = result.Get<decimal>("price");
//
//  [훅 확장 메서드 — BinaryExtensions.cs]
//  bytes.ToParser().WithLog(Console.WriteLine)
//  bytes.ToParser().WithXorDecrypt(0xFF)
//  bytes.ToParser().OnParseDone((r,s) => Verify(r))
// ====================================================================

using System.Buffers.Binary;
using System.Text;
using System.Windows.Media.Animation;

namespace lssLib.Binary
{
    // ── BufferParser 본체 ─────────────────────────────────────────────
   /// <summary>
     /// Node-RED buffer-parser 호환 바이너리 버퍼 파서.
     /// <para>팩토리 메서드로 생성하고 ReadXxx 메서드나 Parse(schema) 로 값을 읽습니다.</para>
    /// <example><code>
     /// // 1. 팩토리로 생성
    /// var bp = BufferParser.From(rawBytes);
    /// var bp = BufferParser.FromHex("AA 03 00 0C 41 20 00 00");
      /// var bp = bytes.ToParser();   // 확장 메서드
    ///
      /// // 2. 직접 읽기
    /// byte    stx   = bp.ReadUInt8(0);
    /// ushort  len   = bp.ReadUInt16BE(2);
    /// float   val   = bp.ReadFloatBE(4);
    /// decimal price = bp.ReadDecimalLE(8);
    ///
      /// // 3. 스키마 파싱
    /// var schema = new BufSchema()
    ///     .Then("stx",   BufType.UInt8)
    ///     .Then("val",   BufType.FloatBE)
    ///     .Then("price", BufType.DecimalLE);
    /// var result = bp.Parse(schema);
    /// decimal p = result.Get<decimal>("price");
    ///
      /// // 4. 훅 체이닝
    /// var result = bytes.ToParser()
    ///     .WithLog(Console.WriteLine)
    ///     .WithXorDecrypt(0xAA)
    ///     .Parse(schema);
    /// </code></example>
    /// </summary>
   public sealed class BufferParser
   {
       internal readonly byte[] Raw;

           /// <summary>버퍼 전체 바이트 수.</summary>
       public int Length => Raw.Length;

          // 확장 메서드에서 설정하는 훅 델리게이트
       internal Action<int, int, string>? HookBefore;
       internal Action<int, int, string, object>? HookAfter;
       internal Action<BufResult, BufSchema>? HookParsed;
       internal Func<byte[], byte[]>? Preprocessor;

       private BufferParser(byte[] raw) => Raw = raw;

           // ── 팩토리 ────────────────────────────────────────────────────

       /// <summary>
          /// byte[] 로부터 파서를 생성합니다.
       /// <example><code>
       /// byte[] raw = { 0xAA, 0x03, 0x00, 0x10 };
       /// var bp = BufferParser.From(raw);
       /// byte v = bp.ReadUInt8(0);  // 0xAA
       /// </code></example>
       /// </summary>
       public static BufferParser From(byte[] raw)
           => new(raw ?? throw new ArgumentNullException(nameof(raw)));

       /// <summary>
           /// HEX 문자열로부터 파서를 생성합니다.
           /// 공백·하이픈·0x 접두사를 자동으로 제거합니다.
       /// <example><code>
          /// var bp = BufferParser.FromHex("AA BB CC DD");   // 공백 허용
          /// var bp = BufferParser.FromHex("AA-BB-CC-DD");   // 하이픈 허용
          /// var bp = BufferParser.FromHex("0xAA0xBB");      // 0x 허용
          /// var bp = BufferParser.FromHex("AABBCCDD");      // 그냥 HEX
       /// </code></example>
       /// </summary>
       public static BufferParser FromHex(string hex) => new(HexDecode(hex));

       /// <summary>
           /// Base64 문자열로부터 파서를 생성합니다.
       /// <example><code>
       /// var bp = BufferParser.FromBase64("qgMAEA==");
       /// </code></example>
       /// </summary>
       public static BufferParser FromBase64(string b64)
           => new(Convert.FromBase64String(b64));

       /// <summary>
          /// ReadOnlySpan<byte>로부터 파서를 생성합니다 (복사 발생).
          /// 메모리의 특정 영역을 "복사하지 않고 아주 빠르고 안전하게 읽기 전용으로 들여다보는 창문"
       /// <example><code>
       /// ReadOnlySpan<byte> span = stackalloc byte[] { 0xAA, 0x01 };
       /// var bp = BufferParser.FromSpan(span);
       /// </code></example>
       /// </summary>
       public static BufferParser FromSpan(ReadOnlySpan<byte> s) => new(s.ToArray());

           // ── 실제 파싱 버퍼 (전처리 적용) ──────────────────────────────
       private byte[] Buf => Preprocessor is null ? Raw : Preprocessor(Raw);

          // ── 경계 검사 + 훅 실행 ──────────────────────────────────────
       /// <summary>
          /// offset 위치에서 size 바이트를 읽기 전에 범위를 검사.
          /// 역활
           /// Check: 단순히 "이 위치에서 이만큼 읽어도 안전한가?"만 확인. (방어벽 역할)
          /// Track<T>: Check 기능을 포함하면서, 실제로 데이터를 읽는 로직(fn)을 실행하고 그 전후에 이벤트(Hook)를 발생시킴
       /// </summary>
       private void Check(int o, int size, string nm)
       {
           var buf = Buf;
           if (o < 0 || o + size > buf.Length)
               throw new ArgumentOutOfRangeException(nameof(o),
                   $"[{nm}] offset={o} size={size} bufLen={buf.Length}");
       }

       /// <summary>
          /// offset 위치에서 size 바이트를 읽기 전에 범위를 검사하고, HookBefore/HookAfter 훅을 실행합니다.
          /// 역활
          /// 주로 네트워크 패킷이나 바이너리 파일을 분석할 때, 데이터를 읽는 시점을 가로채서(Hooking) 로그를 남기거나 디버깅하기 위해 사용
          /// // Track을 호출하면:
          /// 1) 범위 검사 수행
          /// 2) HookBefore 실행 (예: "Age 필드 읽기 시작")
          /// 3) fn(buf) 실행 (실제 4바이트 읽기)
          /// 4) HookAfter 실행 (예: "Age 필드 값은 25입니다")
       /// </summary>
       private T Track<T>(int o, int size, string nm, Func<byte[], T> fn)
       {
           var buf = Buf;
           if (o < 0 || o + size > buf.Length)
               throw new ArgumentOutOfRangeException(nameof(o),
                   $"[{nm}] offset={o} size={size} bufLen={buf.Length}");
           HookBefore?.Invoke(o, size, nm);
           T v = fn(buf);
           HookAfter?.Invoke(o, size, nm, v!);
           return v;
       }

      // ── 정수 읽기 ─────────────────────────────────────────────────

      /// <summary>offset 위치에서 sbyte (1바이트, -128~127) 를 읽습니다.</summary>
       public sbyte ReadInt8(int o) => Track(o, 1, "Int8", b => (sbyte)b[o]);
      /// <summary>offset 위치에서 byte (1바이트, 0~255) 를 읽습니다.</summary>
       public byte ReadUInt8(int o) => Track(o, 1, "UInt8", b => b[o]);
      /// <summary>offset 위치에서 short (2바이트, Big-Endian) 를 읽습니다.</summary>
       public short ReadInt16BE(int o) => Track(o, 2, "I16BE", b => BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(o, 2)));
      /// <summary>offset 위치에서 short (2바이트, Little-Endian) 를 읽습니다.</summary>
       public short ReadInt16LE(int o) => Track(o, 2, "I16LE", b => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(o, 2)));
      /// <summary>offset 위치에서 ushort (2바이트, Big-Endian) 를 읽습니다.</summary>
       public ushort ReadUInt16BE(int o) => Track(o, 2, "U16BE", b => BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o, 2)));
      /// <summary>offset 위치에서 ushort (2바이트, Little-Endian) 를 읽습니다.</summary>
       public ushort ReadUInt16LE(int o) => Track(o, 2, "U16LE", b => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o, 2)));
      /// <summary>offset 위치에서 int (4바이트, Big-Endian) 를 읽습니다.</summary>
       public int ReadInt32BE(int o) => Track(o, 4, "I32BE", b => BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o, 4)));
      /// <summary>offset 위치에서 int (4바이트, Little-Endian) 를 읽습니다.</summary>
       public int ReadInt32LE(int o) => Track(o, 4, "I32LE", b => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o, 4)));
      /// <summary>offset 위치에서 uint (4바이트, Big-Endian) 를 읽습니다.</summary>
       public uint ReadUInt32BE(int o) => Track(o, 4, "U32BE", b => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o, 4)));
      /// <summary>offset 위치에서 uint (4바이트, Little-Endian) 를 읽습니다.</summary>
       public uint ReadUInt32LE(int o) => Track(o, 4, "U32LE", b => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4)));
      /// <summary>offset 위치에서 long (8바이트, Big-Endian) 를 읽습니다.</summary>
       public long ReadInt64BE(int o) => Track(o, 8, "I64BE", b => BinaryPrimitives.ReadInt64BigEndian(b.AsSpan(o, 8)));
      /// <summary>offset 위치에서 long (8바이트, Little-Endian) 를 읽습니다.</summary>
       public long ReadInt64LE(int o) => Track(o, 8, "I64LE", b => BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(o, 8)));
      /// <summary>offset 위치에서 ulong (8바이트, Big-Endian) 를 읽습니다.</summary>
       public ulong ReadUInt64BE(int o) => Track(o, 8, "U64BE", b => BinaryPrimitives.ReadUInt64BigEndian(b.AsSpan(o, 8)));
      /// <summary>offset 위치에서 ulong (8바이트, Little-Endian) 를 읽습니다.</summary>
       public ulong ReadUInt64LE(int o) => Track(o, 8, "U64LE", b => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o, 8)));

      // ── 실수 읽기 ─────────────────────────────────────────────────

       /// <summary>
      /// offset 위치에서 float (4바이트, Big-Endian, IEEE 754) 를 읽습니다.
       /// <example><code>
      /// // Modbus, S7, 대부분의 산업용 프로토콜이 Big-Endian 사용
       /// float val = bp.ReadFloatBE(offset:4);
       /// </code></example>
       /// </summary>
       public float ReadFloatBE(int o) => Track(o, 4, "FloatBE",
           b => { var t = b.AsSpan(o, 4).ToArray(); if (BitConverter.IsLittleEndian) Array.Reverse(t); return BitConverter.ToSingle(t); });

       /// <summary>
      /// offset 위치에서 float (4바이트, Little-Endian, IEEE 754) 를 읽습니다.
       /// <example><code>
      /// // x86 시스템, CAN PDO, RS-485 커스텀 장비가 주로 Little-Endian 사용
       /// float val = bp.ReadFloatLE(offset:4);
       /// </code></example>
       /// </summary>
       public float ReadFloatLE(int o) => Track(o, 4, "FloatLE", b => BitConverter.ToSingle(b, o));

      /// <summary>offset 위치에서 double (8바이트, Big-Endian) 를 읽습니다.</summary>
       public double ReadDoubleBE(int o) => Track(o, 8, "DoubleBE",
           b => { var t = b.AsSpan(o, 8).ToArray(); if (BitConverter.IsLittleEndian) Array.Reverse(t); return BitConverter.ToDouble(t); });

      /// <summary>offset 위치에서 double (8바이트, Little-Endian) 를 읽습니다.</summary>
       public double ReadDoubleLE(int o) => Track(o, 8, "DoubleLE", b => BitConverter.ToDouble(b, o));

      // ── decimal 읽기 (16바이트, .NET 고정소수점) ──────────────────

       /// <summary>
      /// offset 위치에서 decimal (16바이트, Little-Endian) 을 읽습니다.
       /// <para>내부 구조: GetBits [lo(4B), mid(4B), hi(4B), flags(4B)] — 각 int LE.</para>
      /// <para>flags: bit31=부호(1=음수), bits16-23=소수점 자릿수(0~28).</para>
      /// <para>정밀도 28~29 유효자리. 금융/회계/정밀 측정값에 사용합니다.</para>
       /// <example><code>
      /// // 예: 123.456 → bytes: 40 E2 01 00 00 00 00 00 00 00 00 00 00 00 03 00
      /// byte[] raw = new decimal(123.456m).ToBytes();  // 확장 메서드
       /// decimal v  = bp.ReadDecimalLE(0);              // 123.456
       ///
      /// // 금융 데이터 (환율, 가격 등)
      /// decimal price    = bp.ReadDecimalLE(offset:0);   // 예: 1234567.89m
      /// decimal quantity = bp.ReadDecimalLE(offset:16);  // 예: 100.000m
       /// decimal total    = price * quantity;
       /// </code></example>
       /// </summary>
       public decimal ReadDecimalLE(int o)
       {
           Check(o, 16, "DecimalLE");
           var b = Buf;
           HookBefore?.Invoke(o, 16, "DecimalLE");
           var result = new decimal(new[]
           {
               BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o,    4)),  // lo
               BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o+4,  4)),  // mid
               BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o+8,  4)),  // hi
               BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o+12, 4)),  // flags
           });
           HookAfter?.Invoke(o, 16, "DecimalLE", result);
           return result;
       }

       /// <summary>
      /// offset 위치에서 decimal (16바이트, Big-Endian) 을 읽습니다.
      /// <para>일부 네트워크 프로토콜이 Big-Endian decimal 을 사용할 때 활용합니다.</para>
       /// <example><code>
       /// decimal v = bp.ReadDecimalBE(offset:0);
       /// </code></example>
       /// </summary>
       public decimal ReadDecimalBE(int o)
       {
           Check(o, 16, "DecimalBE");
           var b = Buf;
           HookBefore?.Invoke(o, 16, "DecimalBE");
           var result = new decimal(new[]
           {
               BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o,    4)),
               BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o+4,  4)),
               BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o+8,  4)),
               BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o+12, 4)),
           });
           HookAfter?.Invoke(o, 16, "DecimalBE", result);
           return result;
       }

       // ── Bool / Bit ────────────────────────────────────────────────

       /// <summary>
      /// offset 위치의 바이트가 0이 아니면 true 를 반환합니다 (1바이트).
       /// <example><code>
       /// bool isActive = bp.ReadBool(offset:3);
       /// </code></example>
       /// </summary>
       public bool ReadBool(int o) => Track(o, 1, "Bool", b => b[o] != 0);

       /// <summary>
      /// offset 바이트의 특정 비트를 읽습니다.
      /// <para>bit=0 이 LSB, bit=7 이 MSB 입니다.</para>
       /// <example><code>
      /// // 바이트 0b10110101 (0xB5) 에서 비트 읽기
       /// bool bit0 = bp.ReadBit(offset:0, bit:0);  // true  (LSB)
       /// bool bit1 = bp.ReadBit(offset:0, bit:1);  // false
       /// bool bit7 = bp.ReadBit(offset:0, bit:7);  // true  (MSB)
       ///
      /// // 상태 레지스터 파싱 예시
      /// bool isError   = bp.ReadBit(offset:2, bit:7);  // bit7 = 오류
      /// bool isRunning = bp.ReadBit(offset:2, bit:0);  // bit0 = 실행 중
       /// </code></example>
       /// </summary>
       public bool ReadBit(int o, int bit)
       {
           if (bit < 0 || bit > 7) throw new ArgumentOutOfRangeException(nameof(bit), "0~7");
           return Track(o, 1, $"Bit[{bit}]", b => (b[o] & (1 << bit)) != 0);
       }

       /// <summary>
      /// 비트 마스크로 원하는 비트만 추출합니다.
       /// <example><code>
      /// // 하위 4비트만 추출 (니블)
       /// byte loNibble = bp.ReadBitField(offset:0, mask:0x0F);
       /// byte hiNibble = bp.ReadBitField(offset:0, mask:0xF0);
       ///
      /// // 특정 비트 필드 추출
       /// byte statusBits = bp.ReadBitField(offset:1, mask:0b00111000);  // bit3~5
       /// </code></example>
       /// </summary>
       public byte ReadBitField(int o, byte mask)
       {
           Check(o, 1, "BitField");
           return (byte)(Buf[o] & mask);
       }

      // ── 문자열 읽기 ───────────────────────────────────────────────

       /// <summary>
      /// offset 위치에서 ASCII 문자열을 읽습니다. 널 종료문자(\0)는 자동 제거됩니다.
       /// <example><code>
       /// string name  = bp.ReadStringAscii(offset:12, length:16);
       /// string model = bp.ReadStringAscii(offset:28, length:8);
       /// </code></example>
       /// </summary>
       public string ReadStringAscii(int o, int n) => Track(o, n, "StrA",
           b => Encoding.ASCII.GetString(b, o, n).TrimEnd('\0'));

       /// <summary>
      /// offset 위치에서 UTF-8 문자열을 읽습니다. 한글 등 멀티바이트 문자 지원.
       /// <example><code>
       /// string name = bp.ReadStringUtf8(offset:4, length:32);
       /// </code></example>
       /// </summary>
       public string ReadStringUtf8(int o, int n) => Track(o, n, "StrU",
           b => Encoding.UTF8.GetString(b, o, n).TrimEnd('\0'));

       /// <summary>
      /// 바이트를 HEX 문자열로 반환합니다. 예: [0xAA, 0xBB] → "AABB"
       /// <example><code>
       /// string mac = bp.ReadStringHex(offset:0, length:6);  // "001122334455"
       /// </code></example>
       /// </summary>
       public string ReadStringHex(int o, int n) => Track(o, n, "StrHex",
           b => BitConverter.ToString(b, o, n).Replace("-", ""));

       /// <summary>
      /// 바이트를 Base64 문자열로 반환합니다.
       /// <example><code>
       /// string b64 = bp.ReadStringBase64(offset:0, length:12);
       /// </code></example>
       /// </summary>
       public string ReadStringBase64(int o, int n) => Track(o, n, "StrB64",
           b => Convert.ToBase64String(b, o, n));

       // ── Raw / Span ────────────────────────────────────────────────

       /// <summary>
      /// 원시 byte[] 를 복사하여 반환합니다.
       /// <example><code>
       /// byte[] payload = bp.ReadRaw(offset:4, length:32);
       /// </code></example>
       /// </summary>
       public byte[] ReadRaw(int o, int n)
       {
           Check(o, n, "Raw");
           var r = new byte[n];
           Array.Copy(Buf, o, r, 0, n);
           return r;
       }
        /// <summary>
        /// 전체 원시 byte[] 를 복사하여 반환합니다.
        /// <example><code>
        /// byte[] allData = bp.ToBytes();
        /// </code></example>
        /// </summary>
        public byte[] ToBytes()
        {
            // 시작 오프셋 0, 전체 길이 Buf.Length를 전달
            return ReadRaw(0, Buf.Length);
        }

        /// <summary>
        /// zero-copy ReadOnlySpan 을 반환합니다 (복사 없음, 고성능).
        /// <example><code>
        /// ReadOnlySpan<byte> span = bp.ReadSpan(offset:0, length:16);
        /// decimal v = MemoryMarshal.Read<decimal>(span);
        /// </code></example>
        /// </summary>
        public ReadOnlySpan<byte> ReadSpan(int o, int n)
       {
           Check(o, n, "Span");
           return Buf.AsSpan(o, n);
       }

      // ── 배열 읽기 ─────────────────────────────────────────────────

      /// <summary>short 배열 읽기 (Big-Endian). 각 원소 2바이트.</summary>
       public short[] ReadInt16BEArray(int o, int c) { var a = new short[c]; for (int i = 0; i < c; i++) a[i] = ReadInt16BE(o + i * 2); return a; }
      /// <summary>short 배열 읽기 (Little-Endian). 각 원소 2바이트.</summary>
       public short[] ReadInt16LEArray(int o, int c) { var a = new short[c]; for (int i = 0; i < c; i++) a[i] = ReadInt16LE(o + i * 2); return a; }
      /// <summary>ushort 배열 읽기 (Big-Endian). 각 원소 2바이트.</summary>
       public ushort[] ReadUInt16BEArray(int o, int c) { var a = new ushort[c]; for (int i = 0; i < c; i++) a[i] = ReadUInt16BE(o + i * 2); return a; }
      /// <summary>ushort 배열 읽기 (Little-Endian). 각 원소 2바이트.</summary>
       public ushort[] ReadUInt16LEArray(int o, int c) { var a = new ushort[c]; for (int i = 0; i < c; i++) a[i] = ReadUInt16LE(o + i * 2); return a; }
      /// <summary>int 배열 읽기 (Big-Endian). 각 원소 4바이트.</summary>
       public int[] ReadInt32BEArray(int o, int c) { var a = new int[c]; for (int i = 0; i < c; i++) a[i] = ReadInt32BE(o + i * 4); return a; }
      /// <summary>int 배열 읽기 (Little-Endian). 각 원소 4바이트.</summary>
       public int[] ReadInt32LEArray(int o, int c) { var a = new int[c]; for (int i = 0; i < c; i++) a[i] = ReadInt32LE(o + i * 4); return a; }
      /// <summary>uint 배열 읽기 (Big-Endian). 각 원소 4바이트.</summary>
       public uint[] ReadUInt32BEArray(int o, int c) { var a = new uint[c]; for (int i = 0; i < c; i++) a[i] = ReadUInt32BE(o + i * 4); return a; }
      /// <summary>uint 배열 읽기 (Little-Endian). 각 원소 4바이트.</summary>
       public uint[] ReadUInt32LEArray(int o, int c) { var a = new uint[c]; for (int i = 0; i < c; i++) a[i] = ReadUInt32LE(o + i * 4); return a; }

       /// <summary>
      /// float 배열 읽기 (Big-Endian). 각 원소 4바이트.
       /// <example><code>
      /// // Modbus FC03 응답에서 4개 레지스터를 float 로 읽기
       /// float[] sensors = bp.ReadFloatBEArray(offset:3, count:4);
      /// // → [온도, 습도, 압력, 유량]
       /// </code></example>
       /// </summary>
       public float[] ReadFloatBEArray(int o, int c) { var a = new float[c]; for (int i = 0; i < c; i++) a[i] = ReadFloatBE(o + i * 4); return a; }

       /// <summary>
      /// float 배열 읽기 (Little-Endian). 각 원소 4바이트.
       /// <example><code>
      /// // CAN PDO 데이터에서 float 4개 읽기 (8바이트 × 2세트)
       /// float[] values = bp.ReadFloatLEArray(offset:0, count:4);
       /// </code></example>
       /// </summary>
       public float[] ReadFloatLEArray(int o, int c) { var a = new float[c]; for (int i = 0; i < c; i++) a[i] = ReadFloatLE(o + i * 4); return a; }

      /// <summary>double 배열 읽기 (Big-Endian). 각 원소 8바이트.</summary>
       public double[] ReadDoubleBEArray(int o, int c) { var a = new double[c]; for (int i = 0; i < c; i++) a[i] = ReadDoubleBE(o + i * 8); return a; }
      /// <summary>double 배열 읽기 (Little-Endian). 각 원소 8바이트.</summary>
       public double[] ReadDoubleLEArray(int o, int c) { var a = new double[c]; for (int i = 0; i < c; i++) a[i] = ReadDoubleLE(o + i * 8); return a; }

       /// <summary>
      /// decimal 배열 읽기 (Little-Endian). 각 원소 16바이트.
      /// <para>총 count × 16 바이트를 소비합니다.</para>
       /// <example><code>
      /// // 3개의 가격 데이터 읽기 (총 48바이트)
       /// decimal[] prices = bp.ReadDecimalLEArray(offset:0, count:3);
       /// // prices[0] = 1234.56m, prices[1] = 789.00m, prices[2] = 100.50m
       ///
      /// // 스키마에서 사용
       /// var schema = new BufSchema()
       ///     .Add("prices", BufType.DecimalLEArray, offset:0, size:3);
       /// decimal[] arr = result.Get<decimal[]>("prices");
       /// </code></example>
       /// </summary>
       public decimal[] ReadDecimalLEArray(int o, int c)
       {
           var a = new decimal[c];
           for (int i = 0; i < c; i++) a[i] = ReadDecimalLE(o + i * 16);
           return a;
       }

       /// <summary>
      /// decimal 배열 읽기 (Big-Endian). 각 원소 16바이트.
      /// <para>총 count × 16 바이트를 소비합니다.</para>
       /// <example><code>
       /// decimal[] prices = bp.ReadDecimalBEArray(offset:0, count:3);
       /// </code></example>
       /// </summary>
       public decimal[] ReadDecimalBEArray(int o, int c)
       {
           var a = new decimal[c];
           for (int i = 0; i < c; i++) a[i] = ReadDecimalBE(o + i * 16);
           return a;
       }

      // ── 스키마 파싱 ───────────────────────────────────────────────

       /// <summary>
      /// BufSchema 를 기반으로 버퍼를 파싱하여 BufResult 를 반환합니다.
      /// <para>파싱 오류가 발생한 필드는 "[ERR: ...]" 문자열로 저장됩니다.</para>
       /// <example><code>
       /// var schema = new BufSchema()
       ///     .Then("stx",    BufType.UInt8)
       ///     .Then("value",  BufType.FloatBE)
      ///     .Then("price",  BufType.DecimalLE)       // 16바이트
      ///     .Then("prices", BufType.DecimalLEArray, size:3); // 48바이트
       ///
       /// var result = bp.Parse(schema);
       /// float   val    = result.Get<float>("value");
       /// decimal price  = result.Get<decimal>("price");
       /// decimal[] arr  = result.Get<decimal[]>("prices");
       ///
      /// // 전체 출력
       /// Console.WriteLine(result);
       /// </code></example>
       /// </summary>
       public BufResult Parse(BufSchema schema)
       {
           var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
           foreach (var f in schema.Fields)
               try { dict[f.Name] = ReadField(f); }
               catch (Exception ex) { dict[f.Name] = $"[ERR: {ex.Message}]"; }
           var result = new BufResult(dict);
           HookParsed?.Invoke(result, schema);
           return result;
       }

       private object ReadField(BufSchema.Field f) => f.Type switch
       {
           BufType.Int8 => ReadInt8(f.Offset),
           BufType.UInt8 => ReadUInt8(f.Offset),
           BufType.Int16BE => ReadInt16BE(f.Offset),
           BufType.Int16LE => ReadInt16LE(f.Offset),
           BufType.UInt16BE => ReadUInt16BE(f.Offset),
           BufType.UInt16LE => ReadUInt16LE(f.Offset),
           BufType.Int32BE => ReadInt32BE(f.Offset),
           BufType.Int32LE => ReadInt32LE(f.Offset),
           BufType.UInt32BE => ReadUInt32BE(f.Offset),
           BufType.UInt32LE => ReadUInt32LE(f.Offset),
           BufType.Int64BE => ReadInt64BE(f.Offset),
           BufType.Int64LE => ReadInt64LE(f.Offset),
           BufType.UInt64BE => ReadUInt64BE(f.Offset),
           BufType.UInt64LE => ReadUInt64LE(f.Offset),
           BufType.FloatBE => ReadFloatBE(f.Offset),
           BufType.FloatLE => ReadFloatLE(f.Offset),
           BufType.DoubleBE => ReadDoubleBE(f.Offset),
           BufType.DoubleLE => ReadDoubleLE(f.Offset),
           BufType.DecimalLE => ReadDecimalLE(f.Offset),
           BufType.DecimalBE => ReadDecimalBE(f.Offset),
           BufType.Bool => ReadBool(f.Offset),
           BufType.Bit => ReadBit(f.Offset, f.Size),
           BufType.StringAscii => ReadStringAscii(f.Offset, f.Size),
           BufType.StringUtf8 => ReadStringUtf8(f.Offset, f.Size),
           BufType.StringHex => ReadStringHex(f.Offset, f.Size),
           BufType.StringBase64 => ReadStringBase64(f.Offset, f.Size),
           BufType.Raw => ReadRaw(f.Offset, f.Size),
           BufType.Int16BEArray => ReadInt16BEArray(f.Offset, f.Size),
           BufType.Int16LEArray => ReadInt16LEArray(f.Offset, f.Size),
           BufType.UInt16BEArray => ReadUInt16BEArray(f.Offset, f.Size),
           BufType.UInt16LEArray => ReadUInt16LEArray(f.Offset, f.Size),
           BufType.Int32BEArray => ReadInt32BEArray(f.Offset, f.Size),
           BufType.Int32LEArray => ReadInt32LEArray(f.Offset, f.Size),
           BufType.UInt32BEArray => ReadUInt32BEArray(f.Offset, f.Size),
           BufType.UInt32LEArray => ReadUInt32LEArray(f.Offset, f.Size),
           BufType.FloatBEArray => ReadFloatBEArray(f.Offset, f.Size),
           BufType.FloatLEArray => ReadFloatLEArray(f.Offset, f.Size),
           BufType.DoubleBEArray => ReadDoubleBEArray(f.Offset, f.Size),
           BufType.DoubleLEArray => ReadDoubleLEArray(f.Offset, f.Size),
           BufType.DecimalLEArray => ReadDecimalLEArray(f.Offset, f.Size),
           BufType.DecimalBEArray => ReadDecimalBEArray(f.Offset, f.Size),
           _ => throw new NotSupportedException($"미지원 타입: {f.Type}")
       };

      // ── 유틸 ──────────────────────────────────────────────────────

      /// <summary>전체 버퍼를 HEX 문자열로 반환합니다. 기본 구분자는 공백입니다.</summary>
       public string ToHex(string sep = " ") => string.Join(sep, Buf.Select(b => b.ToString("X2")));

      /// <summary>버퍼의 일부를 HEX 문자열로 반환합니다.</summary>
       public string ToHex(int o, int n, string sep = " ") => string.Join(sep, Buf.Skip(o).Take(n).Select(b => b.ToString("X2")));

      /// <summary>"BufferParser[N bytes]" 형식의 문자열을 반환합니다.</summary>
       public override string ToString() => $"BufferParser[{Raw.Length} bytes]";

       internal static byte[] HexDecode(string hex)
       {
           hex = hex.Replace(" ", "").Replace("-", "")
                    .Replace("0x", "", StringComparison.OrdinalIgnoreCase);
           if (hex.Length % 2 != 0) throw new FormatException("HEX 문자열 길이가 홀수입니다.");
           return Enumerable.Range(0, hex.Length / 2)
               .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
       }
   }
}