// ====================================================================
//  lssLib.Binary — BufSchema.cs
//  버퍼 파싱 스키마 빌더
//
//  [설계 원칙]
//  - Add/Then 체이닝으로 선언적 스키마 정의
//  - Then(): 직전 필드 직후 offset 자동 계산 → 순차 프레임에서 오류 방지
//  - Add(): offset 직접 지정 → 복잡한 프레임 구조에 유연하게 대응
//  - Clone(): 기본 스키마에서 버전별 확장
//  - FromJson(): 설정 파일 기반 런타임 동적 스키마 생성
//  - 정적 readonly 선언으로 반복 사용 시 GC 부담 최소화
//
//  [성능 팁]
//  동일 스키마를 매 프레임마다 new 로 생성하면 GC 압력 증가.
//  static readonly 또는 캐시를 활용하면 파싱 성능이 크게 향상됩니다.
//  예:  static readonly BufSchema _schema = new BufSchema().Then(...);
// ====================================================================

using System.Text.Json;

namespace lssLib.Binary
{
    /// <summary>
    /// 버퍼 파싱 스키마. 필드 이름·타입·오프셋을 체이닝 방식으로 정의합니다.
    ///
    /// <para><b>■ 두 가지 정의 방식</b></para>
    /// <para><b>Add</b>: offset 을 직접 지정. 비순차적 구조, 불규칙한 패딩이 있는 프레임에 적합.</para>
    /// <para><b>Then</b>: 직전 필드 직후에 offset 자동 계산. 순차적 프레임에서 계산 오류 방지.</para>
    ///
    /// <para><b>■ 성능 최적화 — 정적 사전 정의</b></para>
    /// <para>반복 파싱 시 매번 new BufSchema() 생성은 GC 부담. <c>static readonly</c> 권장.</para>
    ///
    /// <para><b>■ 런타임 동적 스키마 — FromJson</b></para>
    /// <para>JSON 설정 파일에서 스키마를 로드해 런타임에 결정. 다중 장비 지원에 유용.</para>
    ///
    /// <example><code>
    /// // ─── 방식 1: Add — offset 직접 지정 ──────────────────────────
    ///
    /// // Modbus FC03 응답 프레임 파싱
    /// // [SlaveId:1B][FC:1B][ByteCount:1B][Reg0~3:2B BE × 4][CRC:2B LE]
    /// var modbusSchema = new BufSchema()
    ///     .Add("SlaveId",   BufType.UInt8,        offset:0)
    ///     .Add("FC",        BufType.UInt8,        offset:1)
    ///     .Add("ByteCount", BufType.UInt8,        offset:2)
    ///     .Add("Registers", BufType.UInt16BEArray,offset:3, size:4);  // 4개 레지스터
    ///
    /// // ─── 방식 2: Then — offset 자동 계산 ─────────────────────────
    ///
    /// // 커스텀 센서 프레임: [STX][FC][Len(2B BE)][TempADC(2B BE)][Value(4B BE)][Price(16B LE)][Name(16B)]
    /// var sensorSchema = new BufSchema()
    ///     .Then("STX",     BufType.UInt8)          // offset 0  (1B) → next:1
    ///     .Then("FC",      BufType.UInt8)          // offset 1  (1B) → next:2
    ///     .Then("Length",  BufType.UInt16BE)       // offset 2  (2B) → next:4
    ///     .Then("TempADC", BufType.UInt16BE)       // offset 4  (2B) → next:6
    ///     .Then("Value",   BufType.FloatBE)        // offset 6  (4B) → next:10
    ///     .Then("Price",   BufType.DecimalLE)      // offset 10 (16B)→ next:26
    ///     .Then("Name",    BufType.StringAscii, size:16); // offset 26 (16B)
    /// // → BufSchema [7 fields / 42 bytes]
    ///
    /// // ─── 성능 최적화: 정적 사전 정의 ────────────────────────────
    ///
    /// public static class Schemas
    /// {
    ///     // 한번만 생성, 반복 파싱에 재사용
    ///     public static readonly BufSchema Modbus = new BufSchema()
    ///         .Add("SlaveId",   BufType.UInt8,    offset:0)
    ///         .Add("FC",        BufType.UInt8,    offset:1)
    ///         .Add("ByteCount", BufType.UInt8,    offset:2)
    ///         .Add("Reg0",      BufType.UInt16BE, offset:3);
    ///
    ///     public static readonly BufSchema Sensor = new BufSchema()
    ///         .Then("STX",   BufType.UInt8)
    ///         .Then("Value", BufType.FloatBE)
    ///         .Then("Price", BufType.DecimalLE);
    /// }
    ///
    /// // 매 프레임마다 재사용 (GC 부담 없음)
    /// var result = frame.ToParser().Parse(Schemas.Sensor);
    ///
    /// // ─── Clone: 기본 스키마 확장 ─────────────────────────────────
    ///
    /// var baseSchema = new BufSchema()
    ///     .Then("STX", BufType.UInt8)
    ///     .Then("FC",  BufType.UInt8);
    ///
    /// var v1Schema = baseSchema.Clone()
    ///     .Then("Value", BufType.FloatBE);         // 버전 1 확장
    ///
    /// var v2Schema = baseSchema.Clone()
    ///     .Then("Value", BufType.DoubleBE)         // 버전 2 확장 (배정밀도)
    ///     .Then("Extra", BufType.UInt32LE);
    ///
    /// // ─── FromJson: 동적 스키마 로드 ──────────────────────────────
    ///
    /// // schema.json
    /// // [{"Name":"STX","Type":"UInt8","Offset":0},
    /// //  {"Name":"Value","Type":"FloatBE","Offset":1},
    /// //  {"Name":"Price","Type":"DecimalLE","Offset":5}]
    ///
    /// var schema = BufSchema.FromJson(File.ReadAllText("schema.json"));
    /// var result = raw.ToParser().Parse(schema);
    /// </code></example>
    /// </summary>
    public sealed class BufSchema
    {
        // ── 필드 정의 레코드 ─────────────────────────────────────────

