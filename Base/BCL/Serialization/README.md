# lssLib.Serialization v5

**공용 바이너리 직렬화 라이브러리** · .NET 8.0 · C# 12 · WPF 데모 포함

---

## 개요

임베디드 장비·산업용 프로토콜·금융 데이터 처리에 필요한  
**바이너리 파싱 / 프레임 생성 / 스트림 처리** 기능을  
인터페이스·추상 클래스 없이 **확장 메서드만으로** 제공하는 공용 라이브러리입니다.

```
Abstractions (interface / abstract class) 완전 제거
→ 모든 기능은 확장 메서드로 제공
→ 상속 없이 체이닝으로 조합
```

---

## 솔루션 구성

```
lssLib.Serialization.sln              (.NET 8.0-windows)
│
├── lssLib.Binary                     ── 바이너리 처리 라이브러리
│   ├── BufType.cs                       타입 열거형
│   ├── BufSchema.cs                     파싱 스키마 빌더
│   ├── BufResult.cs                     파싱 결과
│   ├── BufferParser.cs                  읽기 파서  + BufferWriter + StreamParser
│   ├── BinaryExtensions.cs             byte[] / Struct / decimal 확장
│   ├── RingBuffer.cs                    수신 순환 버퍼
│   └── BufferDiff.cs                    버퍼 비교·차이 분석
│
├── lssLib.Extensions                 ── 확장 메서드 라이브러리
│   ├── TextExtensions.cs                string / object 확장
│   ├── ScaleExtensions.cs               수치 변환 확장
│   └── CrcExtensions.cs                 CRC / Checksum 확장
│
└── lssLib.Serialization.WpfDemo      ── WPF .NET 8.0 데모 앱
    └── Views/
        ├── BufferView    🔍            BufferParser + BufferWriter + StreamParser
        ├── HookView      🔗            훅 체이닝 (WithLog / XorDecrypt / Stats)
        ├── DecimalView   💰            decimal 전용 데모
        ├── TypeView      🔤            Text 파싱 + 직렬화
        ├── ScaleView     📐            수치 변환 + SmoothStep + Hysteresis
        ├── CrcView       🔒            CRC / Checksum
        └── CompositeView ⚡            종합 시나리오 16개
```

---

## 개발 환경

| 항목 | 버전 |
|---|---|
| .NET | 8.0-windows |
| C# | 12 (latest) |
| WPF | .NET 8.0-windows (UseWPF=true) |
| Nullable | enable |
| ImplicitUsings | enable |
| 외부 패키지 | System.Text.Json 8.0.0 (lssLib.Extensions만) |

---

## lssLib.Binary — 바이너리 처리

### 프로젝트 설정

```xml
<!-- lssLib.Binary.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AssemblyName>lssLib.Binary</AssemblyName>
    <RootNamespace>lssLib.Binary</RootNamespace>
  </PropertyGroup>
</Project>
```

### using

```csharp
using lssLib.Binary;
```

### BufferParser — 읽기 파서

```csharp
// 팩토리
var bp = rawBytes.ToParser();               // byte[] 확장 메서드
var bp = "AA BB CC".ToParser();             // HEX 문자열
var bp = BufferParser.FromHex("AA BB CC");

// 직접 읽기
byte    stx   = bp.ReadUInt8(0);
ushort  len   = bp.ReadUInt16BE(2);
float   val   = bp.ReadFloatBE(4);
decimal price = bp.ReadDecimalLE(8);        // 16바이트 고정소수점
decimal[]arr  = bp.ReadDecimalLEArray(0,3); // 배열
bool    bit3  = bp.ReadBit(offset:1, bit:3);

// 스키마 파싱 — Then (offset 자동 계산)
var schema = new BufSchema()
    .Then("STX",    BufType.UInt8)          // 0 (1B)
    .Then("FC",     BufType.UInt8)          // 1 (1B)
    .Then("Length", BufType.UInt16BE)       // 2 (2B)
    .Then("Value",  BufType.FloatBE)        // 4 (4B)
    .Then("Price",  BufType.DecimalLE)      // 8 (16B)
    .Then("Prices", BufType.DecimalLEArray, size:3); // 24 (48B)

var result = bp.Parse(schema);

// BufResult — 타입 변환 편의 메서드
int     adcInt = result.GetInt("TempADC");       // ushort → int 자동
float   adcF   = result.GetFloat("TempADC");     // ushort → float 자동
decimal priceD = result.GetDecimal("Price");     // decimal 그대로
string  name   = result.GetString("Name");       // 모든 타입 → string
byte[]  raw2   = result.GetRaw("Payload");       // byte[]

// 안전 접근
byte stxSafe = result.GetOr<byte>("STX", 0x00);
byte stxReq  = result.GetOrThrow<byte>("STX", "STX 필드 누락");

// 상태 확인
bool ok    = result.IsAllOk;
var errs   = result.ErrorFields;

// HexDump
Console.WriteLine(bp.ToHexDump());
// 00000000: AA 01 00 0C 00 00 03 E9  41 20 00 00 ...

// byte[] 필드 HexDump
Console.WriteLine(result.ToDump("Payload"));

// 훅 체이닝
var result2 = rawBytes
    .ToParser()
    .WithLog(Console.WriteLine)
    .WithXorDecrypt(0xAA)
    .WithStats(statsDict)
    .OnParseDone((r, s) =>
    {
        if (!r.IsAllOk) throw new Exception($"오류: {string.Join(", ", r.ErrorFields)}");
        r.GetOrThrow<byte>("STX", "STX 없음");
    })
    .Parse(schema);
```

