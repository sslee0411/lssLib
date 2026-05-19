# lssLib.DB.InfluxDB

> lssLib.DB InfluxDB v2.0 Provider  
> InfluxDB.Client 공식 패키지 기반 시계열 DB 접근 모듈

---

## Ver History

| 버전 | 날짜 | 내용 |
|---|---|---|
| v1.0.0 | 2025-05-19 | 최초 작성 — InfluxDbContext / InfluxRepository / LineProtocolBuilder |

---

## 개요

```
lssLib.DB.InfluxDB
├── InfluxDbContext.cs       Flux 쿼리 / Line Protocol 쓰기 / 버킷 관리
├── InfluxRepository.cs      RowMapper 기반 엔티티 조회 + 쓰기 간편 API
└── LineProtocolBuilder.cs   Measurement / Tag / Field / Timestamp 조립
```

### 관계형 DB와의 차이

| 관계형 DB | InfluxDB |
|---|---|
| SQL | Flux 쿼리 |
| INSERT | Line Protocol 쓰기 |
| Table | Measurement |
| Primary Key | Tag (인덱싱 문자열) |
| Column | Field (측정값) |
| Transaction | 없음 (시계열 특성) |
| Stored Procedure | 없음 |

---

## 의존성

```xml
<!-- lssLib.DB.InfluxDB.csproj -->
<ProjectReference Include="..\lssLib.DB\lssLib.DB.csproj" />
<PackageReference Include="InfluxDB.Client" Version="5.*" />
```

| 참조 | 용도 |
|---|---|
| `lssLib.DB` (Core) | DbContextBase / RepositoryBase / DbResult / DbParam |
| `lssLib.Log` | LogManager 로그 기록 |
| `InfluxDB.Client` | InfluxDB v2.0 공식 클라이언트 |

---

## 빠른 시작

### 1. 설정 생성

```csharp
var cfg = new InfluxDbConfig(
    url:    "http://localhost:8086",
    token:  "my-api-token",
    org:    "my-org",
    bucket: "sensor-data",
    commandTimeoutSec: 30);
```

### 2. Context 연결 및 해제

```csharp
await using var ctx = new InfluxDbContext(cfg);
await ctx.OpenAsync();

// 작업 수행 ...

// await using → DisposeAsync() 자동 호출로 연결 해제
```

### 3. Flux 쿼리 (DataTable 반환)

```csharp
DbResult<DataTable> r = await ctx.QueryFluxAsync("""
    from(bucket: "sensor-data")
      |> range(start: -1h)
      |> filter(fn: (r) => r._measurement == "sensor_data")
      |> filter(fn: (r) => r.plant == "A01")
      |> sort(columns: ["_time"], desc: true)
    """);

if (r.IsOk)
    DgSensor.ItemsSource = r.Value?.DefaultView;
else
    MessageBox.Show(r.Message);
```

### 4. Line Protocol 쓰기

```csharp
// 단건 쓰기
string line = new LineProtocolBuilder("sensor_data")
    .Tag("plant", "A01")
    .Tag("line",  "L1")
    .Field("temperature", 72.5)
    .Field("pressure",    1.013)
    .Timestamp(DateTime.UtcNow)
    .Build();

DbResult<int> wr = await ctx.WriteLineProtocolAsync(line);
```

### 5. 배치 쓰기

```csharp
// 여러 Point를 한 번에 쓰기 (성능 최적화)
var lines = sensorList.Select(s =>
    new LineProtocolBuilder("sensor_data")
        .Tag("id",    s.SensorId.ToString())
        .Tag("plant", s.PlantCd)
        .Field("value",  s.Value)
        .Field("status", s.Status)
        .Timestamp(s.RegDt)
        .Build());

DbResult<int> wr = await ctx.WriteBatchAsync(lines);
if (wr.IsOk)
    LogManager.Instance.Info("DB", $"{wr.Value}개 Point 저장 완료");
```

---

## API 레퍼런스

### InfluxDbConfig

