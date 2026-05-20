# lssLib.DB.MySql

> lssLib.DB MySQL / MariaDB Provider  
> MySqlConnector 기반 MySQL / MariaDB 접근 모듈

---

## Ver History

| 버전 | 날짜 | 내용 |
|---|---|---|
| v1.0.0 | 2025-05-19 | 최초 작성 — MySqlDbContext / MySqlRepository / MySqlQueryBuilder |

---

## 개요

```
lssLib.DB.MySql
├── MySqlDbContext.cs      연결·쿼리·SP·트랜잭션 구현
├── MySqlRepository.cs     RowMapper 기반 CRUD
└── MySqlQueryBuilder.cs   @ParamName / LIMIT N MySQL 문법 쿼리 빌더
```

### MySql.Data vs MySqlConnector

| 항목 | MySql.Data (Oracle 공식) | MySqlConnector (채택) |
|---|---|---|
| 비동기 완성도 | 부분 지원 (내부 동기 블로킹) | 완전 비동기 |
| 라이선스 | GPL / Commercial | MIT |
| .NET 8 지원 | ✅ | ✅ |
| 성능 | 보통 | 우수 |

---

## 의존성

```xml
<!-- lssLib.DB.MySql.csproj -->
<ProjectReference Include="..\lssLib.DB\lssLib.DB.csproj" />
<PackageReference Include="MySqlConnector" Version="2.*" />
```

| 참조 | 용도 |
|---|---|
| `lssLib.DB` (Core) | DbContextBase / RepositoryBase / DbResult / DbParam |
| `lssLib.Log` | LogManager 로그 기록 |
| `MySqlConnector` | MySQL / MariaDB 완전 비동기 드라이버 |

---

## 빠른 시작

### 1. 설정 생성

```csharp
var cfg = new RelationalDbConfig(
    DbProviderType.MySql,
    "Server=localhost;Database=IIoT;Uid=root;Pwd=password;",
    commandTimeoutSec: 30);
```

### 2. Context + Repository 생성

```csharp
await using var ctx = new MySqlDbContext(cfg);
await ctx.OpenAsync();

RowMapper<SensorData> mapper = row => new SensorData
{
    SensorId = Convert.ToInt32(row["sensor_id"]),
    Value    = Convert.ToDouble(row["sensor_value"]),
    RegDt    = Convert.ToDateTime(row["reg_dt"]),
    PlantCd  = row["plant_cd"].ToString() ?? string.Empty,
};

var repo = new MySqlRepository<SensorData>(ctx, mapper);
```

### 3. SQL 조회

```csharp
// 목록 조회
DbResult<List<SensorData>> r = await repo.QueryAsync(
    "SELECT * FROM sensor_data WHERE plant_cd = @P1 AND use_yn = 'Y'",
    [DbParam.In("@P1", "A01")]);

if (r.IsOk) DgSensor.ItemsSource = r.Value;

// 단건 조회
DbResult<SensorData?> r2 = await repo.QuerySingleAsync(
    "SELECT * FROM sensor_data WHERE sensor_id = @ID",
    [DbParam.In("@ID", 42)]);

// 스칼라 조회
DbResult<int?> r3 = await repo.QueryScalarAsync<int>(
    "SELECT COUNT(*) FROM sensor_data WHERE use_yn = 'Y'");
```

### 4. SP 호출 (표준 패턴)

```csharp
// SpResult 원형 반환
DbResult<SpResult> sp = await repo.CallSpAsync(
    "SP_SENSOR_SAVE",
    DbParam.StandardSp("SELECT '001','99.5','2024-01-01'"));

if (sp.IsOk && sp.Value!.IsSuccess)
    MessageBox.Show(sp.Value.ReturnMessage);

// OUT_CURSOR → List<T> 자동 변환
DbResult<List<SensorData>> list = await repo.CallSpQueryAsync(
    "SP_SENSOR_GET",
    DbParam.StandardSp("SELECT 'A01','2024-01-01'"));

if (list.IsOk) DgSensor.ItemsSource = list.Value;
```

> **MySQL SP 참고** — Oracle의 `OUT_CURSOR` (RefCursor) 대신  
> MySQL은 SP 내부 `SELECT` 문 결과셋을 직접 반환합니다.  
> `ExecuteReader`로 첫 번째 결과셋을 DataTable로 수신합니다.