### BufferWriter — 프레임 빌더

```csharp
// 기본 체이닝
byte[] frame = BufferWriter.Create()
    .WriteUInt8(0xAA)
    .WriteUInt16BE(256)
    .WriteFloatBE(3.14f)
    .WriteDecimalLE(123.456m)               // 16바이트
    .WriteDecimalLEArray(prices)            // decimal[] 배열
    .WriteStringAscii("Hello", fixedLen:16)
    .WritePad(4)
    .ToArray();

// PatchByte — 체크섬 나중에 삽입
var bw = BufferWriter.Create()
    .WriteUInt8(0xAA).WriteFloatBE(3.14f).WritePad(1);  // 체크섬 자리 예약
byte cs = bw.ToArray().Sum8(0, bw.Length - 1);
bw.PatchByte(offset: bw.Length - 1, cs);

// PatchUInt16BE — Length 필드 채우기
var bw2 = BufferWriter.Create()
    .WriteUInt8(0xAA).WriteUInt16BE(0).WriteFloatBE(3.14f);
bw2.PatchUInt16BE(offset:1, (ushort)(bw2.Length - 3));

// ToParser — 쓰고 즉시 읽어서 검증
decimal restored = BufferWriter.Create()
    .WriteDecimalLE(123.456m)
    .ToParser()
    .ReadDecimalLE(0);  // 123.456m (손실 없음)
```

### StreamParser — 스트림 프레임 탐색

```csharp
var sp = new StreamParser(rxBuf);

// 고정 길이 프레임 (STX=0xAA, 32바이트)
while (sp.FindNext(stx:0xAA, frameLen:32, out int offset))
{
    var result = sp.Slice(offset, 32).ToParser().Parse(schema);
    sp.Advance(offset + 32);
}

// 가변 길이 프레임
// 구조: [STX:1B][FC:1B][Length:2B BE][Data:NB][CRC32:4B]
while (sp.Remaining > 8)
{
    if (!sp.FindNext(stx:0xAA, minFrameLen:8, out int pos)) break;
    int dataLen = sp.ReadUInt16BE(pos + 2);
    int total   = 1 + 1 + 2 + dataLen + 4;
    if (!sp.HasBytes(pos, total)) break;
    byte[] frame = sp.Slice(pos, total);
    if (frame.VerifyCrc32())
        Process(frame[..^4].ToParser().Parse(schema));
    sp.Advance(pos + total);
}

// 다중 STX 패턴
while (sp.FindNext(new byte[]{0xAA, 0xBB}, 16, out int pos, out byte found))
{
    var frameSchema = found == 0xAA ? schemaA : schemaB;
    sp.Advance(pos + 16);
}
```

### RingBuffer — 수신 순환 버퍼

