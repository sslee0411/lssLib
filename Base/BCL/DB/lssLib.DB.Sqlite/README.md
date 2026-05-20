# lssLib.DB.Sqlite

> lssLib.DB SQLite Provider  
> Microsoft.Data.Sqlite 기반 파일 기반 경량 DB 접근 모듈  
> 로컬 설정 / 로그 / 캐시 / 오프라인 데이터 저장에 적합

---

## Ver History

| 버전 | 날짜 | 내용 |
|---|---|---|
| v1.0.0 | 2025-05-19 | 최초 작성 — SqliteDbContext / SqliteRepository / SqliteQueryBuilder |

---

## 개요

```
lssLib.DB.Sqlite
├── SqliteDbContext.cs      연결·쿼리·트랜잭션·PRAGMA·테이블 초기화 구현
├── SqliteRepository.cs     RowMapper 기반 CRUD + Upsert / InsertIgnore 확장
└── SqliteQueryBuilder.cs   @ParamName / LIMIT N SQLite 문법 쿼리 빌더
```

### SQLite 특성

| 항목 | 내용 |
|---|---|
| DB 형태 | 파일 1개 = DB 1개 |
| ConnectionString | `Data Source=파일경로.db` |
| SP(저장 프로시저) | ❌ 미지원 |
| 트랜잭션 | ✅ 지원 (단일 Writer 제한) |
| 파라미터 접두사 | `@` (또는 `$`, `:` 모두 동작) |
| 타입 시스템 | TEXT / INTEGER / REAL / BLOB (4가지) |
| 주요 용도 | 로컬 설정·로그·캐시·오프라인 저장 |

---

## 의존성

```xml
<!-- lssLib.DB.Sqlite.csproj -->
<ProjectReference Include="..\lssLib.DB\lssLib.DB.csproj" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.*" />
```

| 참조 | 용도 |
|---|---|
| `lssLib.DB` (Core) | DbContextBase / RepositoryBase / DbResult / DbParam |
| `lssLib.Log` | LogManager 로그 기록 |
| `Microsoft.Data.Sqlite` | Microsoft 공식 SQLite 드라이버 (.NET 8 내장 버전) |

---

## 빠른 시작

### 1. 설정 생성

```csharp
var cfg = new RelationalDbConfig(
    DbProviderType.Sqlite,
    "Data Source=D:\\IIoT\\config.db",
    commandTimeoutSec: 30);
```

### 2. Context 생성 + 테이블 초기화

```csharp
await using var ctx = new SqliteDbContext(cfg);
await ctx.OpenAsync();

// WAL 모드 활성화 (동시 읽기 성능 향상)
await ctx.SetPragmaAsync("journal_mode", "WAL");
await ctx.SetPragmaAsync("foreign_keys", "ON");

// 테이블이 없으면 생성
await ctx.EnsureTableAsync("""
    CREATE TABLE IF NOT EXISTS sensor_config (
        id         INTEGER PRIMARY KEY AUTOINCREMENT,
        plant_cd   TEXT NOT NULL,
        sensor_id  INTEGER NOT NULL,
        threshold  REAL,
        use_yn     TEXT DEFAULT 'Y',
        reg_dt     TEXT DEFAULT (datetime('now','localtime'))
    )
    """);
```

### 3. Repository 생성

```csharp
RowMapper<SensorConfig> mapper = row => new SensorConfig
{
    Id        = Convert.ToInt32(row["id"]),
    PlantCd   = row["plant_cd"].ToString()!,
    SensorId  = Convert.ToInt32(row["sensor_id"]),
    Threshold = row["threshold"] is DBNull ? null : Convert.ToDouble(row["threshold"]),
    UseYn     = row["use_yn"].ToString()!,
    RegDt     = row["reg_dt"].ToString()!,
};

var repo = new SqliteRepository<SensorConfig>(ctx, mapper);
```

### 4. SQL 조회

```csharp
// 목록 조회
DbResult<List<SensorConfig>> r = await repo.QueryAsync(
    "SELECT * FROM sensor_config WHERE plant_cd = @P1 AND use_yn = 'Y'",
    [DbParam.In("@P1", "A01")]);

if (r.IsOk) DgConfig.ItemsSource = r.Value;

// 단건 조회
DbResult<SensorConfig?> r2 = await repo.QuerySingleAsync(
    "SELECT * FROM sensor_config WHERE id = @ID",
    [DbParam.In("@ID", 1)]);

// 스칼라 조회
DbResult<int?> r3 = await repo.QueryScalarAsync<int>(
    "SELECT COUNT(*) FROM sensor_config WHERE use_yn = 'Y'");
```

### 5. Upsert (INSERT OR REPLACE)