        /// <summary>
        /// 스키마 필드 정의. BufSchema 내부에서만 생성됩니다.
        /// <para>직접 생성보다 <see cref="BufSchema.Add"/> / <see cref="BufSchema.Then"/> 사용 권장.</para>
        /// <example><code>
        /// // 필드 정보 조회
        /// var field = schema.GetField("Price");
        /// if (field != null)
        /// {
        ///     Console.WriteLine($"이름  : {field.Name}");
        ///     Console.WriteLine($"타입  : {field.Type}");
        ///     Console.WriteLine($"offset: {field.Offset}");
        ///     Console.WriteLine($"크기  : {BufSchema.FieldBytes(field)}B");
        /// }
        ///
        /// // 전체 필드 순회
        /// foreach (var f in schema.Fields)
        ///     Console.WriteLine($"  {f.Name,-16} offset={f.Offset,3}  {BufSchema.FieldBytes(f)}B");
        /// </code></example>
        /// </summary>
        public record Field(string Name, BufType Type, int Offset, int Size = 1);

        /// <summary>
        /// JSON 직렬화용 DTO. <see cref="FromFields"/> / <see cref="FromJson"/> 에서 사용.
        /// <example><code>
        /// // 설정 파일 구조 예시
        /// // [
        /// //   {"Name":"STX",   "Type":"UInt8",    "Offset":0, "Size":1},
        /// //   {"Name":"Value", "Type":"FloatBE",  "Offset":1, "Size":1},
        /// //   {"Name":"Price", "Type":"DecimalLE","Offset":5, "Size":1},
        /// //   {"Name":"Data",  "Type":"Raw",      "Offset":21,"Size":16}
        /// // ]
        /// var schema = BufSchema.FromJson(jsonText);
        /// </code></example>
        /// </summary>
        public record FieldDto
        {
            public string Name { get; init; } = "";
            public string Type { get; init; } = "";
            public int Offset { get; init; }
            public int Size { get; init; } = 1;
        }