```csharp
var ring = new RingBuffer(capacity:4096);
var ring = new RingBuffer(4096, threadSafe:true);  // Thread-safe

// 수신 루프
ring.Write(buf, offset:0, count:n);
ring.WriteByte(0xAA);
bool ok = ring.TryWrite(data);  // 오버플로우 시 false

// 고정 길이 프레임 추출
while (ring.TryReadFrame(stx:0xAA, length:32, out byte[] frame))
{
    var result = frame.ToParser().Parse(schema);
    // STX 이전 쓰레기 데이터 자동 제거
}

// 가변 길이 프레임 추출
while (ring.TryReadVariableFrame(
    stx:0xAA, lengthOffset:2, lengthSize:2,
    bigEndian:true, overhead:8, out byte[] frame))
{
    if (frame.VerifyCrc32()) Process(frame[..^4]);
}

// Peek — 소비 없이 미리 보기
byte stx   = ring.PeekByte(0);
byte[] hdr = ring.Peek(4);

// 유틸
ring.SkipTo(stx:0xAA);  // STX 이전 버림
ring.Clear();
Console.WriteLine(ring.ToString());  // "RingBuffer[512/4096 bytes]"
```

### BufferDiff — 버퍼 비교 분석

```csharp
// 전체 비교
var diff = BufferDiff.Compare(oldBuf, newBuf);
Console.WriteLine(diff.ToPatchString());
// [Diff]  A=18B  B=18B  변경=2바이트  유사도=88.9%
//   offset=0x0004  0x41 → 0x42

bool changed = diff.HasChanges;
double sim   = diff.Similarity;       // 0.0~1.0

// 마스킹 비교 (CRC / 타임스탬프 제외)
bool eq = oldBuf.MaskedEquals(newBuf,
    ignoreOffsets: new[]{ frame.Length-2, frame.Length-1 });

// 스키마 기반 마스킹
bool eq2 = oldBuf.MaskedEquals(newBuf, schema,
    ignoreFields: new[]{"Timestamp", "CRC"});

// 필드 단위 비교
var fdiff = BufferDiff.CompareFields(oldBuf, newBuf, schema,
    ignoreFields: new[]{"Timestamp","CRC"});
Console.WriteLine(fdiff.Summary);
bool configChanged = fdiff.HasFieldChanged("Config");

// 패치 적용
byte[] restored = BufferDiff.ApplyPatches(oldBuf, diff);
bool ok2 = BufferDiff.IsEqual(restored, newBuf);  // true
```

### BufSchema 고급 기능

```csharp
// 정적 사전 정의 — 성능 최적화 (new 없이 재사용)
public static class SensorSchema
{
    public static readonly BufSchema Default = new BufSchema()
        .Then("STX",   BufType.UInt8)
        .Then("Value", BufType.FloatBE)
        .Then("Price", BufType.DecimalLE);
}
var result = raw.ToParser().Parse(SensorSchema.Default);

// Clone — 기본 스키마 확장
var base_ = new BufSchema().Then("STX", BufType.UInt8).Then("FC", BufType.UInt8);
var v1 = base_.Clone().Then("Value", BufType.FloatBE);
var v2 = base_.Clone().Then("Value", BufType.DoubleBE).Then("Extra", BufType.UInt32LE);

// JSON 동적 로드
var schema = BufSchema.FromJson(File.ReadAllText("schema.json"));
var schema = BufSchema.FromFields(fieldDtoList);

// 조회
bool  has   = schema.Contains("Price");
var   field = schema.GetField("Price");
int   total = schema.TotalBytes;
```

### decimal 지원

```csharp
// 직렬화 (16바이트 LE / BE)
byte[] le = 123.456m.ToBytes();
byte[] be = 123.456m.ToBigEndianBytes();

// 역직렬화
decimal v = le.ReadDecimalLE();
decimal v = BufferParser.From(le).ReadDecimalLE(0);

// 배열
decimal[] prices  = { 1234.56m, 789.00m };
byte[]    rawArr  = prices.ToLEBytes();                    // 32B
decimal[] restored= rawArr.ToDecimalLEArray(0, count:2);  // 복원

// 내부 구조 분해
var info = 123.456m.Decompose();
// decimal 123.456  Sign=양수  Scale=3
// GetBits = [0x0001E240, 0x00, 0x00, 0x00030000]
// Bytes(LE) = 40 E2 01 00 ...

// BufResult 자동 변환
decimal price = result.GetDecimal("TempADC"); // ushort → decimal 자동
```

---

## lssLib.Extensions — 확장 메서드

### 프로젝트 설정

```xml
<!-- lssLib.Extensions.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AssemblyName>lssLib.Extensions</AssemblyName>
    <RootNamespace>lssLib.Extensions</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    <ProjectReference Include="..\lssLib.Binary\lssLib.Binary.csproj" />
  </ItemGroup>
</Project>
```

### using

```csharp
using lssLib.Extensions;
```

### ScaleExtensions — 수치 변환

