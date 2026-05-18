# lssLib.Retry

> lssLib 생태계 견고성(Resilience) 모듈 — `v1.0.0`

---

## 개요

| 항목 | 값 |
|---|---|
| 네임스페이스 | `lssLib.Retry` |
| 타겟 프레임워크 | `net8.0` |
| 외부 의존성 | **없음** (BCL only) |
| 설계 원칙 | Extension-method only · No abstractions · Immutable policy |

lssLib 생태계의 견고성 패턴 전용 라이브러리입니다.  
`lssLib.Binary` · `lssLib.Extensions` · `lssLib.Utils`와 결합하여  
프레임 파싱, 직렬화, 센서 스트림 처리 등 실시간 파이프라인에 안정성을 부여합니다.

---

## 파일 구성

```
lssLib.Retry/
├── lssLib.Retry.csproj
│
├── UtilResult.cs               — 안전 실행 반환 타입 (UtilResult / UtilResult<T> / UtilResults)
│
├── RetryPolicy.cs              — Retry 설정 값 객체 (readonly record struct)
├── RetryExtensions.cs          — Retry · Timeout · 안전 실행 확장 메서드
│
├── CircuitBreakerPolicy.cs     — Circuit Breaker 설정 값 객체
├── CircuitBreakerState.cs      — Circuit Breaker 런타임 상태 (스레드 안전)
├── CircuitBreakerExtensions.cs — Circuit Breaker 실행 확장 메서드
│
├── RateLimiterPolicy.cs        — Rate Limiter 설정 값 객체 (슬라이딩 윈도우)
├── RateLimiterState.cs         — Rate Limiter 런타임 상태 (스레드 안전)
└── RateLimiterExtensions.cs    — Rate Limiter 실행 확장 메서드 (CB 조합 포함)
```

---

## lssLib 생태계 내 위치

```
lssLib.Binary          ──► lssLib.Extensions   (CrcExtensions · ScaleExtensions · TextExtensions)
       │                          │
       └──────────────────────────┤
                                  ▼
                          lssLib.Utils          (Guard · StringExt · DateTimeExt · FileExt)
                                  │
                                  ▼
                          lssLib.Retry          (Retry · CircuitBreaker · RateLimiter)
                                  │
                                  ▼
                   lssLib.Serialization.WpfDemo (8 복합 시나리오 검증)
```

`lssLib.Retry`는 다른 lssLib 패키지를 참조하지 않습니다.  
어느 레이어에서든 독립적으로 참조 가능하며, 각 lssLib 모듈과 조합해 사용합니다.

---

## lssLib.Serialization.WpfDemo 연동 맥락

`lssLib.Serialization.WpfDemo`는 lssLib Binary/Extensions 생태계를 검증하는  
7탭 WPF 애플리케이션으로, 8개 복합 시나리오를 실행합니다.  
각 시나리오에서 `lssLib.Retry`가 담당하는 역할은 다음과 같습니다.

| 탭 / 시나리오 | lssLib.Retry 역할 |
|---|---|
| STX 프레임 수신 | `RetryAsync` — 포트 오픈·읽기 재시도 |
| CRC 검증 파이프라인 | `CircuitBreaker` — 연속 CRC 실패 시 수신 차단 |
| RingBuffer 스트리밍 | `RateLimiter` — 초당 프레임 처리량 제한 |
| BufferDiff 비교 | `RetryWithTimeout` — 대용량 diff 타임아웃 보호 |
| SmoothStep 신호 처리 | `CircuitBreaker + Retry` — 센서 연결 장애 복구 |
| TextExtensions 직렬화 | `RetryOnAsync` — HttpRequestException 전용 재시도 |
| Binary 파일 저장 | `TryExecuteAsync` — 저장 실패 시 UtilResult 반환 |
| 복합 파이프라인 | CB + RL + Retry 3중 조합 |

---

## UtilResult / UtilResult\<T\>

`tuple (Value, Error)` 대신 성공/실패를 명시적으로 구분하는 값 타입입니다.  
`TryExecuteAsync` 계열 메서드의 반환 타입으로, 예외 없는 안전한 실행 결과를 표현합니다.

