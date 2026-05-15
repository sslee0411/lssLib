# lssLib.Utils

> lssLib 생태계 범용 유틸리티 모듈 — `v2.0.0`

---

## 개요

| 항목 | 값 |
|---|---|
| 네임스페이스 | `lssLib.Utils` |
| 타겟 프레임워크 | `net8.0` |
| 외부 의존성 | **없음** (BCL only) |
| 설계 원칙 | Extension-method only · No abstractions · `[GeneratedRegex]` |

lssLib 아키텍처 원칙을 따르는 순수 범용 유틸리티 라이브러리입니다.  
인터페이스·추상 클래스 없이 확장 메서드만으로 구성되며,  
`lssLib.Binary` · `lssLib.Extensions` · `lssLib.Retry`와 파이프라인 연계가 가능합니다.

---

## 파일 구성

```
lssLib.Utils/
├── lssLib.Utils.csproj
│
├── Guard.cs               — 인수 선행 검증 (CallerArgumentExpression)
│
├── StringPatterns.cs      — [GeneratedRegex] 전용 partial class (컴파일 타임 생성)
├── StringExtensions.cs    — 문자열 조작 · 변환 · 파싱 · 인코딩
├── DateTimeExtensions.cs  — DateTime / TimeSpan 포맷 · 변환 · 범위 판단
└── FileExtensions.cs      — 파일 · 디렉터리 처리 (체이닝 파이프라인)
```

> Retry · UtilResult 계열은 `lssLib.Retry` 패키지로 분리되었습니다.

---

## lssLib 생태계 내 위치

```
lssLib.Binary   ──────────────────────────────────────────────────►  lssLib.Extensions
   (BufferWriter                                                       (CrcExtensions
    BufferParser                                                        ScaleExtensions
    StreamParser                                                        TextExtensions)
    RingBuffer
    BufferDiff)
        │
        └─────────────────────────────────────────────────────────►  lssLib.Utils
                                                                       (Guard
                                                                        StringExtensions
                                                                        DateTimeExtensions
                                                                        FileExtensions)
                                                                            │
                                                                            ▼
                                                                       lssLib.Retry
                                                                       (Retry · CircuitBreaker
                                                                        RateLimiter · UtilResult)
                                                                            │
                                                                            ▼
                                                                   lssLib.Serialization.WpfDemo
                                                                   (7탭 · 8 복합 시나리오)
```

---

## lssLib.Serialization.WpfDemo 연동 맥락

`lssLib.Serialization.WpfDemo`는 lssLib Binary/Extensions 생태계를 검증하는  
7탭 WPF 애플리케이션으로, 8개 복합 시나리오를 실행합니다.  
각 시나리오에서 `lssLib.Utils`가 담당하는 역할은 다음과 같습니다.

| 탭 / 시나리오 | lssLib.Utils 역할 |
|---|---|
| 탭1 STX 프레임 수신 | `Guard.NotEmpty(rawBuffer)` — BufferParser 진입 전 검증 |
| 탭2 CRC 검증 파이프라인 | `Guard.That(crc == expected)` — CRC 불일치 즉시 차단 |
| 탭3 RingBuffer 스트리밍 | `DateTimeExtensions.ToIsoDateTime` — 프레임 타임스탬프 로그 |
| 탭4 BufferDiff 비교 | `FileExtensions.WriteBytes` — diff 결과 바이너리 저장 |
| 탭5 SmoothStep 신호 처리 | `StringExtensions.ToDecimalOrNull` — 설정 임계값 파싱 |
| 탭6 TextExtensions 직렬화 | `FileExtensions.AppendLine` — 직렬화 결과 로그 누적 |
| 탭7 Binary 파일 저장 | `FileExtensions.WriteBytes + WithTimestamp` — 세션 덤프 저장 |
| 탭8 복합 파이프라인 | Guard + DateTimeExt + FileExt 전체 협력 |

---

## lssLib.Extensions와의 관계

`lssLib.Extensions`와 기능이 일부 유사해 보이지만, 두 프로젝트는 **분리 유지**가 올바른 설계입니다.

### 의존성 경계

| | lssLib.Utils | lssLib.Extensions |
|---|---|---|
| 외부 의존성 | BCL only | `System.Text.Json` |
| 합칠 경우 | Utils 사용자도 Json 패키지를 강제 참조하게 됨 | — |

### 도메인 목적

| 클래스 | 위치 | 목적 |
|---|---|---|
| `StringExtensions` | **Utils** | 범용 앱 레이어 문자열 처리 (변환·파싱·인코딩) |
| `TextExtensions` | Extensions | BufSchema 직렬화 특화 (Binary 파이프라인 연계) |
| `ScaleExtensions` | Extensions | 신호처리 전용 (SmoothStep · Hysteresis) |
| `CrcExtensions` | Extensions | 프레임 무결성 검증 전용 (CRC-8/16/32) |
| `DateTimeExtensions` | **Utils** | 범용 날짜 포맷·변환 |
| `FileExtensions` | **Utils** | 범용 파일 I/O 파이프라인 |
| `Guard` | **Utils** | 범용 인수 선행 검증 |

> Binary와 무관한 범용 로직이 Extensions에 있다면 Utils로 이동하는 수준의 정리는 유효하나, 전체 합병은 부적절합니다.

---

## Guard

`CallerArgumentExpression` 기반 인수 선행 검증 헬퍼입니다.  
검증 통과 시 입력값을 그대로 반환하므로 체이닝이 가능하고,  
실패 시 예외 메시지에 **호출부 표현식**이 자동으로 포함됩니다.