```csharp
// 선형 변환 (double / float / int / decimal)
double volt = 2048.0.MapTo(0, 4095, 0.0, 3.3);          // ≈ 1.65V
float  pwm  = 50f.MapTo(0f, 100f, 0f, 255f);
decimal fee = 1300m.MapTo(1200m, 1400m, 0m, 100m);      // 금융

// Scale + BufResult 연동
double temp = result.GetInt("TempADC").MapTo(0, 4095, -40.0, 125.0);

// Normalize / Denormalize
double n  = 2048.0.Normalize(0, 4095);                   // ≈ 0.5
double dn = 0.5.Denormalize(0.0, 3.3);                   // ≈ 1.65V

// Lerp / InverseLerp
double v = 0.75.Lerp(from:0, to:100);                    // 75.0
double t = 75.0.InverseLerp(0, 100);                     // 0.75

// Clamp / DeadZone
double c  = 1.5.Clamp(0.0, 1.0);                         // 1.0
double dz = 0.05.DeadZone(deadZone:0.1);                 // 0.0

// Piecewise — 비선형 센서 보정
double tmp = 750.0.Piecewise(
    new[]{0.0,100,500,1000,4095},
    new[]{-10.0,0,25,85,125});                            // ≈ 47.5°C

// SmoothStep — 3차 S커브 (게임/UI/LED 페이드)
// 수식: 3t²-2t³  (시작·끝에서 미분=0)
double s1 = 0.2.SmoothStep(from:0, to:100);              // ≈ 10.4
double s2 = 0.5.SmoothStep(from:0, to:100);              // 50.0
float  sf = 0.3f.SmoothStep(from:0f, to:255f);

// SmootherStep — 5차 S커브 (더 부드러운 전환)
// 수식: 6t⁵-15t⁴+10t³
double ss = 0.2.SmootherStep(from:0, to:100);            // ≈ 5.7

// Hysteresis — 노이즈 방지 임계값
// x > high → true,  x < low → false,  그 사이 → 이전 상태 유지
bool alarm = false;
foreach (double temp2 in sensorReadings)
    alarm = temp2.Hysteresis(ref alarm, low:60.0, high:65.0);

// float 오버로드
bool fanOn = false;
fanOn = humidity.Hysteresis(ref fanOn, 20.0f, 30.0f);

// 순수 함수 — LINQ/람다 호환 (ref 없음)
bool state = readings.Aggregate(false,
    (s, v) => v.HysteresisPure(s, low:60.0, high:65.0));
```

### TextExtensions — 문자열 파싱 + 직렬화

```csharp
// 자료형 파싱 (접두사 자동 인식)
int     v1 = "0xFF".ToInt32();                // HEX → 255
int     v2 = "0b11111111".ToInt32();          // BIN → 255
int     v3 = "0o377".ToInt32();               // OCT → 255
float   f1 = "0x3F800000".ToFloat();          // IEEE 754 재해석 → 1.0f
decimal d1 = "123.456789".ToDecimal();        // 28~29자리 정밀

// IEEE 754 분석
var info = 3.14f.Analyze();
// float 3.14159274  0x4048F5C3  Sign=0  Exp=1  Mantissa=0x48F5C3

// 안전 파싱
bool ok = "0xFF".TryParse<int>(out int r);
decimal dv = "bad".ParseOr<decimal>(0m);      // 0m

// JSON / XML / CSV
string json = myObj.ToJson();
T      obj  = jsonStr.FromJson<T>();
string xml  = myObj.ToXml();
string csv  = myList.ToCsv();                  // decimal 정밀도 유지
List<T> lst = csvStr.FromCsv<T>();

await myObj.SaveJsonAsync("data.json");
var loaded = await "data.json".LoadJsonAsync<T>();
```

### CrcExtensions — CRC / Checksum

```csharp
// 알고리즘
byte   c8    = data.Crc8();
ushort c16   = data.Crc16();
ushort ccitt = data.Crc16Ccitt(init:0xFFFF);   // BLE
ushort modbus= data.Crc16Modbus();             // Modbus RTU
uint   c32   = data.Crc32();
byte   sum   = data.Sum8();
byte   sum2  = data.Sum8Twos();                // frame.Sum8() == 0x00
byte   xor   = data.Xor();                    // NMEA GPS

// SHT3x 전용 (poly=0x31, init=0xFF)
byte sht = new byte[]{0x65, 0x66}.Crc8Sht();

// offset / length 지정
uint partial = data.Crc32(offset:1, length:3);

// Append & Verify
byte[] w  = data.AppendCrc32();
bool   ok = w.VerifyCrc32();

// 파일 CRC-32
uint fc = await "firmware.bin".Crc32File();
```