### 5. DML 실행

```csharp
// INSERT
DbResult<int> r = await repo.ExecuteAsync(
    "INSERT INTO sensor_data (plant_cd, sensor_id, sensor_value, reg_dt) " +
    "VALUES (@P1, @P2, @P3, NOW())",
    [
        DbParam.In("@P1", "A01"),
        DbParam.In("@P2", 42),
        DbParam.In("@P3", 99.5),
    ]);

// UPDATE
DbResult<int> r2 = await repo.ExecuteAsync(
    "UPDATE sensor_data SET use_yn='N' WHERE sensor_id = @ID",
    [DbParam.In("@ID", 42)]);

// DELETE
DbResult<int> r3 = await repo.ExecuteAsync(
    "DELETE FROM sensor_data WHERE reg_dt < @DT",
    [DbParam.In("@DT", DateTime.Today.AddMonths(-6))]);
```

### 6. 트랜잭션

```csharp
await ctx.BeginTransactionAsync();
try
{
    await repo.ExecuteAsync(
        "UPDATE sensor_data SET use_yn='N' WHERE sensor_id = @ID",
        [DbParam.In("@ID", 42)]);

    await repo.ExecuteAsync(
        "INSERT INTO sensor_log (sensor_id, log_msg) VALUES (@ID, @MSG)",
        [DbParam.In("@ID", 42), DbParam.In("@MSG", "비활성화")]);

    await ctx.CommitAsync();
}
catch
{
    await ctx.RollbackAsync();
    throw;
}
```

### 7. 배치 트랜잭션

```csharp
var commands = new List<(string, DbParam[]?)>
{
    ("UPDATE sensor_data SET status='OFF' WHERE sensor_id=@ID",
        [DbParam.In("@ID", 1)]),
    ("INSERT INTO sensor_log (sensor_id, log_msg) VALUES (@ID, @MSG)",
        [DbParam.In("@ID", 1), DbParam.In("@MSG", "상태변경")]),
};

DbResult<int> r = await repo.ExecuteBatchAsync(commands);
// 하나라도 실패 시 전체 자동 롤백
```

### 8. 쿼리 빌더

```csharp
var qb = new MySqlQueryBuilder();

// SELECT + LIMIT
var (sql, ps) = qb
    .From("sensor_data")
    .Select("sensor_id", "sensor_value", "reg_dt")
    .Where("plant_cd",  QueryOp.Eq,   "A01")
    .Where("reg_dt",    QueryOp.GtEq, DateTime.Today)
    .OrderBy("reg_dt",  false)
    .Limit(100)
    .Build();
// → SELECT sensor_id, sensor_value, reg_dt
//   FROM sensor_data
//   WHERE plant_cd = @p0 AND reg_dt >= @p1
//   ORDER BY reg_dt DESC
//   LIMIT 100

var r = await repo.QueryAsync(sql, ps);

// INSERT
var (sql2, ps2) = qb.Reset()
    .Insert("sensor_data")
    .Value("plant_cd",     "A01")
    .Value("sensor_id",    42)
    .Value("sensor_value", 99.5)
    .BuildInsert();

await repo.ExecuteAsync(sql2, ps2);

// UPDATE
var (sql3, ps3) = qb.Reset()
    .Update("sensor_data")
    .Set("sensor_value", 100.0)
    .Set("use_yn",       "Y")
    .Where("sensor_id", QueryOp.Eq, 42)
    .BuildUpdate();

await repo.ExecuteAsync(sql3, ps3);
```

---

## API 레퍼런스

### MySqlDbContext

| 메서드 | 반환 | 설명 |
|---|---|---|
| `OpenAsync(ct)` | `Task` | MySQL 연결 (비동기) |
| `CloseAsync()` | `Task` | 연결 해제 |
| `BeginTransactionAsync(ct)` | `Task` | 트랜잭션 시작 |
| `CommitAsync()` | `Task` | 트랜잭션 커밋 |
| `RollbackAsync()` | `Task` | 트랜잭션 롤백 |
| `ExecuteAsync(sql, type, ps, ct)` | `Task<DbResult<int>>` | DML 실행 |
| `QueryTableAsync(sql, type, ps, ct)` | `Task<DbResult<DataTable>>` | SELECT → DataTable |
| `CallSpAsync(spName, ps, ct)` | `Task<DbResult<SpResult>>` | SP 실행 |