```
Guard.NotNull(schema)  실패
  → ArgumentNullException: 'schema' (Parameter 'schema')

Guard.That(frame.Length == schema.ExpectedSize, "프레임 크기 불일치")  실패
  → ArgumentException: 프레임 크기 불일치
```

---

### `NotNull<T>` — Null 검증

**동작**: `null`이면 `ArgumentNullException` throw. 통과 시 non-null 값 반환.

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
var schema  = Guard.NotNull(schema);
int id      = Guard.NotNull(record.Id);     // int? → int (언박싱)

// ── 서비스 생성자 패턴 ─────────────────────────────────────────────
public SensorService(ILogger logger, BufSchema schema)
{
    _logger = Guard.NotNull(logger);
    _schema = Guard.NotNull(schema);
}

// ── lssLib.Serialization 탭8: 복합 파이프라인 진입점 ─────────────
public FramePipeline(StreamParser parser, BufSchema schema, string outputDir)
{
    _parser    = Guard.NotNull(parser);
    _schema    = Guard.NotNull(schema);
    _outputDir = Guard.NotWhiteSpace(outputDir).EnsureDirSelf();
}
```

---

### `NotEmpty` — 빈 문자열 / 빈 배열 검증

**동작**: `null` 또는 빈(`""` / `Length == 0`)이면 `ArgumentException` throw.

```csharp
// ── lssLib.Serialization 탭1: STX 프레임 수신 ────────────────────
// StreamParser에서 수신한 raw 배열을 BufferParser에 전달하기 전 검증
byte[] raw    = await streamParser.ReadNextFrameAsync(ct);
var    parser = new BufferParser(Guard.NotEmpty(raw));
uint   id     = parser.Read<uint>(BufType.UInt32LE);

// ── lssLib.Serialization 탭2: CRC 파이프라인 ─────────────────────
// CRC 계산 대상 배열이 비어있으면 즉시 차단
byte[] payload = Guard.NotEmpty(frame[..^4]);   // CRC 4바이트 제외
uint   crc     = payload.ComputeCrc32();         // CrcExtensions

// ── 문자열 — API 토큰, 경로 등 ────────────────────────────────────
string token = Guard.NotEmpty(apiToken);
string code  = Guard.NotEmpty(deviceCode);
```

> `NotEmpty` vs `NotWhiteSpace`: 공백(`"  "`)을 허용하려면 `NotEmpty`, 거부하려면 `NotWhiteSpace`.

---

### `NotWhiteSpace` — 공백 포함 문자열 검증

**동작**: `null`, `""`, `"  "` 모두 `ArgumentException` throw.

```csharp
// ── lssLib.Serialization WpfDemo 설정 로드 ───────────────────────
public WpfDemoConfig(string portName, string outputDir, string schemaPath)
{
    PortName    = Guard.NotWhiteSpace(portName);
    OutputDir   = Guard.NotWhiteSpace(outputDir).EnsureDirSelf();
    SchemaPath  = Guard.NotWhiteSpace(schemaPath);
    Guard.That(SchemaPath.FileExists(), $"스키마 파일 없음: {SchemaPath}");
}

// ── 체이닝 패턴 ───────────────────────────────────────────────────
string logDir = Guard.NotWhiteSpace(config.LogDir).EnsureDirSelf();
```

---

### `Range<T>` — 범위 검증

**동작**: `value`가 `[min, max]`를 벗어나면 `ArgumentOutOfRangeException` throw. `IComparable<T>` 제약.

```csharp
// ── lssLib.Serialization 탭1: 프레임 오프셋 안전 접근 ───────────
byte[] ReadField(byte[] frame, int offset, int length)
{
    Guard.Range(offset, 0, frame.Length - 1);
    Guard.Range(length, 1, frame.Length - offset);
    return frame[offset..(offset + length)];
}

// ── lssLib.Serialization 탭5: SmoothStep alpha 파라미터 ──────────
// ScaleExtensions.SmoothStep에 전달하는 alpha 범위 검증
float alpha = Guard.Range(configAlpha, 0.0f, 1.0f);
float smoothed = rawSignal.SmoothStep(prevSmoothed, alpha);

// ── 센서 채널, 날짜 범위 ───────────────────────────────────────────
byte ch = Guard.Range(channel, (byte)0, (byte)7);
var dt  = Guard.Range(reportDate, DateTime.Today.AddYears(-1), DateTime.Today);
```

---

### `NotNegative<T>` / `Positive<T>` — 부호 검증

```csharp
// NotNegative: 0 허용, 음수 거부
int bufferSize = Guard.NotNegative(size);     // RingBuffer 크기
int delayMs    = Guard.NotNegative(delay);    // Retry 대기 시간

// Positive: 0도 거부, 양수만 허용
int    capacity     = Guard.Positive(bufCapacity);   // BufferWriter 초기 용량
double samplingRate = Guard.Positive(rate);          // 센서 샘플링 주파수
float  threshold    = Guard.Positive(hysteresisLow); // Hysteresis 임계값

// ── lssLib.Serialization 탭3: RingBuffer 초기화 ──────────────────
var ring = new RingBuffer<byte[]>(Guard.Positive(ringSize));
```

---

### `That` — 조건 검증

**동작**: `condition`이 `false`이면 `ArgumentException` throw.  
`message` 생략 시 조건식 텍스트가 메시지에 자동 포함됩니다.

```csharp
// ── lssLib.Serialization 탭1: STX/ETX 헤더 검증 ─────────────────
void ValidateStxFrame(byte[] frame, BufSchema schema)
{
    Guard.NotEmpty(frame);
    Guard.That(frame[0] == 0x02,  "STX 헤더 없음");
    Guard.That(frame[^1] == 0x03, "ETX 종료자 없음");
    Guard.That(frame.Length == schema.ExpectedSize,
        $"프레임 크기 불일치: 수신={frame.Length}, 예상={schema.ExpectedSize}");
}