### 멤버 레퍼런스

| 멤버 | 타입 | 설명 |
|---|---|---|
| `IsOk` | `bool` | 성공 여부 |
| `IsError` | `bool` | 실패 여부 (`!IsOk`) |
| `Value` | `T?` | 성공 시 결과값. 실패 시 `default` |
| `Error` | `Exception?` | 실패 시 원본 예외. 성공 시 `null` |
| `Unwrap()` | `T` | 성공 시 값. 실패 시 `InvalidOperationException` throw |
| `UnwrapOr(fallback)` | `T` | 실패 시 fallback 반환. 예외 없음 |
| `UnwrapOrElse(factory)` | `T` | 실패 시 `factory(Error)` 결과 반환 |
| `Map<TOut>(mapper)` | `UtilResult<TOut>` | 성공 값 변환. 실패는 그대로 전파 |
| `ThrowIfError()` | `void` | 실패 시 원본 예외 re-throw |

팩토리: `UtilResults.Ok()` · `UtilResults.Ok<T>(value)` · `UtilResults.Fail(ex)` · `UtilResults.Fail<T>(ex)`

### 사용 예제

```csharp
// ── 기본 패턴 ──────────────────────────────────────────────────────
UtilResult<byte[]> r = await (() => ReadFrameAsync()).TryExecuteAsync();

// 성공/실패 분기
if (r.IsOk)
    ProcessFrame(r.Unwrap());
else
    logger.Error($"프레임 읽기 실패: {r.Error!.Message}");

// 실패 시 기본값
byte[] frame = r.UnwrapOr(Array.Empty<byte>());

// 실패 시 동적 대체값
byte[] frame2 = r.UnwrapOrElse(ex =>
{
    logger.Warn($"폴백 사용: {ex!.Message}");
    return _lastValidFrame;
});

// Map — 타입 변환 (실패는 그대로 전파)
UtilResult<BufSchema> schema = r.Map(bytes => BufSchema.Parse(bytes));

// ── lssLib.Serialization 연동 패턴 ────────────────────────────────
// 탭 7: Binary 파일 저장
async Task<UtilResult> SaveFrameAsync(byte[] frame, string path)
{
    return await (() => Task.Run(() =>
    {
        // lssLib.Extensions.CrcExtensions로 CRC 검증 후 저장
        uint crc = frame.ComputeCrc32();
        path.EnsureDir().WriteBytes(frame);  // lssLib.Utils.FileExtensions
    })).TryExecuteAsync();
}

var saveResult = await SaveFrameAsync(frame, outputPath);
saveResult.ThrowIfError();   // 실패 시 호출자에게 전파
```

---

## RetryPolicy

파라미터 sprawl 없이 재시도 설정을 불변 값 객체로 캡슐화합니다.

### 프리셋

| 프리셋 | 횟수 | 대기 | 백오프 | 적합한 상황 |
|---|---|---|---|---|
| `Default` | 3 | 200 ms | 없음 | 일반 일시 오류 |
| `Http` | 3 | 500 ms | 지수 | HTTP API · 외부 서비스 |
| `Database` | 5 | 1 s | 지수 | DB 연결 · 쿼리 타임아웃 |
| `Immediate` | 3 | 0 ms | 없음 | 경쟁 조건 즉시 재시도 |

> 지수 백오프: `Delay × 2^attempt` — Http 기준: 500 ms → 1 s → 2 s

### 커스텀 정책

```csharp
// ── 시리얼 포트 재시도 정책 ────────────────────────────────────────
static readonly RetryPolicy SerialPolicy = new(
    MaxAttempts: 10,
    Delay:       TimeSpan.FromMilliseconds(100),
    Backoff:     false,    // 장치 응답 주기에 맞춘 고정 간격
    OnRetry:     (ex, n) => logger.Warn($"[시리얼 재시도 {n}/10] {ex.Message}")
);

// ── with 식으로 파생 정책 ─────────────────────────────────────────
var verbosePolicy = RetryPolicy.Http with
{
    OnRetry = (ex, n) => diagnostics.RecordRetry(n, ex)
};

// ── lssLib.Serialization STX 프레임 수신 정책 ────────────────────
// 탭 1: StreamParser STX 기반 프레임 감지
static readonly RetryPolicy StxFramePolicy = new(
    MaxAttempts: 5,
    Delay:       TimeSpan.FromMilliseconds(50),
    Backoff:     false,
    OnRetry:     (_, n) => frameStats.IncrementRetry(n)
);
```

