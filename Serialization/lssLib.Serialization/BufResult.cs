// ====================================================================
//  lssLib.Binary — BufResult.cs
//  스키마 파싱 결과
//
//  [설계 원칙]
//  - 파싱 결과를 타입 안전하게 꺼낼 수 있는 인터페이스 제공
//  - 편의 메서드: 타입 몰라도 사용 가능한 자동 변환 (GetInt/GetFloat/GetDecimal)
//  - 안전 접근: GetOr(기본값 폴백), GetOrThrow(명시적 예외)
//  - 무결성: IsAllOk, ErrorFields, HasError
//  - 디버깅: ToString(), ToDump(필드명) — HexDump + ASCII
//
//  [접근 방식 선택 가이드]
//  타입을 정확히 알 때   → Get<T>
//  타입이 숫자라면       → GetInt / GetFloat / GetDecimal (자동 변환)
//  필드 없어도 되는 경우 → GetOr<T>(fallback)
//  필드 반드시 있어야 함 → GetOrThrow<T>(errorMessage)
//  byte[] 필드           → GetRaw
//  로깅/출력용 문자열    → GetString
// ====================================================================

namespace lssLib.Binary
{
    /// <summary>
    /// 스키마 파싱 결과. <see cref="BufferParser.Parse(BufSchema)"/> 가 반환합니다.
    ///
    /// <para><b>■ 기본 접근 — Get<T></b></para>
    /// <para>정확한 타입을 지정합니다. 타입 불일치 시 <c>InvalidCastException</c>.</para>
    ///
    /// <para><b>■ 편의 접근 — GetInt/GetFloat/GetDecimal/GetString</b></para>
    /// <para>타입을 몰라도 숫자/문자열로 자동 변환. 가장 편리한 방법.</para>
    ///
    /// <para><b>■ 안전 접근 — GetOr/GetOrThrow</b></para>
    /// <para>GetOr: 실패 시 기본값 반환. GetOrThrow: 실패 시 명시적 예외.</para>
    ///
    /// <example><code>
    /// // ─── 전형적인 사용 패턴 ────────────────────────────────────────
    ///
    /// var schema = new BufSchema()
    ///     .Then("STX",     BufType.UInt8)
    ///     .Then("DevID",   BufType.UInt8)
    ///     .Then("TempADC", BufType.UInt16BE)
    ///     .Then("Value",   BufType.FloatBE)
    ///     .Then("Price",   BufType.DecimalLE)
    ///     .Then("Prices",  BufType.DecimalLEArray, size:3)
    ///     .Then("Name",    BufType.StringAscii,    size:16)
    ///     .Then("Payload", BufType.Raw,            size:8);
    ///
    /// var result = raw.ToParser().Parse(schema);
    ///
    /// // ─── 방법 1: Get<T> — 정확한 타입 지정 ─────────────────────
    /// byte     stx    = result.Get<byte>("STX");
    /// ushort   adc    = result.Get<ushort>("TempADC");
    /// float    val    = result.Get<float>("Value");
    /// decimal  price  = result.Get<decimal>("Price");
    /// decimal[]pArr   = result.Get<decimal[]>("Prices");
    /// string   name   = result.Get<string>("Name");
    /// byte[]   payload= result.Get<byte[]>("Payload");
    ///
    /// // ─── 방법 2: 편의 메서드 — 타입 자동 변환 (★권장) ──────────────
    /// int     adcInt  = result.GetInt("TempADC");     // ushort → int 자동
    /// float   adcF    = result.GetFloat("TempADC");   // ushort → float 자동
    /// decimal adcD    = result.GetDecimal("TempADC"); // ushort → decimal 자동
    /// string  stxStr  = result.GetString("STX");      // "170" (0xAA)
    /// byte[]  pay     = result.GetRaw("Payload");     // byte[] 그대로
    ///
    /// // ─── Scale 변환과 연결 ─────────────────────────────────────────
    /// double tempC = result.GetInt("TempADC").MapTo(0, 4095, -40.0, 125.0);
    /// float  voltV = result.GetFloat("VoltADC").MapTo(0f, 4095f, 0f, 3.3f);
    ///
    /// // ─── 무결성 확인 ──────────────────────────────────────────────
    /// if (!result.IsAllOk)
    ///     Console.WriteLine($"오류 필드: {string.Join(", ", result.ErrorFields)}");
    ///
    /// // ─── 전체 출력 ────────────────────────────────────────────────
    /// Console.WriteLine(result.ToString());
    /// //   STX                    = 0xAA (170)
    /// //   DevID                  = 0x01 (1)
    /// //   TempADC                = 0x09C4 (2500)
    /// //   Value                  = 3.14
    /// //   Price                  = 1234.56  (decimal 16B)
    /// //   Prices                 = [1234.56, 789.00, 100.50]
    /// //   Name                   = "Sensor-A"
    /// //   Payload                = [AA 01 00 0C 41 20 00 00]
    ///
    /// // ─── HexDump ──────────────────────────────────────────────────
    /// Console.WriteLine(result.ToDump("Payload"));
    /// // Payload (8 bytes):
    /// //   00000000: AA 01 00 0C 41 20 00 00                            ....A ..
    /// </code></example>
    /// </summary>
    public sealed class BufResult
    {
        private readonly Dictionary<string, object> _d;
        internal BufResult(Dictionary<string, object> d) => _d = d;