```csharp
var cfg = new InfluxDbConfig(
    url:              "http://localhost:8086",  // 서버 URL
    token:            "my-token",               // API 인증 토큰
    org:              "my-org",                 // 조직 이름
    bucket:           "sensor-data",            // 기본 버킷
    commandTimeoutSec: 30                        // 쿼리 타임아웃 (초)
);
```

---

### InfluxDbContext

#### 연결 관리

| 메서드 | 반환 | 설명 |
|---|---|---|
| `OpenAsync(ct)` | `Task` | InfluxDB 연결 + Health Check |
| `CloseAsync()` | `Task` | 연결 해제 |
| `PingAsync()` | `Task<bool>` | 서버 응답 확인 |
| `State` | `ConnectionState` | 현재 연결 상태 |

#### Flux 쿼리

| 메서드 | 반환 | 설명 |
|---|---|---|
| `QueryFluxAsync(flux, ct)` | `Task<DbResult<DataTable>>` | Flux → DataTable |
| `QueryFluxAsync<T>(flux, ct)` | `Task<DbResult<List<T>>>` | Flux → 엔티티 목록 (InfluxDB.Client 자동 매핑) |

#### Line Protocol 쓰기

| 메서드 | 반환 | 설명 |
|---|---|---|
| `WriteLineProtocolAsync(line, precision, ct)` | `Task<DbResult<int>>` | 단건 Line Protocol 쓰기 |
| `WriteBatchAsync(lines, precision, ct)` | `Task<DbResult<int>>` | 배치 Line Protocol 쓰기 |
| `WriteAsync(builder, precision, ct)` | `Task<DbResult<int>>` | LineProtocolBuilder 직접 전달 |

#### 버킷 관리

| 메서드 | 반환 | 설명 |
|---|---|---|
| `BucketExistsAsync(name, ct)` | `Task<bool>` | 버킷 존재 여부 확인 |
| `CreateBucketAsync(name, retentionHours, ct)` | `Task<DbResult<int>>` | 버킷 생성 |

> **⚠️ 미지원 메서드**  
> InfluxDB는 ADO.NET SQL 모델을 지원하지 않으므로 아래 메서드는 Fail 반환합니다.  
> `ExecuteAsync()` / `QueryTableAsync()` / `CallSpAsync()`

---

### LineProtocolBuilder

#### 체이닝 메서드

| 메서드 | 설명 |
|---|---|
| `Tag(key, value)` | Tag 추가 (인덱싱 문자열 — GROUP BY / 필터 기준) |
| `Field(key, double)` | Float 측정값 추가 |
| `Field(key, long)` | Integer 측정값 추가 (접미사 `i` 자동 추가) |
| `Field(key, string)` | 문자열 측정값 추가 |
| `Field(key, bool)` | Boolean 측정값 추가 |
| `Field(key, object?)` | 타입 자동 분기 추가 |
| `Timestamp(DateTime)` | UTC 타임스탬프 설정 (Local 자동 변환) |
| `Timestamp(DateTimeOffset)` | DateTimeOffset 타임스탬프 설정 |
| `Timestamp(long)` | 나노초 Unix timestamp 직접 지정 |
| `Build()` | Line Protocol 문자열 생성 |

#### 정적 메서드

| 메서드 | 설명 |
|---|---|
| `BuildBatch(IEnumerable<string>)` | 여러 Line 문자열을 `\n` 으로 연결 |

#### Line Protocol 형식

```
measurementName,tag1=val1,tag2=val2 field1=val1,field2=val2 timestamp
│               │                   │                        │
Measurement     Tags (정렬됨)        Fields                  nanosecond
```

#### 이스케이프 규칙

| 위치 | 이스케이프 문자 |
|---|---|
| Measurement / Tag Key / Field Key | 공백 `\ ` · 쉼표 `\,` · 등호 `\=` |
| Tag 값 | 공백 `\ ` · 쉼표 `\,` · 등호 `\=` |
| Field 문자열 값 | 쌍따옴표 `\"` · 역슬래시 `\\` |

---

### InfluxRepository\<T\>