---

## RetryExtensions

### `Retry` — 동기 재시도

```csharp
// ── Action ────────────────────────────────────────────────────────
(() => serialPort.Open()).Retry(SerialPolicy);

// ── Func<T> ───────────────────────────────────────────────────────
// lssLib.Binary.BufferParser — 파싱 실패 시 재시도
BufResult result = (() => parser.TryParse(schema)).Retry(RetryPolicy.Default);
```

### `RetryAsync` — 비동기 재시도

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
await (() => device.ConnectAsync()).RetryAsync(RetryPolicy.Database, ct);

// ── lssLib.Serialization 탭1: STX 프레임 수신 ────────────────────
// StreamParser의 ReadNextFrameAsync를 재시도로 보호
byte[] stxFrame = await (() => streamParser.ReadNextFrameAsync(ct))
    .RetryAsync(StxFramePolicy, ct);

var parser = new BufferParser(Guard.NotEmpty(stxFrame));
uint  deviceId = parser.Read<uint>(BufType.UInt32LE);
float signal   = parser.Read<float>(BufType.FloatLE);

// ── lssLib.Serialization 탭5: SmoothStep 센서 재연결 ─────────────
// 센서 단절 시 재연결 후 신호 스무딩 재개
await (() => sensorPort.ReOpenAsync(ct))
    .RetryAsync(new RetryPolicy(
        MaxAttempts: 20,
        Delay:       TimeSpan.FromSeconds(1),
        Backoff:     true,
        OnRetry:     (_, n) => ui.ShowReconnecting(n)
    ), ct);
// 재연결 성공 후 ScaleExtensions.SmoothStep으로 신호 보간 재개
float smoothed = rawSignal.SmoothStep(prevSmoothed, alpha: 0.15f);
```

### `RetryOnAsync<T, TEx>` — 조건부 재시도

특정 예외 타입만 재시도하고 나머지는 즉시 전파합니다.

```csharp
// ── lssLib.Serialization 탭6: TextExtensions HTTP 직렬화 전송 ────
// HttpRequestException만 재시도. 직렬화 오류(JsonException)는 즉시 throw
string json = bufSchema.ToJson();    // lssLib.Extensions.TextExtensions
string resp = await (() => httpClient.PostJsonAsync(endpoint, json, ct))
    .RetryOnAsync<string, HttpRequestException>(RetryPolicy.Http, ct);

// ── CRC 검증 실패만 재시도 (커스텀 예외) ─────────────────────────
BufResult verified = await (() => ReceiveAndVerifyAsync(ct))
    .RetryOnAsync<BufResult, CrcMismatchException>(
        new RetryPolicy(MaxAttempts: 3, Delay: TimeSpan.FromMilliseconds(50)),
        ct
    );
```

### `WithTimeout` — 타임아웃 래퍼

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
var frame = await ((CancellationToken t) => sensor.ReadAsync(t))
    .WithTimeout(TimeSpan.FromSeconds(3), operationName: "센서 읽기");

// ── lssLib.Serialization 탭4: BufferDiff 대용량 비교 ─────────────
// 두 프레임 스냅샷의 바이트 수준 diff 연산에 타임아웃 적용
BufferDiffResult diff = await ((CancellationToken t) =>
    Task.Run(() => BufferDiff.Compare(baseSnapshot, newSnapshot, schema), t))
    .WithTimeout(TimeSpan.FromSeconds(10), operationName: "BufferDiff 비교");

logger.Info($"변경 필드 수: {diff.ChangedFieldCount}, " +
            $"유사도: {diff.SimilarityScore:P1}");
```

### `RetryWithTimeout` — 재시도 + 이중 타임아웃 조합

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
var result = await ((CancellationToken t) => FetchAsync(t))
    .RetryWithTimeout(
        policy:            RetryPolicy.Http,
        perAttemptTimeout: TimeSpan.FromSeconds(3),   // 1회 시도 최대 3초
        totalTimeout:      TimeSpan.FromSeconds(15)   // 전체 최대 15초
    );