        // ── 기본 접근 ────────────────────────────────────────────────

        /// <summary>
        /// 필드 값을 T 타입으로 변환합니다.
        /// <para>타입이 정확히 일치해야 합니다. 불일치 시 <c>InvalidCastException</c>.</para>
        /// <para><b>팁</b>: 타입을 잘 모를 경우 <c>GetInt/GetFloat/GetDecimal</c> 편의 메서드 사용 권장.</para>
        /// <example><code>
        /// // 정확한 타입으로 접근
        /// byte     stx    = result.Get<byte>("STX");          // BufType.UInt8
        /// sbyte    offset = result.Get<sbyte>("Offset");      // BufType.Int8
        /// short    s16    = result.Get<short>("Int16BE");     // BufType.Int16BE
        /// ushort   adc    = result.Get<ushort>("TempADC");    // BufType.UInt16BE
        /// int      i32    = result.Get<int>("Int32BE");       // BufType.Int32BE
        /// uint     u32    = result.Get<uint>("UInt32LE");     // BufType.UInt32LE
        /// float    val    = result.Get<float>("Value");       // BufType.FloatBE
        /// double   dbl    = result.Get<double>("Double");     // BufType.DoubleBE
        /// decimal  price  = result.Get<decimal>("Price");     // BufType.DecimalLE
        /// bool     bit    = result.Get<bool>("BitFlag");      // BufType.Bit/Bool
        /// string   name   = result.Get<string>("Name");       // BufType.StringAscii
        /// byte[]   raw    = result.Get<byte[]>("Payload");    // BufType.Raw
        /// float[]  farr   = result.Get<float[]>("Sensors");   // BufType.FloatBEArray
        /// decimal[]darr   = result.Get<decimal[]>("Prices");  // BufType.DecimalLEArray
        /// ushort[] regs   = result.Get<ushort[]>("Regs");     // BufType.UInt16BEArray
        /// </code></example>
        /// </summary>
        public T Get<T>(string name) => (T)_d[name];

        /// <summary>
        /// 값 변환 실패 또는 필드 없을 때 fallback 을 반환합니다. 예외 없음.
        /// <para>선택적 필드, 장비별로 있을 수도 없을 수도 있는 필드 접근에 적합합니다.</para>
        /// <example><code>
        /// // 선택적 필드 안전 접근
        /// decimal price   = result.GetOr<decimal>("Price",    fallback:0m);
        /// float   value   = result.GetOr<float>("Value",      fallback:float.NaN);
        /// byte    status  = result.GetOr<byte>("Status",      fallback:0xFF);
        /// string  name    = result.GetOr<string>("Name",      fallback:"Unknown");
        ///
        /// // 파싱 오류 무시하고 기본값 사용
        /// ushort adc = result.GetOr<ushort>("ADC", 0);
        /// if (adc == 0) Console.WriteLine("ADC 파싱 실패 또는 0");
        ///
        /// // bool 기본값
        /// bool isActive = result.GetOr<bool>("IsActive", false);
        /// </code></example>
        /// </summary>
        public T GetOr<T>(string name, T fallback = default!)
            => _d.TryGetValue(name, out var v) && v is T t ? t : fallback;