        private readonly List<Field> _fields = [];

        /// <summary>
        /// 정의된 필드 목록 (읽기 전용).
        /// <example><code>
        /// // 전체 필드 정보 출력
        /// foreach (var f in schema.Fields)
        ///     Console.WriteLine($"  {f.Name,-16} {f.Type,-16} offset={f.Offset,3} {BufSchema.FieldBytes(f)}B");
        ///
        /// // 특정 필드 유무 확인
        /// bool hasPrice = schema.Fields.Any(f => f.Name == "Price");
        /// </code></example>
        /// </summary>
        public IReadOnlyList<Field> Fields => _fields;

        /// <summary>등록된 필드 수.</summary>
        public int Count => _fields.Count;

        /// <summary>
        /// 스키마의 총 바이트 수 (모든 필드 끝까지의 최대 offset + size).
        /// <example><code>
        /// var schema = new BufSchema()
        ///     .Then("STX",   BufType.UInt8)    // 1B
        ///     .Then("Value", BufType.FloatBE)  // 4B
        ///     .Then("Price", BufType.DecimalLE); // 16B
        ///
        /// Console.WriteLine(schema.TotalBytes);  // 21
        ///
        /// // 프레임 길이 검증
        /// if (rawFrame.Length &lt; schema.TotalBytes)
        ///     throw new InvalidDataException("프레임 너무 짧음");
        /// </code></example>
        /// </summary>
        public int TotalBytes => _fields.Count == 0 ? 0
            : _fields.Max(f => f.Offset + FieldBytes(f));

        // ── 필드 추가 메서드 ─────────────────────────────────────────

        /// <summary>
        /// offset 을 직접 지정하여 필드를 추가합니다.
        /// <para>비순차적 구조, 패딩이 있는 프레임, 특정 위치의 필드만 파싱할 때 사용.</para>
        /// <example><code>
        /// // 예시 프레임: [STX:1B][Pad:2B][Value:4B BE][Pad:2B][Price:16B LE][Name:16B ASCII]
        /// // 패딩(2B)을 건너뛰고 필요한 필드만 파싱
        /// var schema = new BufSchema()
        ///     .Add("STX",   BufType.UInt8,      offset:0)
        ///     // offset 1~2 는 패딩이므로 건너뜀
        ///     .Add("Value", BufType.FloatBE,    offset:3)
        ///     // offset 7~8 는 패딩이므로 건너뜀
        ///     .Add("Price", BufType.DecimalLE,  offset:9)   // 16바이트
        ///     .Add("Name",  BufType.StringAscii,offset:25, size:16);
        ///
        /// // 배열 필드 추가 (size = 원소 수)
        /// var schema2 = new BufSchema()
        ///     .Add("Regs",   BufType.UInt16BEArray, offset:3,  size:4)  // 4개 × 2B = 8B
        ///     .Add("Floats", BufType.FloatBEArray,  offset:11, size:3)  // 3개 × 4B = 12B
        ///     .Add("Prices", BufType.DecimalLEArray,offset:23, size:2); // 2개 × 16B = 32B
        ///
        /// // Modbus 응답 스키마 (Add 방식)
        /// var modbusResp = new BufSchema()
        ///     .Add("SlaveId",   BufType.UInt8,    offset:0)
        ///     .Add("FC",        BufType.UInt8,    offset:1)
        ///     .Add("ByteCount", BufType.UInt8,    offset:2)
        ///     .Add("Reg0",      BufType.UInt16BE, offset:3)
        ///     .Add("Reg1",      BufType.UInt16BE, offset:5)
        ///     .Add("Reg2",      BufType.UInt16BE, offset:7)
        ///     .Add("Reg3",      BufType.UInt16BE, offset:9);
        /// </code></example>
        /// </summary>
        /// <param name="name">필드 이름 (대소문자 무관, BufResult에서 동일하게 접근).</param>
        /// <param name="type">필드 데이터 타입 (<see cref="BufType"/> 참조).</param>
        /// <param name="offset">버퍼 내 시작 바이트 위치 (0-based).</param>
        /// <param name="size">배열 원소 수 또는 문자열/Raw 바이트 수 (기본값 1).</param>
        public BufSchema Add(string name, BufType type, int offset, int size = 1)
        {
            _fields.Add(new(name, type, offset, size));
            return this;
        }