// ── lssLib.Serialization 탭2: CRC 일치 검증 ─────────────────────
uint rxCrc   = frame[^4..].ToUInt32LE();             // lssLib.Binary
uint calcCrc = frame[..^4].ComputeCrc32();            // CrcExtensions
Guard.That(rxCrc == calcCrc,
    $"CRC 불일치: 수신={rxCrc:X8}, 계산={calcCrc:X8}");

// ── 상태 전이 전 사전 조건 ─────────────────────────────────────────
void StartAcquisition()
{
    Guard.That(_isConnected,  "장치가 연결되지 않았습니다.");
    Guard.That(!_isAcquiring, "이미 수집 중입니다.");
    Guard.That(_schema != null, "스키마가 설정되지 않았습니다.");
    _isAcquiring = true;
}
```

---

## StringExtensions

> Regex 계열 메서드는 `StringPatterns.[GeneratedRegex]`를 통해  
> 컴파일 타임 생성된 인스턴스를 사용합니다. 런타임 Regex 할당이 없습니다.

---

### `HasValue` / `OrDefault` — 값 존재 판단

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
"hello".HasValue()          // true
"  ".HasValue()             // false
((string?)null).HasValue()  // false

config.Name.OrDefault("unnamed")
env.GetVariable("LOG_DIR").OrDefault(@"C:\logs")

// ── lssLib.Serialization WpfDemo 설정 읽기 ───────────────────────
string portName  = ini["port_name"].OrDefault("COM3");
string outputDir = ini["output_dir"].OrDefault(@"output\frames");
bool   debug     = ini["debug"].ToBoolOrNull() ?? false;

// ── LINQ 필터 조합 ─────────────────────────────────────────────────
var validFields = schema.Fields
    .Where(f => f.Label.HasValue())
    .ToList();
```

---

### 케이스 · 포맷 변환

```csharp
// ── ToSnakeCase / ToCamelCase ──────────────────────────────────────
"SensorReading".ToSnakeCase()      // "sensor_reading"   (DB 컬럼명 변환)
"frame_payload".ToCamelCase()      // "framePayload"     (JSON 키 변환)
"hello world".Capitalize()         // "Hello world"

// ── lssLib.Serialization 탭6: TextExtensions 직렬화 키 변환 ──────
// BufSchema 필드명 → JSON 키 자동 변환
var jsonObj = schema.Fields.ToDictionary(
    f => f.Name.ToCamelCase(),     // "frame_id" → "frameId"
    f => data[f.Name]
);
string json = JsonSerializer.Serialize(jsonObj);  // TextExtensions 연계

// ── Truncate / Repeat / PadLeftTo ─────────────────────────────────
errorMsg.Truncate(80)                    // UI 에러 메시지 80자 제한
"=".Repeat(60)                           // 로그 구분선
frameId.ToString().PadLeftTo(8, '0')    // "00001024" 고정폭 프레임 ID
```

---

### 검색 · 비교 (대소문자 무시)

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
"Connection: Keep-Alive".ContainsIgnoreCase("keep-alive")  // true
"frame.BIN".EndsWithIgnoreCase(".bin")                      // true
"OK".EqualsIgnoreCase("ok")                                  // true
status.IsAnyOf("ok", "success", "done")

// ── lssLib.Serialization WpfDemo 파일 필터 ───────────────────────
// 출력 디렉터리에서 바이너리 덤프만 필터
var dumpFiles = outputDir.EnumerateFiles()
    .Where(f => f.GetExt().IsAnyOf(".bin", ".dat", ".dump"))
    .ToList();

// ── 포트 이름 검증 ─────────────────────────────────────────────────
if (!portName.StartsWithIgnoreCase("COM") && !portName.StartsWithIgnoreCase("/dev/"))
    throw new ArgumentException($"유효하지 않은 포트: {portName}");
```

---

### 안전 파싱

모두 실패 시 `null` 반환. 예외 throw 없음.

```csharp
// ── lssLib.Serialization 탭5: SmoothStep 설정값 파싱 ─────────────
// INI 파일에서 Hysteresis / SmoothStep 파라미터 안전 파싱
float alpha   = ini["smooth_alpha"].ToDoubleOrNull() is double a
    ? (float)Guard.Range(a, 0.0, 1.0) : 0.15f;
float hystLow = ini["hyst_low"].ToDoubleOrNull() is double l
    ? (float)Guard.Positive(l) : 0.05f;
float hystHigh = ini["hyst_high"].ToDoubleOrNull() is double h
    ? (float)Guard.Range(h, hystLow, 1.0) : 0.95f;

// ── lssLib.Serialization WpfDemo 설정 전체 파싱 ─────────────────
int    port       = ini["port"].ToIntOrNull()         ?? 8080;
long   maxBytes   = ini["max_size"].ToLongOrNull()    ?? 65536L;
bool   debugMode  = ini["debug"].ToBoolOrNull()       ?? false;
double timeout    = ini["timeout"].ToDoubleOrNull()   ?? 5.0;
decimal precision = ini["precision"].ToDecimalOrNull() ?? 0.001m;
// ↑ lssLib.Binary decimal 16바이트 직렬화와 정합성 유지