        /// <summary>
        /// 필드 없거나 타입 불일치 시 <c>InvalidOperationException</c> 을 throw 합니다.
        /// <para>반드시 존재해야 하는 필드 (STX, CRC 등) 에 적합합니다.</para>
        /// <example><code>
        /// // STX 검증: 없으면 명확한 오류 메시지로 예외 발생
        /// byte stx = result.GetOrThrow<byte>("STX", "STX 필드가 없습니다");
        /// if (stx != 0xAA)
        ///     throw new InvalidDataException($"STX 불일치: 0x{stx:X2} (기대값: 0xAA)");
        ///
        /// // FC 코드 검증
        /// byte fc = result.GetOrThrow<byte>("FC", "Function Code 필드 누락");
        /// if (fc != 0x03 && fc != 0x04)
        ///     throw new InvalidDataException($"지원하지 않는 FC: 0x{fc:X2}");
        ///
        /// // decimal 필드 필수 확인
        /// decimal price = result.GetOrThrow<decimal>("Price", "Price 필드 필수");
        ///
        /// // OnParseDone 콜백에서 활용
        /// raw.ToParser()
        ///    .OnParseDone((r, s) =>
        ///    {
        ///        r.GetOrThrow<byte>("STX", "STX 없음");
        ///        if (!r.IsAllOk)
        ///            throw new Exception($"파싱 오류: {string.Join(", ", r.ErrorFields)}");
        ///    })
        ///    .Parse(schema);
        /// </code></example>
        /// </summary>
        public T GetOrThrow<T>(string name, string errorMessage)
        {
            if (!_d.TryGetValue(name, out var v))
                throw new InvalidOperationException(
                    $"[BufResult] 필드 없음: '{name}' — {errorMessage}");
            if (v is not T t)
                throw new InvalidCastException(
                    $"[BufResult] '{name}' 타입 불일치: 실제={v?.GetType().Name} 요청={typeof(T).Name}");
            return t;
        }

        // ── 타입 변환 편의 메서드 ────────────────────────────────────

        /// <summary>
        /// 숫자 필드를 <c>int</c> 로 자동 변환합니다.
        /// byte/sbyte/short/ushort/int 모두 int 로 변환됩니다.
        /// <para><b>가장 범용적인 숫자 접근 방법</b>. ADC 값, 카운터, 인덱스에 편리.</para>
        /// <example><code>
        /// // TempADC 가 UInt16BE (ushort) 이어도 int 로 자동 변환
        /// int adcInt = result.GetInt("TempADC");   // ushort 2500 → int 2500
        ///
        /// // Scale 변환과 자연스럽게 연결
        /// double tempC = result.GetInt("TempADC").MapTo(0, 4095, -40.0, 125.0);
        /// double voltV = result.GetInt("VoltADC").MapTo(0, 4095,  0.0,   3.3);
        ///
        /// // STX, FC 도 int 로 읽기
        /// int stxInt = result.GetInt("STX");   // byte 0xAA → int 170
        /// int fcInt  = result.GetInt("FC");    // byte 0x03 → int 3
        ///
        /// // 여러 필드를 일관된 방식으로 로깅
        /// foreach (var name in new[]{"TempADC","HumiADC","VoltADC"})
        ///     Console.WriteLine($"  {name}: {result.GetInt(name)}");
        /// </code></example>
        /// </summary>
        public int GetInt(string name)
        {
            if (!_d.TryGetValue(name, out var v))
                throw new KeyNotFoundException($"[BufResult] 필드 없음: '{name}'");
            return v switch
            {
                byte b => b,
                sbyte sb => sb,
                short s => s,
                ushort us => us,
                int i => i,
                uint ui => (int)ui,
                long l => (int)l,
                ulong ul => (int)ul,
                _ => Convert.ToInt32(v)
            };
        }