```csharp
// PRIMARY KEY 중복 시 기존 행 교체
DbResult<int> r = await repo.UpsertAsync(
    "sensor_config",
    [
        ("plant_cd",  "A01"),
        ("sensor_id", 42),
        ("threshold", 80.0),
    ]);

// 여러 건 Upsert
foreach (var config in configList)
{
    await repo.UpsertAsync("sensor_config",
    [
        ("plant_cd",  config.PlantCd),
        ("sensor_id", config.SensorId),
        ("threshold", config.Threshold),
    ]);
}
```

### 6. InsertIgnore (INSERT OR IGNORE)

```csharp
// PRIMARY KEY 중복 시 무시 (기존 행 유지)
DbResult<int> r = await repo.InsertIgnoreAsync(
    "sensor_config",
    [
        ("plant_cd",  "A01"),
        ("sensor_id", 42),
        ("threshold", 80.0),
    ]);
```

### 7. DML 실행

```csharp
// INSERT
DbResult<int> r = await repo.ExecuteAsync(
    "INSERT INTO sensor_config (plant_cd, sensor_id, threshold) VALUES (@P1, @P2, @P3)",
    [
        DbParam.In("@P1", "A01"),
        DbParam.In("@P2", 42),
        DbParam.In("@P3", 80.0),
    ]);

// UPDATE
DbResult<int> r2 = await repo.ExecuteAsync(
    "UPDATE sensor_config SET threshold = @VAL WHERE sensor_id = @ID",
    [DbParam.In("@VAL", 90.0), DbParam.In("@ID", 42)]);

// DELETE
DbResult<int> r3 = await repo.ExecuteAsync(
    "DELETE FROM sensor_config WHERE use_yn = 'N'");
```

### 8. 트랜잭션

```csharp
await ctx.BeginTransactionAsync();
try
{
    await repo.ExecuteAsync(
        "UPDATE sensor_config SET use_yn='N' WHERE sensor_id = @ID",
        [DbParam.In("@ID", 42)]);

    await repo.ExecuteAsync(
        "INSERT INTO config_log (sensor_id, log_msg) VALUES (@ID, @MSG)",
        [DbParam.In("@ID", 42), DbParam.In("@MSG", "비활성화")]);

    await ctx.CommitAsync();
}
catch
{
    await ctx.RollbackAsync();
    throw;
}
```

### 9. 쿼리 빌더

```csharp
var qb = new SqliteQueryBuilder();

// SELECT + LIMIT
var (sql, ps) = qb
    .From("sensor_config")
    .Select("id", "plant_cd", "sensor_id", "threshold")
    .Where("plant_cd",  QueryOp.Eq,         "A01")
    .Where("use_yn",    QueryOp.Eq,          "Y")
    .Where("threshold", QueryOp.GtEq,        50.0)
    .OrderBy("sensor_id")
    .Limit(50)
    .Build();
// → SELECT id, plant_cd, sensor_id, threshold
//   FROM sensor_config
//   WHERE plant_cd = @p0 AND use_yn = @p1 AND threshold >= @p2
//   ORDER BY sensor_id ASC
//   LIMIT 50

var r = await repo.QueryAsync(sql, ps);

// DELETE
var (sql2, ps2) = qb.Reset()
    .Delete("sensor_config")
    .Where("use_yn", QueryOp.Eq, "N")
    .BuildDelete();

await repo.ExecuteAsync(sql2, ps2);
```

### 10. PRAGMA 설정

```csharp
// WAL 모드 — 동시 읽기 성능 향상 (쓰기 중 읽기 가능)
await ctx.SetPragmaAsync("journal_mode", "WAL");

// 외래키 제약 활성화 (기본 OFF)
await ctx.SetPragmaAsync("foreign_keys", "ON");

// 페이지 캐시 크기 설정 (KB 단위 음수)
await ctx.SetPragmaAsync("cache_size", "-10000");  // 10MB

// 동기화 모드 (NORMAL = 성능/안전 균형)
await ctx.SetPragmaAsync("synchronous", "NORMAL");

// PRAGMA 값 조회
string mode = await ctx.GetPragmaAsync("journal_mode");
// → "wal"
```

---

## API 레퍼런스

### SqliteDbContext

#### 기본 메서드 (DbContextBase 상속)

| 메서드 | 반환 | 설명 |
|---|---|---|
| `OpenAsync(ct)` | `Task` | SQLite 파일 연결 |
| `CloseAsync()` | `Task` | 연결 해제 |
| `BeginTransactionAsync(ct)` | `Task` | 트랜잭션 시작 |
| `CommitAsync()` | `Task` | 트랜잭션 커밋 |
| `RollbackAsync()` | `Task` | 트랜잭션 롤백 |
| `ExecuteAsync(sql, type, ps, ct)` | `Task<DbResult<int>>` | DML 실행 |
| `QueryTableAsync(sql, type, ps, ct)` | `Task<DbResult<DataTable>>` | SELECT → DataTable |
| `CallSpAsync(spName, ps, ct)` | `Task<DbResult<SpResult>>` | ❌ Fail 반환 (미지원) |