### MySqlRepository\<T\>

`RepositoryBase<T>` 전체 메서드를 그대로 사용합니다.

| 메서드 | 반환 | 설명 |
|---|---|---|
| `QueryAsync(sql, ps, ct)` | `Task<DbResult<List<T>>>` | SQL → 엔티티 목록 |
| `QuerySingleAsync(sql, ps, ct)` | `Task<DbResult<T?>>` | SQL → 단건 엔티티 |
| `QueryScalarAsync<TScalar>(sql, ps, ct)` | `Task<DbResult<TScalar?>>` | SQL → 스칼라 값 |
| `ExecuteAsync(sql, ps, ct)` | `Task<DbResult<int>>` | DML 실행 |
| `CallSpAsync(spName, ps, ct)` | `Task<DbResult<SpResult>>` | SP → SpResult |
| `CallSpQueryAsync(spName, ps, ct)` | `Task<DbResult<List<T>>>` | SP → 엔티티 목록 |
| `ExecuteBatchAsync(commands, ct)` | `Task<DbResult<int>>` | 배치 트랜잭션 실행 |

### MySqlQueryBuilder

| 항목 | 값 |
|---|---|
| 파라미터 접두사 | `@` |
| LIMIT 구현 | `LIMIT N` (후행) |
| SELECT 접두어 | `SELECT` (TOP N 없음) |

---

## DbParamType → MySqlDbType 매핑

| DbParamType | MySqlDbType |
|---|---|
| VarChar | VarChar |
| Char | String |
| Text | LongText |
| TinyInt | Byte |
| SmallInt | Int16 |
| Int | Int32 |
| BigInt | Int64 |
| Float | Float |
| Double | Double |
| Decimal | Decimal |
| Date | Date |
| DateTime | DateTime |
| DateTimeOffset | DateTime |
| Boolean | Bit |
| Guid | Guid |
| Binary | Blob |
| Auto | VarChar (기본값) |

---

## ConnectionString 예제

```
# 기본
Server=localhost;Database=IIoT;Uid=root;Pwd=password;

# 포트 지정
Server=192.168.1.100;Port=3306;Database=IIoT;Uid=app_user;Pwd=password;

# SSL 비활성화 (내부망)
Server=localhost;Database=IIoT;Uid=root;Pwd=password;SslMode=None;

# 연결 풀 설정
Server=localhost;Database=IIoT;Uid=root;Pwd=password;
MinimumPoolSize=5;MaximumPoolSize=20;ConnectionTimeout=15;

# MariaDB (동일 드라이버 사용)
Server=localhost;Database=IIoT;Uid=root;Pwd=password;
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
lssLib.DB.MySql
  ├── MySqlDbContext.cs      DbContextBase 파생
  ├── MySqlRepository.cs     RepositoryBase<T> 파생
  └── MySqlQueryBuilder.cs   QueryBuilderBase 파생
```

---

## 주의사항

1. **테이블·컬럼명 대소문자** — Linux MySQL은 기본적으로 대소문자를 구분합니다. `lower_case_table_names` 설정을 확인하세요.
2. **MySQL SP OUT 파라미터** — Oracle RefCursor와 달리 MySQL은 SP 내부 `SELECT` 결과셋을 직접 반환합니다. `OUT_CURSOR` 파라미터 대신 첫 번째 결과셋을 사용합니다.
3. **Boolean → Bit** — MySQL `TINYINT(1)` 컬럼에 `DbParamType.Boolean`을 매핑할 수 있습니다.
4. **`await using` 사용 권장** — `DisposeAsync()` 자동 호출로 연결이 안전하게 해제됩니다.
5. **MariaDB 호환** — MySqlConnector는 MariaDB와 완전 호환됩니다. ConnectionString만 변경하면 동작합니다.
6. **Guid 지원** — `MySqlDbType.Guid` 사용 시 MySQL BINARY(16) 또는 CHAR(36) 컬럼과 매핑됩니다.