        /// <summary>
        /// 숫자 필드를 <c>float</c> 로 자동 변환합니다.
        /// float 그대로 반환, 정수형은 float 으로 변환됩니다.
        /// <para>Scale 변환 후 float 결과를 받을 때, 단정밀도로 충분한 센서값에 사용.</para>
        /// <example><code>
        /// // ADC 값을 float 으로 읽어 Scale 변환
        /// float adcF  = result.GetFloat("TempADC");  // ushort → float
        /// float tempC = adcF.MapTo(0f, 4095f, -40f, 125f);
        ///
        /// // FloatBE 필드 그대로 반환
        /// float val   = result.GetFloat("Value");    // float 그대로
        ///
        /// // 모든 아날로그 채널을 float[] 로 수집
        /// float[] readings = new[]{"Ch0","Ch1","Ch2","Ch3"}
        ///     .Select(ch => result.GetFloat(ch)).ToArray();
        ///
        /// // 범위 체크
        /// float temp = result.GetFloat("Temperature");
        /// if (temp is < -40f or > 125f)
        ///     Console.WriteLine($"온도 범위 초과: {temp:F2}°C");
        /// </code></example>
        /// </summary>
        public float GetFloat(string name)
        {
            if (!_d.TryGetValue(name, out var v))
                throw new KeyNotFoundException($"[BufResult] 필드 없음: '{name}'");
            return v switch
            {
                float f => f,
                double d => (float)d,
                decimal m => (float)m,
                byte b => b,
                sbyte sb => sb,
                short s => s,
                ushort us => us,
                int i => i,
                uint ui => ui,
                _ => Convert.ToSingle(v)
            };
        }

        /// <summary>
        /// 숫자 필드를 <c>double</c> 로 자동 변환합니다.
        /// <example><code>
        /// double lat = result.GetDouble("Latitude");   // DoubleBE 그대로
        /// double lon = result.GetDouble("Longitude");
        /// </code></example>
        /// </summary>
        public double GetDouble(string name)
        {
            if (!_d.TryGetValue(name, out var v))
                throw new KeyNotFoundException($"[BufResult] 필드 없음: '{name}'");
            return v switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                _ => Convert.ToDouble(v)
            };
        }

        /// <summary>
        /// 필드를 <c>decimal</c> 로 자동 변환합니다.
        /// decimal은 그대로, 정수·실수형은 decimal 로 변환됩니다.
        /// <para>금융 계산, 정밀 측정값, 환율, 가격 데이터 접근에 권장합니다.</para>
        /// <example><code>
        /// // ─── DecimalLE/BE 필드 ───────────────────────────────────────
        /// decimal price   = result.GetDecimal("Price");   // decimal 그대로 (16B)
        /// decimal qty     = result.GetDecimal("Qty");
        /// decimal total   = price * qty;   // 정확한 곱셈 (부동소수점 오차 없음)
        ///
        /// // ─── 정수형 필드도 decimal 로 변환 ─────────────────────────
        /// decimal adcD    = result.GetDecimal("TempADC"); // ushort → decimal
        ///
        /// // ─── decimal 배열 접근 ───────────────────────────────────
        /// decimal[] prices = result.Get<decimal[]>("Prices"); // Get<T> 사용
        /// decimal   sum    = prices.Sum();
        ///
        /// // ─── 금융 계산 체인 ─────────────────────────────────────────
        /// decimal unitPrice = result.GetDecimal("UnitPrice");  // 1234.567890m
        /// decimal quantity  = result.GetDecimal("Quantity");   // 100.000m
        /// decimal discount  = result.GetDecimal("Discount");   // 0.05m (5%)
        /// decimal finalPrice = unitPrice * quantity * (1 - discount);
        /// // = 117283.94955m (정확한 계산)
        ///
        /// // float 로 했을 경우 비교
        /// float fPrice = (float)unitPrice;
        /// float fFinal = fPrice * 100f * 0.95f;  // 부동소수점 오차 발생!
        /// </code></example>
        /// </summary>
        public decimal GetDecimal(string name)
        {
            if (!_d.TryGetValue(name, out var v))
                throw new KeyNotFoundException($"[BufResult] 필드 없음: '{name}'");
            return v switch
            {
                decimal m => m,
                float f => (decimal)f,
                double d => (decimal)d,
                byte b => b,
                sbyte sb => sb,
                short s => s,
                ushort us => us,
                int i => i,
                uint ui => ui,
                long l => l,
                ulong ul => ul,
                _ => Convert.ToDecimal(v)
            };
        }