// ── lssLib.Serialization 탭8: 복합 파이프라인 ────────────────────
// Binary 프레임 수신 → CRC 검증 → 스키마 파싱 전 과정에 이중 타임아웃
byte[] frame = await ((CancellationToken t) =>
    streamParser.ReadAndVerifyAsync(schema, t))  // StreamParser + CrcExtensions
    .RetryWithTimeout(
        policy:            StxFramePolicy,
        perAttemptTimeout: TimeSpan.FromMilliseconds(200),  // 프레임 1개 최대 200ms
        totalTimeout:      TimeSpan.FromSeconds(5)           // 전체 최대 5초
    );
```

### `TryExecuteAsync` — 안전 실행

```csharp
// ── lssLib.Serialization 탭7: Binary 파일 저장 ───────────────────
// 저장 실패 시 예외 없이 UtilResult로 받아 UI에 상태 표시
UtilResult saveR = await (() => Task.Run(() =>
{
    var writer = new BufferWriter();
    foreach (var field in schema.Fields)
        writer.Write(field.Type, data[field.Name]);
    string path = $@"output\{DateTime.Now.ToFileStamp()}.bin";
    path.WriteBytes(writer.ToArray());    // lssLib.Utils.FileExtensions
})).TryExecuteAsync();

if (saveR.IsError)
    viewModel.StatusMessage = $"저장 실패: {saveR.Error!.Message}";
else
    viewModel.StatusMessage = "저장 완료";

// ── 값 반환 + Map 체이닝 ─────────────────────────────────────────
UtilResult<BufResult> parseR = await (() =>
    Task.Run(() => parser.ParseAll(schema))).TryExecuteAsync();

// 성공 시 CRC 검증 결과로 변환 (lssLib.Extensions.CrcExtensions)
UtilResult<bool> crcR = parseR.Map(r => r.RawBytes.ComputeCrc32() == r.ExpectedCrc);
bool crcOk = crcR.UnwrapOr(false);
```

---

## CircuitBreakerPolicy

회로 차단기 동작 설정 불변 값 객체입니다.

### 프리셋

| 프리셋 | 실패 임계 | Open 유지 | HalfOpen 성공 | 적합한 상황 |
|---|---|---|---|---|
| `Default` | 5 | 30 s | 1 | 일반 서비스 |
| `Strict` | 3 | 60 s | 2 | 중요 리소스 |
| `Lenient` | 10 | 10 s | 1 | 빠른 복구 |

### 상태 전이

```
Closed  ──(연속 실패 ≥ FailureThreshold)──►  Open
Open    ──(OpenDuration 경과)──────────────►  HalfOpen
HalfOpen──(성공 ≥ HalfOpenSuccessThreshold)─►  Closed
HalfOpen──(실패)──────────────────────────────►  Open (즉시 재차단)
```

### 사용 예제

```csharp
// ── 공유 인스턴스 — DI 컨테이너 또는 static 필드로 관리 ─────────
static readonly CircuitBreakerState _sensorBreaker = new(
    new CircuitBreakerPolicy(
        FailureThreshold:          5,
        OpenDuration:              TimeSpan.FromSeconds(30),
        HalfOpenSuccessThreshold:  2,
        OnStateChanged: (prev, next) =>
        {
            logger.Warn($"[CB] {prev} → {next}");
            if (next == CircuitState.Open)
                alertService.SendAlert("센서 회로 차단 — 30초 후 복구 시도");
        }
    )
);

// ── 상태 조회 ─────────────────────────────────────────────────────
CircuitState state     = _sensorBreaker.Current;       // Closed / Open / HalfOpen
int          failures  = _sensorBreaker.FailureCount;
TimeSpan     remaining = _sensorBreaker.RemainingOpenDuration;