// ── bool 유연 변환 — UI 체크박스 설정 읽기 ────────────────────────
// INI: "true" / "1" / "yes" / "on" 모두 true로 인식
bool showGrid  = settings["show_grid"].ToBoolOrNull()  ?? true;
bool autoSave  = settings["auto_save"].ToBoolOrNull()  ?? false;
```

---

### 인코딩 · 바이트 변환

```csharp
// ── lssLib.Binary 파이프라인 연계 ────────────────────────────────
// BufferWriter 출력 → HEX 덤프 로그 (lssLib.Binary 덤프 포맷과 동일)
var writer = new BufferWriter();
writer.Write(BufType.UInt32LE, sensorId);
writer.Write(BufType.FloatLE, temperature);
byte[] raw = writer.ToArray();

logger.Debug($"[{DateTime.Now.ToIsoDateTime()}] " +
             $"Frame({raw.Length}B): {raw.ToHex(spaced: true)}");
// → "[2024-04-01 14:30:00] Frame(8B): 01 00 00 00 CD CC 4C 42"

// ── HEX 역변환 — 시리얼 프로토콜 수신 ────────────────────────────
string hexLine = serialPort.ReadLine().Trim();
byte[] data    = hexLine.FromHex();              // "AA BB CC" → byte[]
var    parser  = new BufferParser(Guard.NotEmpty(data));

// ── Base64 — API 토큰 / 인증 헤더 ───────────────────────────────
string authHeader = $"Basic {$"{user}:{pass}".ToBase64()}";

// ── UTF-8 변환 ────────────────────────────────────────────────────
byte[] labelBytes = schema.Label.ToUtf8Bytes();    // TextExtensions 연계
```

---

### 정규식 유틸

```csharp
// ── IsDigitsOnly / IsEmail (GeneratedRegex — 런타임 할당 없음) ───
"12345".IsDigitsOnly()           // true
"admin@example.com".IsEmail()    // true

// ── lssLib.Serialization WpfDemo: 장치 응답 파싱 ─────────────────
// 시리얼 응답: "STATUS=READY;TEMP=42.5;CH=3;CRC=AABB1234"
string response = device.ReadLine();

string? status  = response.MatchGroup(@"STATUS=(\w+)");
string? tempStr = response.MatchGroup(@"TEMP=([\d.]+)");
string? crcStr  = response.MatchGroup(@"CRC=([0-9A-Fa-f]{8})");

float   temp    = tempStr?.ToDoubleOrNull() is double d ? (float)d : 0f;
uint?   rxCrc   = crcStr?.FromHex() is byte[] b ? BitConverter.ToUInt32(b) : null;

// ── 멀티라인 INI 설정 파싱 ────────────────────────────────────────
string raw = @"config\demo.ini".ReadText();
var settings = raw.ToNonEmptyLines()
    .Where(l => !l.StartsWithIgnoreCase("#") && l.ContainsIgnoreCase("="))
    .ToDictionary(
        l => l.MatchGroup(@"^(\w+)\s*=")!.ToSnakeCase(),
        l => l.MatchGroup(@"=\s*(.+)$")!.Trim()
    );
```

---

## DateTimeExtensions

---

### 표준 포맷 문자열

모두 `CultureInfo.InvariantCulture` 고정 — 지역 설정 무관하게 동일한 출력을 보장합니다.

| 메서드 | 출력 예 | lssLib.Serialization 활용 |
|---|---|---|
| `ToIsoDate()` | `"2024-04-01"` | 날짜별 로그 디렉터리명 |
| `ToTimeString()` | `"14:30:00"` | 프레임 수신 시각 표시 |
| `ToIsoDateTime()` | `"2024-04-01 14:30:00"` | 로그 파일 타임스탬프 |
| `ToIso8601Utc()` | `"2024-04-01T14:30:00.000Z"` | REST API 전송 타임스탬프 |
| `ToFileStamp()` | `"20240401_143000"` | 세션 덤프 파일명 |
| `ToMsStamp()` | `"20240401143000123"` | 프레임 단위 고유 ID |

```csharp
// ── lssLib.Serialization 탭7: 세션 덤프 저장 ─────────────────────
// BufferWriter로 직렬화된 바이너리를 타임스탬프 파일명으로 저장
var writer = new BufferWriter();
foreach (var field in schema.Fields)
    writer.Write(field.Type, sessionData[field.Name]);

string dumpPath = $@"dumps\session_{DateTime.Now.ToFileStamp()}.bin";
dumpPath.EnsureDir().WriteBytes(writer.ToArray());
// → "dumps\session_20240401_143000.bin"

// ── lssLib.Serialization 탭3: RingBuffer 프레임 타임스탬프 ───────
// 수신 프레임마다 정밀 타임스탬프 기록
void OnFrameReceived(byte[] frame)
{
    string ts = DateTime.Now.ToMsStamp();   // "20240401143000123" — 프레임 ID 역할
    ring.Enqueue(frame);
    logger.Debug($"[{DateTime.Now.ToIsoDateTime()}] Frame #{ts} enqueued");
}

// ── 날짜별 로그 디렉터리 자동 분류 ──────────────────────────────
string logDir = Path.Combine("logs", DateTime.Today.ToIsoDate());
logDir.EnsureDirSelf();
// → "logs/2024-04-01/"
```

---

### Unix Epoch 변환

```csharp
// ── lssLib.Serialization 탭1: STX 프레임 내 Unix 타임스탬프 ──────
// 프레임 헤더에 Unix 밀리초 타임스탬프가 포함된 경우
var parser = new BufferParser(Guard.NotEmpty(frame));
uint  id      = parser.Read<uint>(BufType.UInt32LE);
long  unixMs  = parser.Read<long>(BufType.Int64LE);
DateTime time = unixMs.FromUnixMilliseconds();