---

## WpfDemo 탭 구성

```
lssLib.Serialization.WpfDemo  (.NET 8.0-windows / WPF)
```

```xml
<ItemGroup>
  <ProjectReference Include="..\lssLib.Binary\lssLib.Binary.csproj" />
  <ProjectReference Include="..\lssLib.Extensions\lssLib.Extensions.csproj" />
</ItemGroup>
```

| 탭 | 주요 기능 |
|---|---|
| 🔍 BufferParser | Add/Then 스키마, 직접 읽기, 배열, 비트/HexDump<br>**BufferWriter** Write 체이닝 / Patch / ToParser 검증<br>**StreamParser** 고정·가변·다중STX 프레임 탐색 |
| 🔗 훅 체이닝 | WithLog, WithXorDecrypt, WithStats, OnParseDone, WithOffset, WithPreprocess |
| 💰 Decimal | decimal ↔ byte[], Decompose, 배열 변환, GetDecimal 편의 메서드 |
| 🔤 Text | ToInt32/Float/Decimal, Analyze(IEEE754), ToJson/Xml/Csv, TryParse |
| 📐 Scale | MapTo, Normalize, Lerp, Clamp, DeadZone, Piecewise<br>**SmoothStep** / **SmootherStep** S커브 보간<br>**Hysteresis** 히스테리시스 (ref + 순수 함수) |
| 🔒 CRC | 전체 알고리즘 비교, AppendCrc32/Verify, Crc8Sht, NMEA XOR |
| ⚡ 종합 예제 | **16개 시나리오** (아래 표 참조) |

### 종합 예제 16개 시나리오

| 번호 | 제목 | 핵심 조합 |
|---|---|---|
| ① | ADC 센서 프레임 파싱 + 스케일링 | BufferParser + Scale + CRC-8 |
| ② | 금융 데이터 프레임 (decimal) | BufferWriter + DecimalLE + GetDecimal |
| ③ | XOR 암호화 프레임 투명 파싱 | WithXorDecrypt |
| ④ | 다중 프레임 슬라이딩 윈도우 | WithOffset |
| ⑤ | CRC-32 검증 후 조건부 파싱 | AppendCrc32 + VerifyCrc32 |
| ⑥ | JSON 설정 → 스키마 동적 생성 | BufSchema.FromJson |
| ⑦ | 파싱 → Scale → CSV 파이프라인 | MapTo + ToCsv |
| ⑧ | struct ↔ byte[] 왕복 직렬화 | ToBytes / To\<T\> / Dump |
| ⑨ | 요청 프레임 생성 + 응답 파싱 | BufferWriter + Crc16Modbus |
| ⑩ | Writer → Parser 왕복 검증 | BufferWriter.ToParser() |
| ⑪ | RingBuffer 수신 스트림 처리 | TryReadFrame + 쓰레기 자동 제거 |
| ⑫ | RingBuffer 가변 길이 프레임 | TryReadVariableFrame + threadSafe |
| ⑬ | 프레임 변경 감지 | BufferDiff.Compare |
| ⑭ | CRC/타임스탬프 마스킹 비교 | MaskedEquals |
| ⑮ | 스키마 기반 필드 단위 비교 | CompareFields + HasFieldChanged |
| ⑯ | 펌웨어 패치 생성 + 적용 | Compare + ApplyPatches |

---

## BufType 지원 타입

| 분류 | 타입 | 크기 |
|---|---|---|
| 정수 | Int8/UInt8 | 1B |
| 정수 | Int16/UInt16 BE/LE | 2B |
| 정수 | Int32/UInt32 BE/LE | 4B |
| 정수 | Int64/UInt64 BE/LE | 8B |
| 실수 | FloatBE/LE | 4B |
| 실수 | DoubleBE/LE | 8B |
| **고정소수점** | **DecimalLE/BE** | **16B** |
| 논리 | Bool / Bit | 1B |
| 문자열 | StringAscii/Utf8/Hex/Base64 | Size B |
| 원시 | Raw | Size B |
| 배열 | Int16~UInt32 / Float / Double 배열 | Size × nB |
| **배열** | **DecimalLEArray / BEArray** | **Size × 16B** |

---

*lssLib.Serialization v5 · .NET 8.0 · WPF · 확장 메서드 전용 아키텍처*