// ── 수동 제어 ─────────────────────────────────────────────────────
_sensorBreaker.Reset();   // 점검 완료 후 강제 Closed 복귀
_sensorBreaker.Trip();    // 배포 전 예방적 차단
```

---

## CircuitBreakerExtensions

### `ExecuteAsync` — 회로 차단기 보호 실행

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
var frame = await ((CancellationToken t) => sensor.ReadFrameAsync(t))
    .ExecuteAsync(_sensorBreaker, ct);

// ── lssLib.Serialization 탭2: CRC 검증 파이프라인 ────────────────
// CRC 불일치가 연속 5회 발생하면 수신 자체를 차단
static readonly CircuitBreakerState _crcBreaker = new(
    new CircuitBreakerPolicy(
        FailureThreshold: 5,
        OpenDuration:     TimeSpan.FromSeconds(10),
        OnStateChanged:   (_, next) =>
        {
            if (next == CircuitState.Open)
                ui.ShowCrcError("CRC 연속 오류 — 10초 차단 중");
        }
    )
);

async Task<BufResult> ReceiveVerifiedFrameAsync(CancellationToken ct)
{
    return await ((CancellationToken t) => Task.Run(async () =>
    {
        byte[] raw    = await streamParser.ReadNextFrameAsync(t);
        uint   rxCrc  = raw[^4..].ToUInt32LE();          // lssLib.Binary
        uint   calcCrc = raw[..^4].ComputeCrc32();        // lssLib.Extensions.CrcExtensions
        if (rxCrc != calcCrc)
            throw new CrcMismatchException(rxCrc, calcCrc);
        return new BufferParser(raw).ParseAll(schema);
    }, t)).ExecuteAsync(_crcBreaker, t);
}

// Open 상태일 때 호출하면 즉시 CircuitBreakerOpenException
try
{
    var result = await ReceiveVerifiedFrameAsync(ct);
}
catch (CircuitBreakerOpenException ex)
{
    logger.Warn($"회로 차단 중: {ex.RemainingDuration.TotalSeconds:F0}초 후 재시도");
    ui.ShowCircuitOpen(ex.RemainingDuration);
}
```

### `ExecuteWithRetryAsync` — CB + Retry 조합

```csharp
// ── lssLib.Serialization 탭5: SmoothStep 센서 데이터 스트리밍 ────
// Open → 즉시 차단 (재시도 없음)
// Closed/HalfOpen → 실패 시 RetryPolicy 설정대로 재시도
static readonly CircuitBreakerState _streamBreaker = new(CircuitBreakerPolicy.Default);

float signal = await ((CancellationToken t) => Task.Run(async () =>
{
    byte[]  raw    = await sensorStream.ReadAsync(t);
    float   raw_v  = new BufferParser(raw).Read<float>(BufType.FloatLE);

    // lssLib.Extensions.ScaleExtensions — Hysteresis 잡음 제거 후 SmoothStep 보간
    float filtered  = raw_v.Hysteresis(prev: _lastSignal, low: 0.1f, high: 0.9f);
    float smoothed  = filtered.SmoothStep(_lastSmoothed, alpha: 0.2f);
    return smoothed;
}, t)).ExecuteWithRetryAsync(_streamBreaker, RetryPolicy.Default, ct);
```

### `TryExecuteAsync` — 안전 실행 (예외 없음)

```csharp
// ── lssLib.Serialization 탭8: 복합 파이프라인 안전 실행 ──────────
var r = await ((CancellationToken t) => ReadAndParseAsync(t))
    .TryExecuteAsync(_sensorBreaker, ct);

switch (r)
{
    case { IsOk: true }:
        UpdateDisplay(r.Unwrap());
        break;
    case { Error: CircuitBreakerOpenException cbEx }:
        ui.ShowCircuitOpen(cbEx.RemainingDuration);
        break;
    default:
        ui.ShowError(r.Error!.Message);
        break;
}
```

---

## RateLimiterPolicy

슬라이딩 윈도우 방식의 속도 제한 설정 값 객체입니다.

### 팩토리 메서드

```csharp
RateLimiterPolicy.PerSecond(10)    // 초당 10회
RateLimiterPolicy.PerMinute(100)   // 분당 100회
RateLimiterPolicy.PerHour(1_000)   // 시간당 1,000회
RateLimiterPolicy.PerDay(10_000)   // 일당 10,000회
```