logger.Info($"[{time.ToIsoDateTime()}] 센서 ID={id} 수신");

// ── API 전송 타임스탬프 ─────────────────────────────────────────
var payload = new
{
    device_id  = deviceId,
    timestamp  = DateTime.UtcNow.ToUnixMilliseconds(),
    created_at = DateTime.UtcNow.ToIso8601Utc()
};

// ── 범위 조회 → Unix 변환 ─────────────────────────────────────────
long fromTs = DateTime.Today.StartOfMonth().ToUnixSeconds();
long toTs   = DateTime.Today.EndOfMonth().ToUnixSeconds();
var records = await api.GetRecordsAsync(fromTs, toTs);
```

---

### 날짜 경계

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
dt.StartOfDay()    // 2024-04-01 00:00:00.0000000
dt.EndOfDay()      // 2024-04-01 23:59:59.9999999
dt.StartOfWeek()   // 해당 주 월요일 (ISO 8601)
dt.StartOfMonth()  // 2024-04-01 00:00:00
dt.EndOfMonth()    // 2024-04-30 23:59:59.9999999

// ── lssLib.Serialization WpfDemo: 일별 집계 뷰 ───────────────────
// 탭 데이터 뷰에서 오늘 수신된 프레임만 필터
var todayFrames = allFrames
    .Where(f => f.ReceivedAt.IsBetween(
        DateTime.Today.StartOfDay(),
        DateTime.Today.EndOfDay()))
    .ToList();

// ── 주말 제외 스케줄링 ─────────────────────────────────────────────
DateTime next = DateTime.Today;
while (next.IsWeekend()) next = next.AddDays(1);
scheduler.ScheduleSync(next.StartOfDay());

// ── 업무 시간 판단 ────────────────────────────────────────────────
bool isBusinessHour = DateTime.Now.IsBetween(
    DateTime.Today.AddHours(9),
    DateTime.Today.AddHours(18));
if (isBusinessHour) SendAlert(alert);
```

---

### 상대 시간 (한국어)

```csharp
// ── 임계값 단계 ────────────────────────────────────────────────────
// < 60 s    → "N초 전"     / < 1 h  → "N분 전"
// < 1 day   → "N시간 전"   / < 7 d  → "N일 전"
// < 30 d    → "N주 전"     / 이상   → "yyyy-MM-dd"

// ── lssLib.Serialization WpfDemo: 알림 및 이벤트 리스트 ─────────
// 프레임 이벤트 목록에 상대 시간 표시
foreach (var ev in eventLog.OrderByDescending(e => e.OccurredAt))
    listBox.Items.Add($"[{ev.OccurredAt.ToRelativeKo()}] {ev.Message}");
// → "[3분 전] CRC 불일치 감지"
// → "[2일 전] 센서 재연결 완료"

// ── 장치 마지막 수신 표시 ────────────────────────────────────────
sensorLabel.Content = $"마지막 수신: {device.LastFrameAt.ToRelativeKo()}";
```

---

### TimeSpan 유틸

```csharp
// ── lssLib.Serialization 탭8: 파이프라인 처리 시간 측정 ──────────
var sw = Stopwatch.StartNew();
var result = await pipeline.RunAsync(frame, ct);
sw.Stop();

// 로그: "처리 완료: 00:00:00.123  (123 ms)"
logger.Info($"처리 완료: {sw.Elapsed.ToDisplay()}  ({sw.Elapsed.ToMs()} ms)");

// WPF ViewModel 바인딩
ViewModel.ProcessingTime = $"{sw.Elapsed.ToDisplay()}";
ViewModel.ProcessingMs   = sw.Elapsed.ToMs();

// ── 타임아웃 잔여 시간 ─────────────────────────────────────────────
TimeSpan remaining = deadline - DateTime.Now;
if (remaining.ToMs() < 200)
    logger.Warn($"타임아웃 임박: {remaining.ToDisplay()} 남음");
```

---

### 안전 파싱

```csharp
// ── lssLib.Serialization WpfDemo: 날짜 필터 입력 처리 ───────────
// 사용자가 다양한 형식으로 날짜를 입력할 수 있음
DateTime from = dateFromBox.Text.TryParseAny(
    "yyyy-MM-dd", "yyyyMMdd", "yyyy/MM/dd", "MM/dd/yyyy")
    ?? DateTime.Today.StartOfMonth();

DateTime to = dateToBox.Text.TryParseAny(
    "yyyy-MM-dd", "yyyyMMdd", "yyyy/MM/dd", "MM/dd/yyyy")
    ?? DateTime.Today.EndOfMonth();

// ── 프레임 내 날짜 문자열 파싱 ───────────────────────────────────
// TextExtensions 직렬화 결과에 포함된 날짜 필드 파싱
string? rawDate = parser.ReadString(schema["date_field"]);
DateTime? parsed = rawDate.TryParseDateTime("yyyy-MM-dd HH:mm:ss");
```

---

## FileExtensions

모든 쓰기 메서드는 상위 디렉터리를 자동 생성하고 경로를 반환하여 체이닝이 가능합니다.

---

### 경로 조작

```csharp
// ── lssLib.Serialization WpfDemo: 출력 파일 경로 처리 ───────────
@"output\frames\capture.bin".GetExt()            // ".bin"
@"output\frames\capture.bin".GetFileNameNoExt()  // "capture"
@"output\frames\capture.bin".GetDir()            // @"output\frames"

// ── WithTimestamp: 세션별 파일명 ─────────────────────────────────
// lssLib.Serialization 탭7: Binary 파일 저장
string dumpPath = @"dumps\session.bin".WithTimestamp();
// → "dumps\session_20240401_143000.bin"

string logPath = @"logs\app.log".WithTimestamp(format: "yyyyMMdd");
// → "logs\app_20240401.log"

// ── ToUniquePath: 동일 세션 중복 저장 방지 ───────────────────────
string savePath = @"captures\frame.bin".WithTimestamp().ToUniquePath();
// → "captures\frame_20240401_143000_1.bin" (같은 초 내 두 번째 저장)
```