        /// <summary>
        /// 직전 필드 직후에 자동으로 offset 을 계산하여 추가합니다.
        /// <para>순차적으로 이어지는 프레임 구조에서 offset 계산 실수를 방지합니다.</para>
        /// <para>첫 번째 필드는 offset=0 에서 시작합니다.</para>
        /// <example><code>
        /// // 커스텀 산업 장비 프레임 파싱
        /// // 구조: [STX:1B][DevID:1B][Seq:4B LE][TempADC:2B BE][Value:4B BE]
        /// //       [Price:16B LE][Prices:3개×16B=48B][Name:16B ASCII][CRC:2B LE]
        ///
        /// var schema = new BufSchema()
        ///     .Then("STX",    BufType.UInt8)              // offset 0  (1B)  → next:1
        ///     .Then("DevID",  BufType.UInt8)              // offset 1  (1B)  → next:2
        ///     .Then("Seq",    BufType.UInt32LE)           // offset 2  (4B)  → next:6
        ///     .Then("TempADC",BufType.UInt16BE)           // offset 6  (2B)  → next:8
        ///     .Then("Value",  BufType.FloatBE)            // offset 8  (4B)  → next:12
        ///     .Then("Price",  BufType.DecimalLE)          // offset 12 (16B) → next:28
        ///     .Then("Prices", BufType.DecimalLEArray, size:3) // offset 28 (48B) → next:76
        ///     .Then("Name",   BufType.StringAscii,    size:16) // offset 76 (16B) → next:92
        ///     .Then("CRC",    BufType.UInt16LE);          // offset 92 (2B)  → next:94
        ///
        /// Console.WriteLine(schema);
        /// // BufSchema [9 fields / 94 bytes]
        ///
        /// // offset 자동 계산 결과 확인
        /// foreach (var f in schema.Fields)
        ///     Console.WriteLine($"  {f.Name,-10} offset={f.Offset,3}B  size={BufSchema.FieldBytes(f),2}B");
        /// // STX        offset=  0B  size= 1B
        /// // DevID      offset=  1B  size= 1B
        /// // Seq        offset=  2B  size= 4B
        /// // TempADC    offset=  6B  size= 2B
        /// // Value      offset=  8B  size= 4B
        /// // Price      offset= 12B  size=16B  ← decimal 16바이트
        /// // Prices     offset= 28B  size=48B  ← decimal[] 3개
        /// // Name       offset= 76B  size=16B
        /// // CRC        offset= 92B  size= 2B
        ///
        /// // 파싱 및 값 접근
        /// var result = raw.ToParser().Parse(schema);
        ///
        /// // 편의 메서드 활용 (타입 몰라도 사용 가능)
        /// int   tempInt  = result.GetInt("TempADC");        // ushort → int
        /// float tempF    = result.GetFloat("TempADC");      // ushort → float
        /// decimal price  = result.GetDecimal("Price");      // decimal 그대로
        /// decimal[]pArr  = result.Get&lt;decimal[]&gt;("Prices"); // 배열 접근
        ///
        /// // Scale 변환
        /// double tempC = result.GetInt("TempADC").MapTo(0, 4095, -40.0, 125.0);
        /// </code></example>
        /// </summary>
        public BufSchema Then(string name, BufType type, int size = 1)
        {
            int next = _fields.Count == 0 ? 0 : _fields[^1].Offset + FieldBytes(_fields[^1]);
            _fields.Add(new(name, type, next, size));
            return this;
        }

