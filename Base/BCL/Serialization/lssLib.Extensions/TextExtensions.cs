// ====================================================================
//  LSSlib.Extensions — TextExtensions
//  string / object 확장 메서드 (TypeParser + Serializer)
//
//  ── 자료형 파싱 ─────────────────────────────────────────────────
//  "255".ToInt32()           → 255  (10진수)
//  "0xFF".ToInt32()          → 255  (HEX, 접두사 자동 인식)
//  "0b11111111".ToInt32()    → 255  (BIN, 접두사 자동 인식)
//  "0o377".ToInt32()         → 255  (OCT, 접두사 자동 인식)
//  "0x3F800000".ToFloat()    → 1.0f (IEEE 754 비트 재해석)
//  "123.456789".ToDecimal()  → 123.456789m (고정소수점, 28~29자리)
//  "AA BB".ToBytes()         → byte[]
//  bytes.ToHexString()       → "AA BB"
//  3.14f.ToHex()             → "0x4048F5C3"
//  3.14f.Analyze()           → Ieee754Info (Sign/Exp/Mantissa)
//
//  ── TryParse / ParseOr ──────────────────────────────────────────
//  "0xFF".TryParse<int>(out int v)
//  "invalid".ParseOr<decimal>(0m)
//
//  ── 직렬화 ──────────────────────────────────────────────────────
//  obj.ToJson()               → JSON 문자열
//  obj.ToXml()                → XML 문자열
//  list.ToCsv()               → CSV 문자열
//  "{...}".FromJson<T>()
//  "<root>...".FromXml<T>()
//  "a,b\n1,2".FromCsv<T>()
// ====================================================================