### 프리셋

| 프리셋 | 제한 | 적합한 상황 |
|---|---|---|
| `ApiDefault` | 분당 60 | REST API 기본 |
| `Strict` | 초당 10 | 처리량 민감 경로 |
| `Lenient` | 시간당 1,000 | 배치 처리 |
| `LoginAttempt` | 분당 5 | 보안 강화 경로 |

### 상태 조회

```csharp
RateLimiterState limiter = new(RateLimiterPolicy.PerSecond(30));

int      available = limiter.Available;         // 현재 윈도우 내 남은 슬롯
int      used      = limiter.Used;              // 현재 윈도우 내 사용된 슬롯
DateTime nextSlot  = limiter.NextAvailableAt;   // 다음 슬롯 예상 시각
```

---

## RateLimiterExtensions

### `ExecuteAsync` — 속도 제한 보호 실행

```csharp
// ── 기본 사용 ──────────────────────────────────────────────────────
static readonly RateLimiterState _apiLimiter =
    new(RateLimiterPolicy.PerMinute(60));

var resp = await ((CancellationToken t) => api.GetAsync(url, t))
    .ExecuteAsync(_apiLimiter, ct);

// ── lssLib.Serialization 탭3: RingBuffer 스트리밍 처리량 제어 ────
// RingBuffer에서 프레임을 꺼내 처리하는 속도를 초당 30프레임으로 제한
static readonly RateLimiterState _frameLimiter =
    new(RateLimiterPolicy.PerSecond(30));   // 30 FPS 상한

async Task ConsumeRingBufferAsync(RingBuffer<byte[]> ring, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        if (!ring.TryDequeue(out var frame)) { await Task.Delay(1, ct); continue; }

        await ((CancellationToken t) => Task.Run(() =>
        {
            var    parser   = new BufferParser(Guard.NotEmpty(frame));
            uint   id       = parser.Read<uint>(BufType.UInt32LE);
            float  temp     = parser.Read<float>(BufType.FloatLE);
            float  smoothed = temp.SmoothStep(_lastTemp, 0.15f);  // ScaleExtensions
            _lastTemp = smoothed;
            ui.UpdateChart(id, smoothed);
        }, t)).ExecuteAsync(_frameLimiter, t);
    }
}

// ── ThrowOnExceeded: false — 한도 초과 시 조용히 건너뜀 ───────────
static readonly RateLimiterState _softLimiter =
    new(RateLimiterPolicy.PerSecond(10) with { ThrowOnExceeded = false });

await func.ExecuteAsync(_softLimiter, ct);   // 초과 시 null/default 반환, 예외 없음
```

### `ExecuteWithWaitAsync` — 슬롯 대기 후 실행

```csharp
// ── 슬롯이 열릴 때까지 대기 (최대 5초) ───────────────────────────
var result = await ((CancellationToken t) => api.PostAsync(payload, t))
    .ExecuteWithWaitAsync(_apiLimiter,
        maxWait: TimeSpan.FromSeconds(5),
        ct:      ct);

// ── lssLib.Serialization 탭6: TextExtensions 직렬화 전송 ─────────
// 분당 60회 제한 API에 JSON 직렬화 결과를 전송 (슬롯 대기 허용)
async Task SendSerializedAsync(BufSchema schema, object data, CancellationToken ct)
{
    string json = schema.SerializeToJson(data);    // lssLib.Extensions.TextExtensions
    string etag = json.ToUtf8Bytes().ComputeCrc32().ToString("X8");  // CrcExtensions

    await ((CancellationToken t) => httpClient.PostAsync(
        $"{endpoint}?etag={etag}", json, t))
        .ExecuteWithWaitAsync(_apiLimiter,
            maxWait: TimeSpan.FromSeconds(3),
            ct:      ct);
}
```

### `ExecuteAsync(limiter, circuitBreaker)` — RL + CB 조합