        /// <summary>
        /// 필드 값을 문자열로 반환합니다. 모든 타입에 적용 가능합니다.
        /// <para>로깅, 디버깅, 화면 출력, CSV 내보내기에 편리합니다.</para>
        /// <example><code>
        /// // 모든 필드를 문자열로 출력
        /// foreach (var key in result.Keys)
        ///     Console.WriteLine($"  {key,-20} = {result.GetString(key)}");
        ///
        /// // 출력 예시:
        /// //   STX                  = 0xAA (170)
        /// //   TempADC              = 0x09C4 (2500)
        /// //   Value                = 3.14
        /// //   Price                = 1234.56  (decimal 16B)
        /// //   Name                 = "Sensor-A"
        /// //   Payload              = [AA 01 00 0C 41 20 00 00]
        ///
        /// // CSV 행 생성
        /// var csvRow = string.Join(",",
        ///     new[]{"STX","TempADC","Value","Price","Name"}
        ///     .Select(k => result.GetString(k)));
        ///
        /// // 특정 타입별 포맷 예시:
        /// result.GetString("STX");      // "0xAA (170)"     ← byte
        /// result.GetString("TempADC");  // "0x09C4 (2500)"  ← ushort
        /// result.GetString("Value");    // "3.14"           ← float
        /// result.GetString("Price");    // "1234.56  (decimal 16B)" ← decimal
        /// result.GetString("Name");     // "\"Sensor-A\""   ← string
        /// result.GetString("Payload");  // "[AA 01 00 0C]"  ← byte[]
        /// </code></example>
        /// </summary>
        public string GetString(string name)
        {
            if (!_d.TryGetValue(name, out var v))
                throw new KeyNotFoundException($"[BufResult] 필드 없음: '{name}'");
            return FormatVal(v);
        }

        /// <summary>
        /// 필드 값을 <c>byte[]</c> 로 반환합니다.
        /// <para><see cref="BufType.Raw"/> 또는 <see cref="BufType.UInt8Array"/> 타입 필드 전용.</para>
        /// <example><code>
        /// // Raw 페이로드 추출
        /// byte[] payload = result.GetRaw("Payload");
        ///
        /// // 서브 프레임으로 재파싱
        /// var subResult = payload.ToParser().Parse(subSchema);
        ///
        /// // CRC 검증에 활용
        /// byte[] payload2 = result.GetRaw("Data");
        /// byte   storedCrc= result.Get<byte>("CRC");
        /// byte   calcCrc  = payload2.Crc8();
        /// bool   ok       = storedCrc == calcCrc;
        ///
        /// // 16진수로 출력
        /// Console.WriteLine(payload.ToHexString(" "));
        ///
        /// // HexDump (상세 출력)
        /// Console.WriteLine(result.ToDump("Payload"));
        /// </code></example>
        /// </summary>
        public byte[] GetRaw(string name)
        {
            if (!_d.TryGetValue(name, out var v) || v is not byte[] arr)
                throw new InvalidCastException(
                    $"[BufResult] '{name}' 은 byte[] 타입이 아닙니다. 실제 타입: {v?.GetType().Name}");
            return arr;
        }

        // ── 무결성 확인 ───────────────────────────────────────────────