using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace LSS.Core.Text
{
    // ────────────────────────────────────────────────────────────────
    //  TypeParserExtensions — string 확장
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 자료형 파싱 string 확장 메서드.
    /// 접두사 자동 인식: 0x=HEX, 0b=BIN, 0o=OCT, 그 외=DEC
    /// </summary>
    public static class TypeParserExtensions
    {
        // ── 정수 파싱 ─────────────────────────────────────────────────

        /// <summary>
        /// 문자열을 byte (0~255) 로 파싱합니다.
        /// <example><code>
        /// byte a = "255".ToByte();      // 10진수
        /// byte b = "0xFF".ToByte();     // HEX
        /// byte c = "0b11111111".ToByte(); // BIN
        /// </code></example>
        /// </summary>
        public static byte ToByte(this string s) => (byte)s.ToUInt64();

        /// <summary>문자열을 sbyte (-128~127) 로 파싱합니다.</summary>
        public static sbyte ToSByte(this string s) => (sbyte)s.ToInt64();

        /// <summary>
        /// 문자열을 short (-32768~32767) 로 파싱합니다.
        /// <example><code>
        /// short v = "0x0100".ToInt16();  // 256
        /// short v = "-1000".ToInt16();
        /// </code></example>
        /// </summary>
        public static short ToInt16(this string s) => (short)s.ToInt64();

        /// <summary>문자열을 ushort (0~65535) 로 파싱합니다.</summary>
        public static ushort ToUInt16(this string s) => (ushort)s.ToUInt64();

        /// <summary>
        /// 문자열을 int 로 파싱합니다. 접두사 자동 인식.
        /// <example><code>
        /// int a = "255".ToInt32();          // 10진: 255
        /// int b = "0xFF".ToInt32();         // HEX:  255
        /// int c = "0b11111111".ToInt32();   // BIN:  255
        /// int d = "0o377".ToInt32();        // OCT:  255
        /// int e = "-100".ToInt32();         // 음수
        /// </code></example>
        /// </summary>
        public static int ToInt32(this string s) => (int)s.ToInt64();

        /// <summary>문자열을 uint 로 파싱합니다. 접두사 자동 인식.</summary>
        public static uint ToUInt32(this string s) => (uint)s.ToUInt64();

        /// <summary>
        /// 문자열을 long 으로 파싱합니다.
        /// <example><code>
        /// long v = "0xFFFFFFFFFFFFFFFF".ToInt64();
        /// long v = "9223372036854775807".ToInt64();  // long.MaxValue
        /// </code></example>
        /// </summary>
        public static long ToInt64(this string s)
        {
            s = s.Trim();
            if (IsHex(s)) return (long)ParseHex(s[2..]);
            if (IsBin(s)) return Convert.ToInt64(s[2..].Replace(" ", ""), 2);
            if (IsOct(s)) return Convert.ToInt64(s[2..], 8);
            return long.Parse(s, CultureInfo.InvariantCulture);
        }

        /// <summary>문자열을 ulong 으로 파싱합니다.</summary>
        public static ulong ToUInt64(this string s)
        {
            s = s.Trim();
            if (IsHex(s)) return ParseHex(s[2..]);
            if (IsBin(s)) return Convert.ToUInt64(s[2..].Replace(" ", ""), 2);
            if (IsOct(s)) return Convert.ToUInt64(s[2..], 8);
            return ulong.Parse(s, CultureInfo.InvariantCulture);
        }

        // ── 실수 파싱 ─────────────────────────────────────────────────

        /// <summary>
        /// 문자열을 float 로 파싱합니다.
        /// HEX 접두사(0x)가 있으면 IEEE 754 비트 재해석을 수행합니다.
        /// <example><code>
        /// float a = "3.14".ToFloat();           // 일반 파싱
        /// float b = "0x3F800000".ToFloat();     // IEEE 754 → 1.0f
        /// float c = "0x41200000".ToFloat();     // IEEE 754 → 10.0f
        /// float d = "0x7F800000".ToFloat();     // +Infinity
        /// float e = "0xFF800000".ToFloat();     // -Infinity
        /// float f = "0x7FC00000".ToFloat();     // NaN
        /// </code></example>
        /// </summary>
        public static float ToFloat(this string s)
        {
            s = s.Trim();
            if (IsHex(s)) { uint b = (uint)ParseHex(s[2..]); return Unsafe.As<uint, float>(ref b); }
            return float.Parse(s, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 문자열을 double 로 파싱합니다.
        /// HEX 접두사(0x)가 있으면 IEEE 754 비트 재해석을 수행합니다.
        /// <example><code>
        /// double a = "3.14159265358979".ToDouble();
        /// double b = "0x400921FB54442D18".ToDouble();  // IEEE 754 → π
        /// </code></example>
        /// </summary>
        public static double ToDouble(this string s)
        {
            s = s.Trim();
            if (IsHex(s)) { ulong b = ParseHex(s[2..]); return Unsafe.As<ulong, double>(ref b); }
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 문자열을 decimal 로 파싱합니다.
        /// <para>float/double 과 달리 HEX 재해석 없음 — 순수 10진수 정밀 파싱.</para>
        /// <para>정밀도 28~29자리. 금융·회계·정밀 측정에 적합합니다.</para>
        /// <example><code>
        /// decimal a = "123.456".ToDecimal();                           // 123.456m
        /// decimal b = "-0.01".ToDecimal();                             // -0.01m
        /// decimal c = "79228162514264337593543950335".ToDecimal();     // decimal.MaxValue
        /// decimal d = "1234567890123456789.123456789".ToDecimal();     // 최대 28자리
        ///
        /// // 금융 계산 예시
        /// decimal price    = "1234.567890123456".ToDecimal();
        /// decimal quantity = "100.000".ToDecimal();
        /// decimal total    = price * quantity;  // 정확한 정밀도
        ///
        /// // TryParse 와 함께 사용
        /// bool ok = "123.45".TryParse&lt;decimal&gt;(out decimal v);
        /// decimal safe = "invalid".ParseOr&lt;decimal&gt;(0m);
        /// </code></example>
        /// </summary>
        public static decimal ToDecimal(this string s)
            => decimal.Parse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture);

        /// <summary>
        /// 문자열을 bool 로 파싱합니다.
        /// true 로 인식: "true", "1", "yes", "on", "y" (대소문자 무관).
        /// <example><code>
        /// bool a = "true".ToBool();   // true
        /// bool b = "1".ToBool();      // true
        /// bool c = "yes".ToBool();    // true
        /// bool d = "false".ToBool();  // false
        /// bool e = "0".ToBool();      // false
        /// </code></example>
        /// </summary>
        public static bool ToBool(this string s)
            => s.Trim().ToLowerInvariant() is "true" or "1" or "yes" or "on" or "y";

        // ── HEX 변환 ─────────────────────────────────────────────────

        /// <summary>
        /// HEX 문자열 → byte[].
        /// 공백·하이픈·0x 접두사 자동 제거.
        /// <example><code>
        /// byte[] a = "AA BB CC DD".ToBytes();  // [0xAA, 0xBB, 0xCC, 0xDD]
        /// byte[] b = "AABBCCDD".ToBytes();
        /// byte[] c = "AA-BB-CC".ToBytes();
        /// byte[] d = "0xAA0xBB".ToBytes();
        /// </code></example>
        /// </summary>
        public static byte[] ToBytes(this string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "")
                     .Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (hex.Length == 0) return [];
            if (hex.Length % 2 != 0) throw new FormatException("HEX 길이 홀수");
            return Enumerable.Range(0, hex.Length / 2)
                .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        }
        // ── float / double / int → HEX ───────────────────────────────

        /// <summary>
        /// float → HEX 문자열 (IEEE 754 비트 표현).
        /// <example><code>
        /// string hex = 1.0f.ToHex();    // "0x3F800000"
        /// string hex = 10.0f.ToHex();   // "0x41200000"
        /// string hex = 3.14f.ToHex();   // "0x4048F5C3"
        /// string hex = 3.14f.ToHex(prefix:false);  // "4048F5C3"
        /// </code></example>
        /// </summary>
        public static string ToHex(this float v, bool prefix = true)
        { uint b = Unsafe.As<float, uint>(ref v); return $"{(prefix ? "0x" : "")}{b:X8}"; }

        /// <summary>
        /// double → HEX 문자열 (IEEE 754 비트 표현).
        /// <example><code>
        /// string hex = Math.PI.ToHex();  // "0x400921FB54442D18"
        /// </code></example>
        /// </summary>
        public static string ToHex(this double v, bool prefix = true)
        { ulong b = Unsafe.As<double, ulong>(ref v); return $"{(prefix ? "0x" : "")}{b:X16}"; }

        /// <summary>int → HEX 문자열. 예: 255 → "0x000000FF"</summary>
        public static string ToHex(this int v, bool prefix = true) => $"{(prefix ? "0x" : "")}{v:X8}";
        /// <summary>uint → HEX 문자열. 예: 255u → "0x000000FF"</summary>
        public static string ToHex(this uint v, bool prefix = true) => $"{(prefix ? "0x" : "")}{v:X8}";
        /// <summary>byte → HEX 문자열. 예: 0xAA → "0xAA"</summary>
        public static string ToHex(this byte v, bool prefix = true) => $"{(prefix ? "0x" : "")}{v:X2}";
        /// <summary>ushort → HEX 문자열. 예: 0x1234 → "0x1234"</summary>
        public static string ToHex(this ushort v, bool prefix = true) => $"{(prefix ? "0x" : "")}{v:X4}";

        // ── 2진수 변환 ────────────────────────────────────────────────

        /// <summary>
        /// "0b1010" / "1010" 형식의 2진수 문자열 → int.
        /// <example><code>
        /// int a = "0b10110101".ToInt32FromBin();  // 181
        /// int b = "10110101".ToInt32FromBin();    // 181 (접두사 생략 가능)
        /// </code></example>
        /// </summary>
        public static int ToInt32FromBin(this string bin)
            => Convert.ToInt32(NormBin(bin), 2);

        /// <summary>
        /// int → 2진수 문자열.
        /// <example><code>
        /// string bin = 181.ToBinString(8);         // "0b10110101"
        /// string bin = 181.ToBinString(8, false);  // "10110101"
        /// string bin = 255.ToBinString(32);        // "0b00000000000000000000000011111111"
        /// </code></example>
        /// </summary>
        public static string ToBinString(this int v, int digits = 32, bool prefix = true)
            => $"{(prefix ? "0b" : "")}{Convert.ToString(v, 2).PadLeft(digits, '0')}";

        /// <summary>
        /// byte → 2진수 문자열 (8자리).
        /// <example><code>
        /// string bin = ((byte)0xB5).ToBinString();  // "0b10110101"
        /// </code></example>
        /// </summary>
        public static string ToBinString(this byte v, bool prefix = true)
            => $"{(prefix ? "0b" : "")}{Convert.ToString(v, 2).PadLeft(8, '0')}";

        /// <summary>
        /// byte[] → 2진수 문자열 배열 (각 바이트 8자리, 구분자 기본 공백).
        /// <example><code>
        /// string bins = new byte[]{0xAA,0xBB}.ToBinString();
        /// // "10101010 10111011"
        /// </code></example>
        /// </summary>
        public static string ToBinString(this byte[] bytes, string sep = " ")
            => string.Join(sep, bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));

        // ── IEEE 754 분석 ─────────────────────────────────────────────

        /// <summary>
        /// float 의 IEEE 754 내부 구조를 분석합니다.
        /// <example><code>
        /// var info = 3.14f.Analyze();
        /// Console.WriteLine(info);
        /// // float 3.14159274  0x4048F5C3  Sign=0  Exp=1  Mantissa=0x48F5C3
        /// //   BIN: 0 | 10000000 | 10010001111010111000011
        ///
        /// var pi = 3.14f.Analyze();
        /// Console.WriteLine($"부호={pi.Sign}  지수={pi.Exponent}  가수=0x{pi.Mantissa:X6}");
        ///
        /// // 특수값 확인
        /// float.PositiveInfinity.Analyze().IsInfinity  // true
        /// float.NaN.Analyze().IsNaN                    // true
        /// 0f.Analyze().IsZero                          // true
        /// </code></example>
        /// </summary>
        public static Ieee754Info Analyze(this float v)
        {
            uint bits = Unsafe.As<float, uint>(ref v);
            return new Ieee754Info(v,
                $"0x{bits:X8}",
                Convert.ToString(bits, 2).PadLeft(32, '0'),
                (bits >> 31) & 1,
                (int)((bits >> 23) & 0xFF) - 127,
                bits & 0x7FFFFF);
        }

        /// <summary>
        /// double 의 IEEE 754 내부 구조를 분석합니다.
        /// <example><code>
        /// var info = Math.PI.Analyze();
        /// Console.WriteLine(info);
        /// // double 3.14159265358979324  0x400921FB54442D18  Sign=0  Exp=1
        /// </code></example>
        /// </summary>
        public static Ieee754InfoD Analyze(this double v)
        {
            ulong bits = Unsafe.As<double, ulong>(ref v);
            return new Ieee754InfoD(v,
                $"0x{bits:X16}",
                Convert.ToString((long)bits, 2).PadLeft(64, '0'),
                (bits >> 63) & 1,
                (int)((bits >> 52) & 0x7FF) - 1023,
                bits & 0x000FFFFFFFFFFFFFUL);
        }

        // ── 검증 ──────────────────────────────────────────────────────

        /// <summary>
        /// 유효한 HEX 문자열인지 확인합니다.
        /// <example><code>
        /// "AA BB CC".IsValidHex()   // true
        /// "ZZZZ".IsValidHex()       // false (HEX 문자 아님)
        /// "AAB".IsValidHex()        // false (홀수 길이)
        /// "0xAABB".IsValidHex()     // true  (0x 접두사 제거 후 확인)
        /// </code></example>
        /// </summary>
        public static bool IsValidHex(this string s)
        {
            s = s.Replace(" ", "").Replace("-", "").TrimStart('0', 'x').TrimStart('0', 'X');
            return s.Length > 0 && s.Length % 2 == 0 && s.All(c => "0123456789ABCDEFabcdef".Contains(c));
        }

        /// <summary>
        /// 유효한 2진수 문자열인지 확인합니다 (0b 접두사 허용).
        /// <example><code>
        /// "0b1010".IsValidBin()    // true
        /// "1010".IsValidBin()      // true
        /// "1012".IsValidBin()      // false ('2' 포함)
        /// </code></example>
        /// </summary>
        public static bool IsValidBin(this string s)
        { s = NormBin(s); return s.Length > 0 && s.All(c => c is '0' or '1'); }

        /// <summary>
        /// 유효한 숫자 문자열인지 확인합니다 (HEX/BIN/DEC 모두 지원).
        /// <example><code>
        /// "0xFF".IsValidNumber()    // true
        /// "0b1010".IsValidNumber()  // true
        /// "3.14".IsValidNumber()    // true
        /// "abc".IsValidNumber()     // false
        /// </code></example>
        /// </summary>
        public static bool IsValidNumber(this string s)
        {
            s = s.Trim();
            if (IsHex(s)) return s[2..].IsValidHex();
            if (IsBin(s)) return s[2..].IsValidBin();
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        }

        // ── TryParse / ParseOr ────────────────────────────────────────

        /// <summary>
        /// 안전하게 파싱을 시도합니다. 실패해도 예외가 발생하지 않습니다.
        /// byte, sbyte, short, ushort, int, uint, long, ulong,
        /// float, double, decimal, bool 을 지원합니다.
        /// <example><code>
        /// bool ok1 = "0xFF".TryParse<int>(out int v1);     // ok1=true, v1=255
        /// bool ok2 = "abc".TryParse<int>(out int v2);      // ok2=false, v2=0
        /// bool ok3 = "123.45".TryParse<decimal>(out decimal d); // ok3=true
        /// bool ok4 = "0.01".TryParse<decimal>(out decimal d2);  // ok4=true
        /// </code></example>
        /// </summary>
        public static bool TryParse<T>(this string s, out T result) where T : struct
        {
            result = default;
            try { result = (T)ParseAuto(s, typeof(T)); return true; }
            catch { return false; }
        }

        /// <summary>
        /// 파싱 실패 시 기본값을 반환합니다.
        /// <example><code>
        /// int     v1 = "0xFF".ParseOr<int>(-1);           // 255
        /// int     v2 = "bad".ParseOr<int>(-1);            // -1
        /// decimal d1 = "123.45".ParseOr<decimal>(0m);     // 123.45m
        /// decimal d2 = "bad".ParseOr<decimal>(0m);        // 0m
        /// float   f1 = "0x3F800000".ParseOr<float>(0f);   // 1.0f (IEEE 754)
        /// </code></example>
        /// </summary>
        public static T ParseOr<T>(this string s, T fallback = default) where T : struct
            => s.TryParse<T>(out var r) ? r : fallback;

        // ── 내부 헬퍼 ─────────────────────────────────────────────────
        private static bool IsHex(string s) => s.Length > 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X');
        private static bool IsBin(string s) => s.Length > 2 && s[0] == '0' && (s[1] == 'b' || s[1] == 'B');
        private static bool IsOct(string s) => s.Length > 2 && s[0] == '0' && (s[1] == 'o' || s[1] == 'O');
        private static string NormBin(string s) { s = s.Trim(); if (s.Length > 2 && s[0] == '0' && (s[1] == 'b' || s[1] == 'B')) s = s[2..]; return s.Replace(" ", ""); }
        private static ulong ParseHex(string h) => ulong.Parse(h, NumberStyles.HexNumber);

        private static object ParseAuto(string s, Type t)
        {
            if (t == typeof(byte)) return s.ToByte(); if (t == typeof(sbyte)) return s.ToSByte();
            if (t == typeof(short)) return s.ToInt16(); if (t == typeof(ushort)) return s.ToUInt16();
            if (t == typeof(int)) return s.ToInt32(); if (t == typeof(uint)) return s.ToUInt32();
            if (t == typeof(long)) return s.ToInt64(); if (t == typeof(ulong)) return s.ToUInt64();
            if (t == typeof(float)) return s.ToFloat(); if (t == typeof(double)) return s.ToDouble();
            if (t == typeof(decimal)) return s.ToDecimal();
            if (t == typeof(bool)) return s.ToBool();
            throw new NotSupportedException($"미지원 타입: {t.Name}");
        }
    }

    // ── IEEE 754 결과 ─────────────────────────────────────────────────

    /// <summary>float IEEE 754 내부 구조 분해 결과.</summary>
    public record Ieee754Info(float Value, string Hex, string Bin,
        uint Sign, int Exponent, uint Mantissa)
    {
        /// <summary>NaN 여부.</summary>
        public bool IsNaN => float.IsNaN(Value);
        /// <summary>Infinity 여부.</summary>
        public bool IsInfinity => float.IsInfinity(Value);
        /// <summary>0 여부.</summary>
        public bool IsZero => Value == 0f;

        /// <summary>분해 결과 문자열. 부호|지수|가수 비트를 표시합니다.</summary>
        public override string ToString()
            => $"float {Value:G9}  {Hex}  Sign={Sign}  Exp={Exponent}  Mantissa=0x{Mantissa:X6}\n" +
               $"  BIN: {Bin[..1]} | {Bin[1..9]} | {Bin[9..]}";
    }

    /// <summary>double IEEE 754 내부 구조 분해 결과.</summary>
    public record Ieee754InfoD(double Value, string Hex, string Bin,
        ulong Sign, int Exponent, ulong Mantissa)
    {
        /// <summary>NaN 여부.</summary>
        public bool IsNaN => double.IsNaN(Value);
        /// <summary>Infinity 여부.</summary>
        public bool IsInfinity => double.IsInfinity(Value);
        /// <summary>0 여부.</summary>
        public bool IsZero => Value == 0d;

        /// <summary>분해 결과 문자열.</summary>
        public override string ToString()
            => $"double {Value:G17}  {Hex}  Sign={Sign}  Exp={Exponent}";
    }

    // ────────────────────────────────────────────────────────────────
    //  SerializerExtensions — object 확장
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// JSON / XML / CSV 직렬화 object 확장 메서드.
    /// decimal 타입은 System.Text.Json 에서 정밀하게 직렬화됩니다.
    /// </summary>
    public static class SerializerExtensions
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        // ── JSON ──────────────────────────────────────────────────────

        /// <summary>
        /// 객체를 JSON 문자열로 직렬화합니다.
        /// decimal 은 정밀도 손실 없이 직렬화됩니다.
        /// <example><code>
        /// var data = new { Price=1234.56m, Name="상품", Count=10 };
        /// string json = data.ToJson();
        /// // {
        /// //   "Price": 1234.56,
        /// //   "Name": "상품",
        /// //   "Count": 10
        /// // }
        ///
        /// // 커스텀 옵션
        /// var opts = new JsonSerializerOptions { WriteIndented = false };
        /// string compact = data.ToJson(opts);
        /// </code></example>
        /// </summary>
        public static string ToJson<T>(this T obj, JsonSerializerOptions? opt = null)
            => JsonSerializer.Serialize(obj, opt ?? _opts);

        /// <summary>
        /// JSON 문자열을 객체로 역직렬화합니다.
        /// <example><code>
        /// var data = jsonStr.FromJson<MyRecord>();
        /// decimal price = data?.Price ?? 0m;
        /// </code></example>
        /// </summary>
        public static T? FromJson<T>(this string json, JsonSerializerOptions? opt = null)
            => JsonSerializer.Deserialize<T>(json, opt ?? _opts);

        /// <summary>
        /// 객체를 JSON 파일로 비동기 저장합니다.
        /// <example><code>
        /// await myObj.SaveJsonAsync("data.json");
        /// </code></example>
        /// </summary>
        public static async Task SaveJsonAsync<T>(this T obj, string path, JsonSerializerOptions? opt = null)
        { await using var fs = File.Create(path); await JsonSerializer.SerializeAsync(fs, obj, opt ?? _opts); }

        /// <summary>
        /// JSON 파일에서 객체를 비동기 로드합니다.
        /// <example><code>
        /// var data = await "data.json".LoadJsonAsync<MyRecord>();
        /// </code></example>
        /// </summary>
        public static async Task<T?> LoadJsonAsync<T>(this string path, JsonSerializerOptions? opt = null)
        { await using var fs = File.OpenRead(path); return await JsonSerializer.DeserializeAsync<T>(fs, opt ?? _opts); }

        // ── XML ───────────────────────────────────────────────────────

        /// <summary>
        /// 객체를 XML 문자열로 직렬화합니다.
        /// <example><code>
        /// string xml = myObj.ToXml();
        /// string xml = myObj.ToXml(indent:false);  // 압축 형태
        /// </code></example>
        /// </summary>
        public static string ToXml<T>(this T obj, bool indent = true)
        {
            var xs = new XmlSerializer(typeof(T)); var sb = new StringBuilder();
            using var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = indent, Encoding = new UTF8Encoding(false) });
            xs.Serialize(w, obj); return sb.ToString();
        }

        /// <summary>
        /// XML 문자열을 객체로 역직렬화합니다.
        /// <example><code>
        /// var obj = xmlStr.FromXml<MyClass>();
        /// </code></example>
        /// </summary>
        public static T? FromXml<T>(this string xml)
        { var xs = new XmlSerializer(typeof(T)); using var r = new StringReader(xml); return (T?)xs.Deserialize(r); }

        /// <summary>객체를 XML 파일로 저장합니다.</summary>
        public static void SaveXml<T>(this T obj, string path, bool indent = true)
        { var xs = new XmlSerializer(typeof(T)); using var w = XmlWriter.Create(path, new XmlWriterSettings { Indent = indent }); xs.Serialize(w, obj); }

        /// <summary>XML 파일에서 객체를 로드합니다.</summary>
        public static T? LoadXml<T>(this string path)
        { var xs = new XmlSerializer(typeof(T)); using var r = new StreamReader(path, Encoding.UTF8); return (T?)xs.Deserialize(r); }

        // ── CSV ───────────────────────────────────────────────────────

        /// <summary>
        /// IEnumerable 컬렉션을 CSV 문자열로 변환합니다.
        /// decimal 프로퍼티는 ToString() 을 통해 정밀도 유지.
        /// <example><code>
        /// var rows = new[]
        /// {
        ///     new SensorRow { Id=1, Name="온도", Value=25.3f,  Price=1234.56m },
        ///     new SensorRow { Id=2, Name="습도", Value=60.1f,  Price=789.00m  },
        /// };
        /// string csv = rows.ToCsv();
        /// // Id,Name,Value,Price
        /// // 1,온도,25.3,1234.56
        /// // 2,습도,60.1,789.00
        ///
        /// // 탭 구분자
        /// string tsv = rows.ToCsv(delim:"\t");
        ///
        /// // 헤더 없이
        /// string data = rows.ToCsv(header:false);
        /// </code></example>
        /// </summary>
        public static string ToCsv<T>(this IEnumerable<T> data, string delim = ",", bool header = true)
        {
            var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var sb = new StringBuilder();
            if (header) sb.AppendLine(string.Join(delim, props.Select(p => Esc(p.Name, delim))));
            foreach (var item in data)
                sb.AppendLine(string.Join(delim, props.Select(p => Esc(p.GetValue(item)?.ToString() ?? "", delim))));
            return sb.ToString();
        }

        /// <summary>
        /// CSV 문자열을 List 로 역직렬화합니다.
        /// decimal 프로퍼티는 string.ToDecimal() 을 통해 정밀 복원.
        /// <example><code>
        /// var rows = csvStr.FromCsv<SensorRow>();
        /// foreach (var r in rows)
        ///     Console.WriteLine($"{r.Name}: {r.Price:C}");
        /// </code></example>
        /// </summary>
        public static List<T> FromCsv<T>(this string csv, string delim = ",") where T : new()
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return [];
            var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var headers = SplitCsv(lines[0], delim);
            var result = new List<T>();
            for (int i = 1; i < lines.Length; i++)
            {
                var vals = SplitCsv(lines[i], delim); var obj = new T();
                for (int j = 0; j < headers.Length && j < vals.Length; j++)
                {
                    var p = props.FirstOrDefault(x => string.Equals(x.Name, headers[j].Trim(), StringComparison.OrdinalIgnoreCase));
                    if (p?.CanWrite == true)
                        try
                        {
                            // decimal 특수 처리
                            if (p.PropertyType == typeof(decimal))
                                p.SetValue(obj, decimal.Parse(vals[j].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));
                            else
                                p.SetValue(obj, Convert.ChangeType(vals[j].Trim(), p.PropertyType, CultureInfo.InvariantCulture));
                        }
                        catch { }
                }
                result.Add(obj);
            }
            return result;
        }

        /// <summary>컬렉션을 CSV 파일로 비동기 저장합니다.</summary>
        public static async Task SaveCsvAsync<T>(this IEnumerable<T> data, string path, string delim = ",")
            => await File.WriteAllTextAsync(path, data.ToCsv(delim), new UTF8Encoding(false));

        /// <summary>CSV 파일을 List 로 비동기 로드합니다.</summary>
        public static async Task<List<T>> LoadCsvAsync<T>(this string path, string delim = ",") where T : new()
            => (await File.ReadAllTextAsync(path, Encoding.UTF8)).FromCsv<T>(delim);

        private static string Esc(string v, string d)
            => (v.Contains(d) || v.Contains('"') || v.Contains('\n')) ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

        private static string[] SplitCsv(string line, string d)
        {
            var r = new List<string>(); var cur = new StringBuilder(); bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = !q; }
                else if (!q && line[i..].StartsWith(d)) { r.Add(cur.ToString()); cur.Clear(); i += d.Length - 1; }
                else cur.Append(c);
            }
            r.Add(cur.ToString()); return r.ToArray();
        }
    }
}