```csharp
// ── lssLib.Serialization 탭8: 3중 보호 복합 파이프라인 ───────────
// Rate Limiter → Circuit Breaker → Retry 순서로 중첩 적용
static readonly RateLimiterState    _limiter  = new(RateLimiterPolicy.PerSecond(30));
static readonly CircuitBreakerState _breaker  = new(CircuitBreakerPolicy.Default);

async Task<BufResult> AcquireFrameAsync(CancellationToken ct)
{
    return await ((CancellationToken t) => Task.Run(async () =>
    {
        // ① StreamParser로 STX 프레임 수신
        byte[] raw = await streamParser.ReadNextFrameAsync(t);

        // ② CRC 검증 (lssLib.Extensions.CrcExtensions)
        Guard.That(raw.VerifyCrc32(), "CRC 검증 실패");

        // ③ Binary 파싱 (lssLib.Binary.BufferParser)
        return new BufferParser(raw).ParseAll(schema);
    }, t)).ExecuteAsync(_limiter, _breaker, ct);   // RL + CB 동시 적용
}

// Retry까지 포함한 완전한 3중 조합
BufResult result = await ((CancellationToken t) =>
    Task.Run(() => AcquireFrameAsync(t).GetAwaiter().GetResult(), t))
    .ExecuteWithRetryAsync(_breaker, RetryPolicy.Default, ct);
```

---

## 전체 파이프라인 — 복합 시나리오

아래는 `lssLib.Serialization.WpfDemo` 탭 8 (복합 파이프라인)의 핵심 흐름입니다.  
lssLib 생태계 전 레이어가 협력하는 실전 패턴입니다.

```csharp
// ── 공유 상태 (앱 수명 동안 유지) ────────────────────────────────
static readonly CircuitBreakerState _breaker = new(
    new CircuitBreakerPolicy(
        FailureThreshold: 5,
        OpenDuration:     TimeSpan.FromSeconds(30),
        OnStateChanged:   (prev, next) =>
            App.Logger.Warn($"[CB] {prev} → {next}")
    )
);

static readonly RateLimiterState _limiter =
    new(RateLimiterPolicy.PerSecond(30));

static readonly RetryPolicy _framePolicy = new(
    MaxAttempts: 3,
    Delay:       TimeSpan.FromMilliseconds(50),
    OnRetry:     (ex, n) => App.Logger.Debug($"[Retry {n}] {ex.Message}")
);

// ── 수신 루프 ─────────────────────────────────────────────────────
async Task RunPipelineAsync(CancellationToken ct)
{
    await foreach (var raw in streamParser.ReadFramesAsync(ct))
    {
        // §1  Rate Limit + Circuit Breaker + Retry 3중 보호
        UtilResult<BufResult> r = await ((CancellationToken t) =>
            Task.Run(async () =>
            {
                // §2  CRC 검증 (lssLib.Extensions.CrcExtensions)
                uint rxCrc   = raw[^4..].ToUInt32LE();
                uint calcCrc = raw[..^4].ComputeCrc32();
                Guard.That(rxCrc == calcCrc,
                    $"CRC 불일치: 수신 {rxCrc:X8} ≠ 계산 {calcCrc:X8}");

                // §3  Binary 파싱 (lssLib.Binary)
                var parser  = new BufferParser(raw[..^4]);
                uint   id   = parser.Read<uint>(BufType.UInt32LE);
                float  temp = parser.Read<float>(BufType.FloatLE);
                float  hum  = parser.Read<float>(BufType.FloatLE);

                // §4  신호 보간 (lssLib.Extensions.ScaleExtensions)
                float smoothTemp = temp.SmoothStep(_lastTemp, alpha: 0.2f);
                float filteredHum = hum.Hysteresis(_lastHum, low: 0.05f, high: 0.95f);

                _lastTemp = smoothTemp;
                _lastHum  = filteredHum;

                return new BufResult(id, smoothTemp, filteredHum,
                    timestamp: DateTime.Now.ToIsoDateTime());  // lssLib.Utils
            }, t)
        ).TryExecuteAsync(_breaker, t);   // CB 보호 실행

        // §5  결과 처리
        switch (r)
        {
            case { IsOk: true }:
                var data = r.Unwrap();
                ui.UpdateSensorDisplay(data);

                // §6  로그 파일 저장 (lssLib.Utils.FileExtensions)
                string logPath = $@"logs\{DateTime.Today.ToIsoDate()}.log";
                logPath.AppendLine(
                    $"[{data.Timestamp}] ID={data.Id} " +
                    $"Temp={data.Temp:F2} Hum={data.Hum:F2}");
                break;

            case { Error: CircuitBreakerOpenException cbEx }:
                ui.ShowCircuitOpen(cbEx.RemainingDuration);
                await Task.Delay(1_000, ct);
                break;

            case { Error: RateLimitExceededException }:
                await Task.Delay(33, ct);   // 30 FPS 간격 대기
                break;

            default:
                ui.ShowError(r.Error!.Message);
                break;
        }
    }
}

// ── 오래된 로그 정리 (lssLib.Utils.FileExtensions) ────────────────
void CleanupOldLogs()
{
    @"logs".EnumerateByDate("*.log")
           .Where(f => f.GetLastModified() < DateTime.Now.AddDays(-30))
           .ToList()
           .ForEach(f => f.TryDelete());
}
```