        /// <summary>
        /// 필드 파싱 중 오류가 발생했는지 확인합니다.
        /// <para>오류 발생 시 해당 필드 값은 "[ERR: 오류 메시지]" 문자열로 저장됩니다.</para>
        /// <example><code>
        /// // 개별 필드 오류 확인
        /// if (result.HasError("Price"))
        ///     Console.WriteLine($"Price 파싱 오류: {result.Get<string>("Price")}");
        ///
        /// // 전체 무결성 확인 후 처리
        /// if (result.IsAllOk)
        ///     ProcessData(result);
        /// else
        ///     foreach (var errField in result.ErrorFields)
        ///         Console.WriteLine($"오류: {errField} = {result.GetString(errField)}");
        /// </code></example>
        /// </summary>
        public bool HasError(string name)
            => _d.TryGetValue(name, out var v) && v is string s && s.StartsWith("[ERR");

        /// <summary>필드가 결과에 존재하는지 확인합니다.</summary>
        public bool Has(string name) => _d.ContainsKey(name);

        /// <summary>
        /// 모든 필드에 오류가 없으면 true 를 반환합니다.
        /// <example><code>
        /// var result = raw.ToParser().Parse(schema);
        ///
        /// if (!result.IsAllOk)
        /// {
        ///     var errors = result.ErrorFields.ToList();
        ///     throw new InvalidDataException(
        ///         $"파싱 오류 ({errors.Count}개): {string.Join(", ", errors)}");
        /// }
        ///
        /// // 정상 처리
        /// decimal price = result.GetDecimal("Price");
        /// </code></example>
        /// </summary>
        public bool IsAllOk => _d.Values.All(v =>
            v is not string s || !s.StartsWith("[ERR"));

        /// <summary>
        /// 오류가 발생한 필드 이름 목록을 반환합니다.
        /// <example><code>
        /// var errors = result.ErrorFields.ToList();
        /// if (errors.Count > 0)
        ///     Console.WriteLine($"파싱 오류 필드: {string.Join(", ", errors)}");
        ///
        /// // 오류 상세 출력
        /// foreach (var errField in result.ErrorFields)
        ///     Console.WriteLine($"  [{errField}] {result.GetString(errField)}");
        /// </code></example>
        /// </summary>
        public IEnumerable<string> ErrorFields =>
            _d.Where(p => p.Value is string s && s.StartsWith("[ERR")).Select(p => p.Key);

        // ── 접근자 ───────────────────────────────────────────────────

        /// <summary>인덱서로 값에 접근합니다 (object 반환).</summary>
        public object this[string name] => _d[name];

        /// <summary>모든 필드 이름 목록.</summary>
        public IEnumerable<string> Keys => _d.Keys;

        /// <summary>모든 (이름, 값) 쌍.</summary>
        public IEnumerable<(string Key, object Val)> Items => _d.Select(p => (p.Key, p.Value));

        /// <summary>파싱된 필드 수.</summary>
        public int Count => _d.Count;

        /// <summary>Dictionary 복사본을 반환합니다 (수정 가능한 복사본).</summary>
        public Dictionary<string, object> ToDict()
            => new(_d, StringComparer.OrdinalIgnoreCase);

        // ── 출력 메서드 ───────────────────────────────────────────────