---

### 디렉터리 자동 보장

```csharp
// ── lssLib.Serialization WpfDemo: 출력 구조 초기화 ──────────────
void InitOutputStructure(string baseDir)
{
    Path.Combine(baseDir, "frames").EnsureDirSelf();
    Path.Combine(baseDir, "logs").EnsureDirSelf();
    Path.Combine(baseDir, "reports").EnsureDirSelf();
    Path.Combine(baseDir, "diff").EnsureDirSelf();   // 탭4 BufferDiff 결과
}

// ── 체이닝 패턴 ───────────────────────────────────────────────────
@"output\2024-04\session.log".EnsureDir().AppendLine(logEntry);
// → "output/2024-04/" 폴더 자동 생성 후 로그 추가
```

---

### 파일 읽기

```csharp
// ── lssLib.Serialization 탭1: STX 프레임 파일 재생 ──────────────
// 저장된 바이너리 덤프를 읽어 BufferParser에 전달
byte[] dump   = @"dumps\session_20240401_143000.bin".ReadBytes();
var    parser = new BufferParser(Guard.NotEmpty(dump));

// 비동기 읽기
byte[] raw = await @"captures\frame.bin".ReadBytesAsync(ct);

// ── 대용량 로그 지연 읽기 ─────────────────────────────────────────
// 탭6: TextExtensions 직렬화 결과 로그 분석
var crcErrors = @"logs\2024-04-01.log".ReadLines()
    .Where(l => l.ContainsIgnoreCase("CRC") && l.ContainsIgnoreCase("불일치"))
    .ToList();

// ── 설정 파일 읽기 ────────────────────────────────────────────────
string json = @"config\schema.json".ReadText();
var schema  = BufSchema.FromJson(json);    // lssLib.Extensions.TextExtensions
```

---

### 파일 쓰기

```csharp
// ── lssLib.Serialization 탭7: Binary 직렬화 결과 저장 ───────────
var writer = new BufferWriter();
foreach (var field in schema.Fields)
    writer.Write(field.Type, sessionData[field.Name]);

string path = $@"output\{DateTime.Now.ToFileStamp()}.bin"
    .EnsureDir()
    .WriteBytes(writer.ToArray());   // 경로 반환 → 체이닝 가능
logger.Info($"저장 완료: {path.GetFileName()} ({path.GetSizeDisplay()})");

// ── 탭4: BufferDiff 비교 결과 저장 ──────────────────────────────
var diff = BufferDiff.Compare(snapshotA, snapshotB, schema);
string diffPath = @"diff\result.json"
    .WithTimestamp()
    .WriteText(diff.ToJson());      // lssLib.Extensions.TextExtensions

// ── 탭6: 직렬화 결과 로그 추적 ──────────────────────────────────
string logFile = $@"logs\{DateTime.Today.ToIsoDate()}.log";
logFile.AppendLine(
    $"[{DateTime.Now.ToIsoDateTime()}] " +
    $"Schema={schema.Name} Fields={schema.Fields.Count} " +
    $"CRC={payload.ComputeCrc32():X8}");   // CrcExtensions

// ── 쓰기 후 즉시 백업 체이닝 ─────────────────────────────────────
@"config\app.json"
    .WriteText(newJson)
    .CopyTo(@"config\backup\app.json");
```

---

### 파일 메타데이터

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
path.GetSize()           // 바이트 수 (-1: 파일 없음)
path.GetSizeDisplay()    // "1.4 MB"  (B/KB/MB/GB 자동)
path.GetLastModified()   // DateTime?

// ── lssLib.Serialization WpfDemo: 덤프 파일 관리 탭 ─────────────
var dumpInfo = @"output".EnumerateByDate("*.bin")
    .Select(f => new
    {
        Name     = f.GetFileName(),
        Size     = f.GetSizeDisplay(),
        Modified = f.GetLastModified()?.ToRelativeKo() ?? "알 수 없음"
    })
    .ToList();
// WPF DataGrid 바인딩용 데이터

// ── 설정 파일 변경 감지 루프 ─────────────────────────────────────
DateTime lastChecked = DateTime.MinValue;
while (true)
{
    var modified = configPath.GetLastModified();
    if (modified > lastChecked)
    {
        ReloadSchema(configPath.ReadText());
        lastChecked = modified.Value;
        logger.Info($"설정 리로드: {configPath.GetFileName()}");
    }
    await Task.Delay(5_000, ct);
}
```

---

### 디렉터리 열거

```csharp
// ── lssLib.Serialization WpfDemo: 세션 파일 목록 탭 ─────────────
// 출력 디렉터리의 바이너리 덤프를 최신순으로 나열
var sessions = @"output"
    .EnumerateByDate("*.bin")
    .Select(f => new SessionInfo
    {
        Path      = f,
        Name      = f.GetFileName(),
        Size      = f.GetSizeDisplay(),
        CreatedAt = f.GetLastModified()?.ToIsoDateTime() ?? "-"
    })
    .ToList();