---

## 인스턴스 수명 관리

`CircuitBreakerState`와 `RateLimiterState`는 **공유 인스턴스**로 관리해야  
전체 호출에 걸쳐 누적 상태가 유지됩니다.

```csharp
// ── WPF App.xaml.cs — DI 등록 패턴 ─────────────────────────────
public partial class App : Application
{
    public static readonly CircuitBreakerState SensorBreaker =
        new(CircuitBreakerPolicy.Default);

    public static readonly RateLimiterState FrameLimiter =
        new(RateLimiterPolicy.PerSecond(30));

    // ViewModel에서 주입받아 사용
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var vm = new MainViewModel(SensorBreaker, FrameLimiter);
        new MainWindow { DataContext = vm }.Show();
    }
}

// ── ViewModel ──────────────────────────────────────────────────
public class MainViewModel
{
    private readonly CircuitBreakerState _breaker;
    private readonly RateLimiterState    _limiter;

    public MainViewModel(CircuitBreakerState breaker, RateLimiterState limiter)
    {
        _breaker = breaker;
        _limiter = limiter;
    }

    // 현재 상태 바인딩 (WPF INotifyPropertyChanged)
    public string CircuitStatus => _breaker.Current.ToString();
    public int    LimiterSlots  => _limiter.Available;
}
```

---

## 패턴 선택 가이드

| 상황 | 권장 패턴 |
|---|---|
| 네트워크 요청 일시 오류 | `RetryAsync(RetryPolicy.Http)` |
| DB 연결 타임아웃 | `RetryAsync(RetryPolicy.Database)` |
| 특정 예외만 재시도 | `RetryOnAsync<T, TEx>` |
| 작업 전체 시간 제한 | `WithTimeout` |
| 재시도 + 시간 제한 동시 | `RetryWithTimeout` |
| 연속 실패 시 서비스 차단 | `CircuitBreakerExtensions.ExecuteAsync` |
| CB + Retry 조합 | `ExecuteWithRetryAsync` |
| 처리량 상한 강제 | `RateLimiterExtensions.ExecuteAsync` |
| 슬롯 대기 허용 | `ExecuteWithWaitAsync` |
| CB + RL 동시 적용 | `ExecuteAsync(limiter, breaker)` |
| 실패 무시·계속 진행 | `TryExecuteAsync` → `UtilResult` |

---

## 설계 원칙

| 원칙 | 내용 |
|---|---|
| **Extension-method only** | 인터페이스·추상 클래스 없음. 상태 객체 주입으로 조합 |
| **No external dependencies** | BCL만 참조. 어떤 lssLib 패키지와도 독립적으로 참조 가능 |
| **Immutable policy** | `RetryPolicy` · `CircuitBreakerPolicy` · `RateLimiterPolicy` 모두 `readonly record struct` |
| **Shared state** | `CircuitBreakerState` · `RateLimiterState`는 공유 인스턴스로 수명 관리 |
| **CancellationToken 일관 전파** | `OperationCanceledException`은 재시도 없이 즉시 상위 전파 |
| **`UtilResult<T>`** | 예외 없는 안전 실행 결과를 명시적 성공/실패 값 타입으로 표현 |