#### SQLite 전용 메서드

| 메서드 | 반환 | 설명 |
|---|---|---|
| `EnsureTableAsync(ddl, ct)` | `Task<DbResult<int>>` | 테이블 없으면 생성 |
| `EnsureTablesAsync(ddls, ct)` | `Task<DbResult<int>>` | 여러 테이블 한 번에 초기화 |
| `SetPragmaAsync(pragma, value, ct)` | `Task<DbResult<int>>` | PRAGMA 설정 |
| `GetPragmaAsync(pragma, ct)` | `Task<string>` | PRAGMA 값 조회 |
| `DbFilePath` | `string` | DB 파일 경로 |
| `DbFileExists` | `bool` | DB 파일 존재 여부 |

### SqliteRepository\<T\>

`RepositoryBase<T>` 전체 메서드 + 아래 추가

| 메서드 | 설명 |
|---|---|
| `UpsertAsync(table, columns, ct)` | INSERT OR REPLACE — 중복 시 교체 |
| `InsertIgnoreAsync(table, columns, ct)` | INSERT OR IGNORE — 중복 시 무시 |

### SqliteQueryBuilder

| 항목 | 값 |
|---|---|
| 파라미터 접두사 | `@` |
| LIMIT 구현 | `LIMIT N` (후행) |
| SELECT 접두어 | `SELECT` |

---

## DbParamType → SqliteType 매핑

| DbParamType | SqliteType | SQLite 실제 타입 |
|---|---|---|
| TinyInt / SmallInt / Int / BigInt / Boolean | Integer | INTEGER |
| Float / Double / Decimal | Real | REAL |
| Binary | Blob | BLOB |
| 그 외 (VarChar, Char, Text, DateTime 등) | Text | TEXT |

> SQLite는 타입 선호도(Type Affinity) 시스템을 사용합니다.  
> 실제 저장 타입은 값에 따라 자동 결정됩니다.

---

## 앱 시작 시 권장 초기화 패턴

```csharp
// App.xaml.cs 또는 MainWindow.xaml.cs
private async Task InitDatabaseAsync()
{
    var cfg = new RelationalDbConfig(
        DbProviderType.Sqlite,
        $"Data Source={AppDomain.CurrentDomain.BaseDirectory}app.db");

    _ctx = new SqliteDbContext(cfg);
    await _ctx.OpenAsync();

    // 성능 최적화 PRAGMA
    await _ctx.SetPragmaAsync("journal_mode", "WAL");
    await _ctx.SetPragmaAsync("foreign_keys", "ON");
    await _ctx.SetPragmaAsync("synchronous",  "NORMAL");

    // 스키마 초기화 (없는 테이블만 생성)
    await _ctx.EnsureTableAsync("""
        CREATE TABLE IF NOT EXISTS app_config (
            key    TEXT PRIMARY KEY,
            value  TEXT NOT NULL,
            reg_dt TEXT DEFAULT (datetime('now','localtime'))
        )
        """);

    await _ctx.EnsureTableAsync("""
        CREATE TABLE IF NOT EXISTS sensor_config (
            id        INTEGER PRIMARY KEY AUTOINCREMENT,
            plant_cd  TEXT NOT NULL,
            sensor_id INTEGER NOT NULL,
            threshold REAL,
            use_yn    TEXT DEFAULT 'Y',
            UNIQUE(plant_cd, sensor_id)
        )
        """);
}
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
lssLib.DB.Sqlite
  ├── SqliteDbContext.cs      DbContextBase 파생
  ├── SqliteRepository.cs     RepositoryBase<T> 파생
  └── SqliteQueryBuilder.cs   QueryBuilderBase 파생
```

---

## 주의사항

1. **SP 미지원** — `CallSpAsync()` 호출 시 `DbResult.Fail`을 반환합니다. `ExecuteAsync()` / `QueryTableAsync()`를 사용하세요.
2. **단일 Writer 제한** — SQLite는 동시에 하나의 쓰기 연결만 허용합니다. WAL 모드에서는 읽기는 동시 가능합니다.
3. **파일 경로** — 절대 경로 사용을 권장합니다. 상대 경로는 실행 위치에 따라 달라질 수 있습니다.
4. **외래키 기본 OFF** — SQLite는 기본적으로 외래키 제약을 적용하지 않습니다. `SetPragmaAsync("foreign_keys", "ON")` 으로 활성화하세요.
5. **DateTime 저장** — SQLite에는 DateTime 전용 타입이 없습니다. TEXT(`yyyy-MM-dd HH:mm:ss`) 또는 INTEGER(Unix timestamp)로 저장됩니다.
6. **`await using` 사용 권장** — `DisposeAsync()` 자동 호출로 연결이 안전하게 해제됩니다.
7. **WAL 모드 권장** — 대부분의 시나리오에서 `journal_mode = WAL`이 기본 DELETE 모드보다 성능이 우수합니다.