// ── 오래된 파일 자동 정리 ─────────────────────────────────────────
void CleanupOldDumps(string dir, int keepDays = 7)
{
    var cutoff = DateTime.Now.AddDays(-keepDays);
    var old = dir.EnumerateByDate("*.bin")
                 .Where(f => f.GetLastModified() < cutoff)
                 .ToList();

    foreach (var f in old)
        if (f.TryDelete())
            logger.Info($"삭제: {f.GetFileName()} ({f.GetSizeDisplay()})");

    logger.Info($"정리 완료: {old.Count}개 파일");
}

// ── 최신 N개만 유지 (롤링) ───────────────────────────────────────
void KeepLatestDumps(string dir, int keep = 20)
{
    dir.EnumerateByDate("*.bin")
       .Skip(keep)
       .ToList()
       .ForEach(f => f.TryDelete());
}
```

---

### 안전 삭제 · 복사 · 이동

```csharp
// ── lssLib.Serialization WpfDemo: 세션 아카이브 ─────────────────
// 처리 완료된 덤프 파일을 날짜별 아카이브 폴더로 이동
void ArchiveSession(string dumpPath)
{
    string date    = File.GetLastWriteTime(dumpPath).ToIsoDate();
    string archive = Path.Combine("archive", date, dumpPath.GetFileName());
    dumpPath.MoveTo(archive.ToUniquePath());
    logger.Info($"아카이브 완료: {archive.GetFileName()}");
}

// ── 처리 후 임시 파일 정리 ───────────────────────────────────────
async Task ProcessAndCleanAsync(string tempPath, CancellationToken ct)
{
    try
    {
        byte[]    data   = await tempPath.ReadBytesAsync(ct);
        BufResult result = new BufferParser(Guard.NotEmpty(data)).ParseAll(schema);
        await SaveResultAsync(result, ct);
    }
    finally
    {
        tempPath.TryDelete();   // 성공·실패 무관하게 임시 파일 정리
    }
}

// ── 설정 백업 후 업데이트 ─────────────────────────────────────────
string cfgPath = @"config\schema.json";
cfgPath.CopyTo(@"config\backup\schema.json")  // 백업
       .GetFileName();                          // "schema.json" (체이닝 예시)
cfgPath.WriteText(newSchemaJson);              // 원본 업데이트
```

---

## 실전 운용 시나리오

### 시나리오 1 — lssLib.Serialization 탭8: 복합 파이프라인 전체 흐름

Guard · DateTimeExtensions · FileExtensions · StringExtensions 전 모듈이 협력합니다.

```csharp
public class CompositePipeline
{
    private readonly StreamParser _parser;
    private readonly BufSchema    _schema;
    private readonly string       _outputDir;
    private          float        _lastTemp;

    public CompositePipeline(StreamParser parser, BufSchema schema, string outputDir)
    {
        _parser    = Guard.NotNull(parser);
        _schema    = Guard.NotNull(schema);
        _outputDir = Guard.NotWhiteSpace(outputDir).EnsureDirSelf();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // 날짜별 로그 디렉터리 준비
        string logDir = Path.Combine(_outputDir, "logs",
            DateTime.Today.ToIsoDate());
        logDir.EnsureDirSelf();

        await foreach (var raw in _parser.ReadFramesAsync(ct))
        {
            // §1  프레임 검증 (Guard)
            Guard.NotEmpty(raw);
            Guard.That(raw[0] == 0x02, "STX 헤더 없음");

            // §2  CRC 검증 (CrcExtensions + Guard)
            uint rxCrc   = raw[^4..].ToUInt32LE();
            uint calcCrc = raw[..^4].ComputeCrc32();
            Guard.That(rxCrc == calcCrc,
                $"CRC 불일치: {rxCrc:X8} ≠ {calcCrc:X8}");

            // §3  Binary 파싱 (lssLib.Binary)
            var   parser = new BufferParser(raw[1..^5]);  // STX/ETX/CRC 제외
            uint  id     = parser.Read<uint>(BufType.UInt32LE);
            float temp   = parser.Read<float>(BufType.FloatLE);
            float hum    = parser.Read<float>(BufType.FloatLE);

            // §4  신호 보간 (ScaleExtensions)
            float smoothTemp = temp.SmoothStep(_lastTemp, alpha: 0.2f);
            float filtHum    = hum.Hysteresis(_lastTemp, low: 0.05f, high: 0.95f);
            _lastTemp = smoothTemp;

            // §5  로그 기록 (DateTimeExtensions + FileExtensions)
            string logLine =
                $"[{DateTime.Now.ToIsoDateTime()}] " +
                $"ID={id.ToString().PadLeftTo(6, '0')} " +  // StringExtensions
                $"Temp={smoothTemp:F2} Hum={filtHum:F2} " +
                $"CRC={calcCrc:X8}";

            Path.Combine(logDir, "sensor.log").AppendLine(logLine);

            // §6  HEX 덤프 (StringExtensions.ToHex)
            logger.Debug($"Raw: {raw.ToHex(spaced: true)}");

            // §7  주기적 바이너리 스냅샷 저장 (FileExtensions)
            if (id % 100 == 0)
            {
                string snapPath = Path.Combine(_outputDir, "snapshots",
                    $"snap_{DateTime.Now.ToMsStamp()}.bin");
                snapPath.WriteBytes(raw);
            }
        }

        // §8  오래된 스냅샷 정리
        Path.Combine(_outputDir, "snapshots")
            .EnumerateByDate("*.bin")
            .Where(f => f.GetLastModified() < DateTime.Now.AddDays(-3))
            .ToList()
            .ForEach(f => f.TryDelete());
    }
}
```

---

### 시나리오 2 — lssLib.Serialization 탭6: TextExtensions 직렬화 + API 전송

```csharp
public class SerializationSyncService
{
    public async Task SyncMonthAsync(BufSchema schema, string apiUrl, CancellationToken ct)
    {
        // 월 범위 계산 (DateTimeExtensions)
        DateTime from = DateTime.Today.StartOfMonth();
        DateTime to   = DateTime.Today.EndOfMonth();

        // 로컬 덤프 파일에서 해당 월 데이터 수집
        var monthFiles = @"output\frames"
            .EnumerateByDate("*.bin")
            .Where(f => f.GetLastModified().IsBetween(from, to))  // DateTimeExtensions
            .ToList();

        logger.Info($"대상 파일: {monthFiles.Count}개 " +
                    $"({from.ToIsoDate()} ~ {to.ToIsoDate()})");

        // 파일별 파싱 → JSON 직렬화 → API 전송
        foreach (var filePath in monthFiles)
        {
            byte[]    raw    = filePath.ReadBytes();
            BufResult result = new BufferParser(Guard.NotEmpty(raw)).ParseAll(schema);

            // TextExtensions: BufSchema → JSON
            string json = schema.SerializeToJson(result);
            string etag = json.ToUtf8Bytes().ComputeCrc32().ToString("X8");  // CrcExtensions

            logger.Debug($"[{filePath.GetFileName()}] " +
                         $"Size={filePath.GetSizeDisplay()} " +
                         $"ETag={etag}");

            // lssLib.Retry.RateLimiterExtensions 연계 (별도 패키지)
            // await postFunc.ExecuteAsync(_apiLimiter, ct);
        }

        // 30일 이상 지난 파일 정리 (FileExtensions)
        @"output\frames"
            .EnumerateByDate("*.bin")
            .Where(f => f.GetLastModified() < DateTime.Now.AddDays(-30))
            .ToList()
            .ForEach(f =>
            {
                string archive = Path.Combine("archive",
                    f.GetLastModified()?.ToIsoDate() ?? "unknown",
                    f.GetFileName());
                f.MoveTo(archive.ToUniquePath());
            });
    }
}
```

---

### 시나리오 3 — WpfDemo 설정 파일 로드 + 전체 검증

```csharp
public class WpfDemoConfig
{
    public string  PortName      { get; }
    public int     BaudRate      { get; }
    public string  OutputDir     { get; }
    public string  SchemaPath    { get; }
    public float   SmoothAlpha   { get; }
    public float   HystLow       { get; }
    public float   HystHigh      { get; }
    public bool    AutoSave      { get; }
    public int     MaxFrames     { get; }