```csharp
// ① RowMapper 정의 (DataRow → SensorRow 변환 규칙)
RowMapper<SensorRow> mapper = row => new SensorRow
{
    Time        = DateTime.Parse(row["_time"].ToString()!),
    Measurement = row["_measurement"].ToString()!,
    Plant       = row["plant"].ToString()!,
    Field       = row["_field"].ToString()!,
    Value       = double.Parse(row["_value"].ToString()!),
};

// ② Repository 생성
var repo = new InfluxRepository<SensorRow>(ctx, mapper);

// ③ Flux 쿼리 → List<SensorRow>
DbResult<List<SensorRow>> r = await repo.QueryFluxAsync("""
    from(bucket: "sensor-data")
      |> range(start: -24h)
      |> filter(fn: (r) => r.plant == "A01")
    """);

// ④ 간편 쓰기
await repo.WriteAsync(
    measurement: "sensor_data",
    tags:        [("plant", "A01"), ("line", "L1")],
    fields:      [("temperature", (object)72.5), ("pressure", 1.013)],
    time:        DateTime.UtcNow);
```

#### 쓰기 메서드

| 메서드 | 설명 |
|---|---|
| `WriteAsync(measurement, tags, fields, time, precision, ct)` | 튜플 배열로 간편 쓰기 |
| `WriteAsync(builder, precision, ct)` | LineProtocolBuilder 직접 전달 |
| `WriteBatchAsync(builders, precision, ct)` | Builder 목록 배치 쓰기 |
| `WriteBatchAsync(lineProtocols, precision, ct)` | 문자열 목록 배치 쓰기 |

---

## Flux 쿼리 예제

### 최근 1시간 데이터

```
from(bucket: "sensor-data")
  |> range(start: -1h)
  |> filter(fn: (r) => r._measurement == "sensor_data")
```

### 특정 Tag 필터 + 정렬

```
from(bucket: "sensor-data")
  |> range(start: -24h)
  |> filter(fn: (r) => r._measurement == "sensor_data")
  |> filter(fn: (r) => r.plant == "A01" and r.line == "L1")
  |> sort(columns: ["_time"], desc: true)
  |> limit(n: 100)
```

### 1분 평균 집계

```
from(bucket: "sensor-data")
  |> range(start: -6h)
  |> filter(fn: (r) => r._measurement == "sensor_data")
  |> filter(fn: (r) => r._field == "temperature")
  |> aggregateWindow(every: 1m, fn: mean, createEmpty: false)
```

### 특정 기간 조회

```csharp
// C# 에서 동적으로 범위 지정
string flux = $"""
    from(bucket: "{cfg.Bucket}")
      |> range(start: {startDt:yyyy-MM-ddTHH:mm:ssZ},
               stop:  {endDt:yyyy-MM-ddTHH:mm:ssZ})
      |> filter(fn: (r) => r._measurement == "sensor_data")
    """;
var r = await ctx.QueryFluxAsync(flux);
```

---

## 아키텍처 위치

```
lssLib.DB (Core)
  ├── Core/           DbResult / DbParam / DbConfig / DbException
  ├── Contracts/      IDbContext / IRepository / IQueryBuilder
  ├── Abstractions/   DbContextBase / RepositoryBase / QueryBuilderBase
  └── Helpers/        DbHelper
          ▲
          │ ProjectReference
lssLib.DB.InfluxDB
  ├── InfluxDbContext.cs      DbContextBase 파생
  ├── InfluxRepository.cs     RepositoryBase<T> 파생
  └── LineProtocolBuilder.cs  시계열 전용 Point 빌더
```

---

## 주의사항

1. **InfluxDB v2.0 이상 전용** — v1.x API(InfluxQL)와 호환되지 않습니다.
2. **Token 인증 필수** — InfluxDB v2.0은 사용자/패스워드 방식을 지원하지 않습니다.
3. **트랜잭션 없음** — 배치 쓰기 실패 시 부분 저장될 수 있습니다.
4. **Tag vs Field 구분** — 자주 필터링하는 값은 `Tag`, 측정값은 `Field`로 분리하세요.
5. **Timestamp 정밀도** — 기본 `WritePrecision.Ns` (나노초). Flux 쿼리의 `range()` 범위와 맞춰야 합니다.
6. **`await using` 사용 권장** — `DisposeAsync()` 자동 호출로 연결이 안전하게 해제됩니다.