        /// <summary>
        /// 모든 필드를 "  이름 = 값" 형식의 문자열로 반환합니다.
        /// <example><code>
        /// Console.WriteLine(result.ToString());
        /// // 출력 예시:
        /// //   STX                    = 0xAA (170)
        /// //   DevID                  = 0x01 (1)
        /// //   TempADC                = 0x09C4 (2500)
        /// //   Value                  = 3.14
        /// //   Price                  = 1234.56  (decimal 16B)
        /// //   Prices                 = [1234.56, 789.00, 100.50]
        /// //   Name                   = "Sensor-A"
        /// //   Payload                = [AA 01 00 0C 41 20 00 00]
        ///
        /// // 로그 파일에 저장
        /// File.AppendAllText("parse_log.txt",
        ///     $"[{DateTime.Now:HH:mm:ss.fff}]\n{result}\n");
        /// </code></example>
        /// </summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (k, v) in _d)
                sb.AppendLine($"  {k,-22} = {FormatVal(v)}");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 특정 byte[] 필드를 HexDump 형식으로 반환합니다.
        /// 컴퓨터의 이진 데이터(byte[])를 사람이 읽을 수 있도록 16진수(Hexadecimal)와 텍스트(ASCII)를 결합하여 보여주는 출력 형식
        /// [메모리 주소] | [16진수 데이터 (Hex)]                             | [텍스트 변환 (ASCII)]
        ///    00000000   | 48 65 6c 6c 6f 20 57 6f  72 6c 64 21 00 00 00 00  | Hello World!....|
        /// <para>오프셋(16진수) + HEX 바이트 + ASCII 문자를 3열 구조로 표시합니다.</para>
        /// <example><code>
        /// // Raw 페이로드 상세 분석
        /// Console.WriteLine(result.ToDump("Payload"));
        /// // Payload (32 bytes):
        /// //   00000000: AA 01 00 0C 00 00 03 E9  41 20 00 00 48 65 6C 6C  ........A ...Hell
        /// //   00000010: 6F 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  o...............
        ///
        /// // decimal 필드의 내부 바이트 확인 (디버깅)
        /// // decimal 필드는 byte[] 가 아니므로 raw[] 로 변환 필요
        /// byte[] decBytes = result.GetDecimal("Price").ToBytes();
        /// Console.WriteLine(decBytes.ToHexString());  // 16바이트 LE
        /// </code></example>
        /// </summary>
        /// <param name="fieldName">byte[] 타입 필드 이름 (<see cref="BufType.Raw"/> 등).</param>
        /// <param name="bytesPerLine">한 줄에 표시할 바이트 수 (기본 16).</param>
        public string ToDump(string fieldName, int bytesPerLine = 16)
        {
            if (!_d.TryGetValue(fieldName, out var v) || v is not byte[] data)
                return $"  {fieldName}: byte[] 타입 필드가 아닙니다.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"  {fieldName} ({data.Length} bytes):");
            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                sb.Append($"    {i:X8}: ");
                int len = Math.Min(bytesPerLine, data.Length - i);
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (j < len) sb.Append($"{data[i + j]:X2} ");
                    else sb.Append("   ");
                    if (j == bytesPerLine / 2 - 1) sb.Append(" ");
                }
                sb.Append(" ");
                for (int j = 0; j < len; j++)
                    sb.Append(data[i + j] is >= 0x20 and < 0x7F ? (char)data[i + j] : '.');
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        // ── 내부 포맷 헬퍼 ────────────────────────────────────────────
        internal static string FormatVal(object v) => v switch
        {
            byte b => $"0x{b:X2} ({b})",
            ushort u => $"0x{u:X4} ({u})",
            uint u => $"0x{u:X8} ({u})",
            ulong u => $"0x{u:X16} ({u})",
            sbyte b => b.ToString(),
            short s => s.ToString(),
            int i => i.ToString(),
            long l => l.ToString(),
            float f => $"{f:G9}",
            double d => $"{d:G12}",
            decimal m => $"{m:G}  (decimal 16B)",
            bool b => b.ToString(),
            string s => $"\"{s}\"",
            byte[] a => "[" + string.Join(" ", a.Select(x => $"{x:X2}")) + "]",
            short[] a => "[" + string.Join(", ", a) + "]",
            ushort[] a => "[" + string.Join(", ", a) + "]",
            int[] a => "[" + string.Join(", ", a) + "]",
            float[] a => "[" + string.Join(", ", a.Select(x => $"{x:G6}")) + "]",
            double[] a => "[" + string.Join(", ", a.Select(x => $"{x:G8}")) + "]",
            decimal[] a => "[" + string.Join(", ", a.Select(x => $"{x:G}")) + "]",
            _ => v?.ToString() ?? "null"
        };
    }
}