    public static WpfDemoConfig Load(string iniPath)
    {
        // 경로 검증 (Guard + FileExtensions)
        Guard.NotWhiteSpace(iniPath);
        Guard.That(iniPath.FileExists(), $"설정 파일 없음: {iniPath}");

        // INI 파싱 (StringExtensions)
        var ini = iniPath.ReadText()
            .ToNonEmptyLines()
            .Where(l => !l.StartsWithIgnoreCase("#") && l.IsMatch(@"^\w+="))
            .ToDictionary(
                l => l.MatchGroup(@"^(\w+)\s*=")!,
                l => l.MatchGroup(@"=\s*(.+)$")!.Trim()
            );

        float alpha = (float)(ini.GetValueOrDefault("smooth_alpha").ToDoubleOrNull() ?? 0.15);
        float low   = (float)(ini.GetValueOrDefault("hyst_low").ToDoubleOrNull()    ?? 0.05);
        float high  = (float)(ini.GetValueOrDefault("hyst_high").ToDoubleOrNull()   ?? 0.95);

        return new WpfDemoConfig
        {
            // 필수 문자열 값 (Guard.NotWhiteSpace)
            PortName    = Guard.NotWhiteSpace(ini.GetValueOrDefault("port_name").OrDefault()),
            SchemaPath  = Guard.NotWhiteSpace(ini.GetValueOrDefault("schema_path").OrDefault()),
            OutputDir   = Guard.NotWhiteSpace(ini.GetValueOrDefault("output_dir")
                              .OrDefault(@"output")).EnsureDirSelf(),

            // 수치 파싱 + 범위 검증 (StringExtensions + Guard)
            BaudRate    = Guard.Range(ini.GetValueOrDefault("baud_rate").ToIntOrNull() ?? 115200,
                              1200, 921600),
            SmoothAlpha = Guard.Range(alpha, 0.0f, 1.0f),
            HystLow     = Guard.Positive(low),
            HystHigh    = Guard.Range(high, low, 1.0f),
            MaxFrames   = Guard.Positive(ini.GetValueOrDefault("max_frames").ToIntOrNull() ?? 1000),

            // bool 유연 변환 (StringExtensions)
            AutoSave    = ini.GetValueOrDefault("auto_save").ToBoolOrNull() ?? true,
        };
    }
}
```

---

## 설계 원칙

| 원칙 | 내용 |
|---|---|
| **Extension-method only** | 인터페이스·추상 클래스 없음. 조합으로만 기능 확장 |
| **No external dependencies** | BCL만 참조. `lssLib.Extensions`의 `System.Text.Json` 의존성과 명확히 분리 |
| **`[GeneratedRegex]`** | `StringPatterns`에 집약. 컴파일 타임 생성, 런타임 Regex 할당 없음 |
| **값 반환 체이닝** | 쓰기·검증·경로 메서드는 입력값을 반환 → 파이프라인 구성 가능 |
| **Guard 선행 검증** | 진입점에서 모든 인수를 즉시 검증. 이후 로직에서 null 체크 불필요 |
| **lssLib.Retry 분리** | Retry · UtilResult 계열은 독립 패키지로 분리. Utils는 범용 헬퍼에 집중 |