        /// <summary>
        /// 필드 이름으로 정의를 조회합니다. 없으면 null 반환 (대소문자 무관).
        /// <example><code>
        /// var field = schema.GetField("Price");
        /// if (field != null)
        ///     Console.WriteLine($"Price: offset={field.Offset}, {BufSchema.FieldBytes(field)}B");
        ///
        /// // null 조건 연산자 활용
        /// int? priceOffset = schema.GetField("Price")?.Offset;
        /// int? priceSize   = schema.GetField("Price") is {} f ? BufSchema.FieldBytes(f) : null;
        ///
        /// // 다국어 대소문자 무관
        /// var f1 = schema.GetField("PRICE");  // 동일 결과
        /// var f2 = schema.GetField("price");  // 동일 결과
        /// </code></example>
        /// </summary>
        public Field? GetField(string name)
            => _fields.FirstOrDefault(f =>
                string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 특정 필드가 스키마에 존재하는지 확인합니다.
        /// <example><code>
        /// if (schema.Contains("Price"))
        ///     decimal price = result.GetDecimal("Price");
        ///
        /// // 런타임 장비 타입별 처리
        /// if (schema.Contains("DecimalPrice"))
        ///     ProcessFinancialData(result.GetDecimal("DecimalPrice"));
        /// else if (schema.Contains("FloatPrice"))
        ///     ProcessSensorData(result.GetFloat("FloatPrice"));
        /// </code></example>
        /// </summary>
        public bool Contains(string name) => GetField(name) is not null;

        /// <summary>스키마를 초기화합니다 (체이닝 유지).</summary>
        public BufSchema Clear() { _fields.Clear(); return this; }

        /// <summary>
        /// 스키마를 복제합니다. 기본 스키마를 기반으로 버전별·장비별 확장에 사용합니다.
        /// <para>원본 스키마에 영향을 주지 않고 독립적으로 필드를 추가할 수 있습니다.</para>
        /// <example><code>
        /// // 공통 헤더 스키마
        /// static readonly BufSchema BaseHeader = new BufSchema()
        ///     .Then("STX",     BufType.UInt8)
        ///     .Then("DevType", BufType.UInt8)
        ///     .Then("Seq",     BufType.UInt32LE);
        ///
        /// // 장비 타입별 페이로드 확장 (원본 불변)
        /// var sensorSchema = BaseHeader.Clone()
        ///     .Then("TempADC", BufType.UInt16BE)
        ///     .Then("HumiADC", BufType.UInt16BE)
        ///     .Then("CRC",     BufType.UInt8);
        ///
        /// var tradeSchema = BaseHeader.Clone()
        ///     .Then("Price",   BufType.DecimalLE)
        ///     .Then("Qty",     BufType.DecimalLE)
        ///     .Then("Total",   BufType.DecimalLE);
        ///
        /// var v2Schema = BaseHeader.Clone()
        ///     .Then("Flags",   BufType.UInt16BE)     // 버전2 추가 필드
        ///     .Then("Value",   BufType.DoubleBE)     // 배정밀도로 업그레이드
        ///     .Then("Price",   BufType.DecimalLE);
        ///
        /// // 사용
        /// var result = raw.ToParser().Parse(
        ///     devType == 0x01 ? sensorSchema : tradeSchema);
        /// </code></example>
        /// </summary>
        public BufSchema Clone()
        {
            var copy = new BufSchema();
            foreach (var f in _fields) copy._fields.Add(f);
            return copy;
        }

        // ── JSON 직렬화 / 동적 로드 ──────────────────────────────────

        /// <summary>
        /// 스키마를 JSON 문자열로 직렬화합니다.
        /// <para>런타임 스키마를 설정 파일로 저장하거나 네트워크 전송에 활용합니다.</para>
        /// <example><code>
        /// var schema = new BufSchema()
        ///     .Then("STX",   BufType.UInt8)
        ///     .Then("Value", BufType.FloatBE)
        ///     .Then("Price", BufType.DecimalLE);
        ///
        /// string json = schema.ToJson();
        /// // [
        /// //   {"Name":"STX",   "Type":"UInt8",   "Offset":0,"Size":1},
        /// //   {"Name":"Value", "Type":"FloatBE", "Offset":1,"Size":1},
        /// //   {"Name":"Price", "Type":"DecimalLE","Offset":5,"Size":1}
        /// // ]
        ///
        /// // 파일 저장 후 다음 실행 시 로드
        /// File.WriteAllText("device_schema.json", schema.ToJson());
        ///
        /// // 복원
        /// var loaded = BufSchema.FromJson(File.ReadAllText("device_schema.json"));
        /// </code></example>
        /// </summary>
        public string ToJson()
        {
            var dtos = _fields.Select(f => new FieldDto
            { Name = f.Name, Type = f.Type.ToString(), Offset = f.Offset, Size = f.Size });
            return JsonSerializer.Serialize(dtos,
                new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// JSON 문자열에서 스키마를 동적으로 생성합니다.
        /// <para>설정 파일, 데이터베이스, API 응답에서 장비별 스키마를 런타임에 결정할 때 사용합니다.</para>
        /// <example><code>
        /// // ─── 설정 파일 기반 다중 장비 지원 ──────────────────────────
        ///
        /// // device_configs/sensor_v1.json:
        /// // [{"Name":"STX","Type":"UInt8","Offset":0},
        /// //  {"Name":"TempADC","Type":"UInt16BE","Offset":1},
        /// //  {"Name":"Value","Type":"FloatBE","Offset":3}]
        ///
        /// // device_configs/trade_v2.json:
        /// // [{"Name":"STX","Type":"UInt8","Offset":0},
        /// //  {"Name":"Price","Type":"DecimalLE","Offset":1},
        /// //  {"Name":"Qty","Type":"DecimalLE","Offset":17}]
        ///
        /// // 장비 ID 기반으로 스키마 선택
        /// string schemaFile = devType switch
        /// {
        ///     0x01 => "device_configs/sensor_v1.json",
        ///     0x02 => "device_configs/trade_v2.json",
        ///     _    => throw new NotSupportedException($"미지원 장비: {devType}")
        /// };
        ///
        /// var schema = BufSchema.FromJson(File.ReadAllText(schemaFile));
        /// var result = raw.ToParser().Parse(schema);
        ///
        /// // 런타임 타입 확인 후 처리
        /// if (schema.Contains("Price"))
        ///     decimal price = result.GetDecimal("Price");
        /// </code></example>
        /// </summary>
        public static BufSchema FromJson(string json)
        {
            var dtos = JsonSerializer.Deserialize<FieldDto[]>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return FromFields(dtos);
        }

        /// <summary>
        /// FieldDto 컬렉션에서 스키마를 생성합니다.
        /// <para>DB나 API에서 가져온 필드 정의를 스키마로 변환할 때 사용합니다.</para>
        /// <example><code>
        /// // DB 에서 로드한 필드 정의
        /// var defs = dbContext.SchemaFields
        ///     .Where(f => f.SchemaId == deviceSchemaId)
        ///     .OrderBy(f => f.Offset)
        ///     .Select(f => new BufSchema.FieldDto
        ///     {
        ///         Name   = f.FieldName,
        ///         Type   = f.DataType,
        ///         Offset = f.ByteOffset,
        ///         Size   = f.ArraySize
        ///     });
        ///
        /// var schema = BufSchema.FromFields(defs);
        ///
        /// // C# 익명 타입으로도 가능
        /// var schema2 = BufSchema.FromFields(new[]
        /// {
        ///     new BufSchema.FieldDto { Name="STX",   Type="UInt8",   Offset=0 },
        ///     new BufSchema.FieldDto { Name="Value", Type="FloatBE", Offset=1 },
        ///     new BufSchema.FieldDto { Name="Price", Type="DecimalLE", Offset=5 },
        /// });
        /// </code></example>
        /// </summary>
        public static BufSchema FromFields(IEnumerable<FieldDto> defs)
        {
            var schema = new BufSchema();
            foreach (var d in defs)
                if (Enum.TryParse<BufType>(d.Type, out var bt))
                    schema.Add(d.Name, bt, d.Offset, d.Size);
            return schema;
        }

        // ── 유틸 메서드 ───────────────────────────────────────────────

        /// <summary>
        /// 필드가 소비하는 바이트 수를 반환합니다.
        /// <para>decimal = 16B, decimal배열 = Size×16B, double = 8B, float/int32 = 4B, int16 = 2B, int8 = 1B.</para>
        /// <example><code>
        /// // 각 타입별 크기 확인
        /// var f1 = new BufSchema.Field("price",  BufType.DecimalLE,      0);
        /// var f2 = new BufSchema.Field("prices", BufType.DecimalLEArray, 0, size:3);
        /// var f3 = new BufSchema.Field("value",  BufType.FloatBE,        0);
        /// var f4 = new BufSchema.Field("data",   BufType.Raw,            0, size:32);
        ///
        /// BufSchema.FieldBytes(f1)  // → 16  (decimal 1개)
        /// BufSchema.FieldBytes(f2)  // → 48  (decimal 3개 × 16B)
        /// BufSchema.FieldBytes(f3)  // →  4  (float 4B)
        /// BufSchema.FieldBytes(f4)  // → 32  (Raw 32B)
        ///
        /// // 스키마 총 크기 확인
        /// int totalBytes = schema.TotalBytes;
        /// int calcBytes  = schema.Fields.Sum(BufSchema.FieldBytes);
        /// </code></example>
        /// </summary>
        public static int FieldBytes(Field f) => f.Type switch
        {
            BufType.Int8 or BufType.UInt8 or BufType.Bool or BufType.Bit => 1,
            BufType.Int16BE or BufType.Int16LE or BufType.UInt16BE or BufType.UInt16LE => 2,
            BufType.Int32BE or BufType.Int32LE or BufType.UInt32BE or BufType.UInt32LE
                or BufType.FloatBE or BufType.FloatLE => 4,
            BufType.Int64BE or BufType.Int64LE or BufType.UInt64BE or BufType.UInt64LE
                or BufType.DoubleBE or BufType.DoubleLE => 8,
            BufType.DecimalLE or BufType.DecimalBE => 16,
            BufType.StringAscii or BufType.StringUtf8 or BufType.StringHex
                or BufType.StringBase64 or BufType.Raw
                or BufType.Int8Array or BufType.UInt8Array => f.Size,
            BufType.Int16BEArray or BufType.Int16LEArray
                or BufType.UInt16BEArray or BufType.UInt16LEArray => f.Size * 2,
            BufType.Int32BEArray or BufType.Int32LEArray
                or BufType.UInt32BEArray or BufType.UInt32LEArray
                or BufType.FloatBEArray or BufType.FloatLEArray => f.Size * 4,
            BufType.DoubleBEArray or BufType.DoubleLEArray => f.Size * 8,
            BufType.DecimalLEArray or BufType.DecimalBEArray => f.Size * 16,
            _ => f.Size,
        };

        /// <summary>
        /// 스키마 요약 문자열을 반환합니다.
        /// <example><code>
        /// Console.WriteLine(schema.ToString());
        /// // BufSchema [7 fields / 42 bytes]
        /// </code></example>
        /// </summary>
        public override string ToString()
            => $"BufSchema [{_fields.Count} fields / {_fields.Sum(FieldBytes)} bytes]";
    }
}