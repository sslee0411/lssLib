# lssLib.DB.MsSql

> lssLib.DB MSSQL Provider  
> Microsoft.Data.SqlClient 기반 SQL Server 접근 모듈

---

## Ver History

| 버전 | 날짜 | 내용 |
|---|---|---|
| v1.0.0 | 2025-05-19 | 최초 작성 — MsSqlDbContext / MsSqlRepository / MsSqlQueryBuilder |

---

## 개요

```
lssLib.DB.MsSql
├── MsSqlDbContext.cs      연결·쿼리·SP·BulkInsert 구현
├── MsSqlRepository.cs     RowMapper 기반 CRUD + BulkInsert 간편 API
└── MsSqlQueryBuilder.cs   TOP N / @Param MSSQL 문법 쿼리 빌더
```

---

## 의존성

```xml
<!-- lssLib.DB.MsSql.csproj -->
<ProjectReference Include="..\lssLib.DB\lssLib.DB.csproj" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" />
```

| 참조 | 용도 |
|---|---|
| `lssLib.DB` (Core) | DbContextBase / RepositoryBase / DbResult / DbParam |
| `lssLib.Log` | LogManager 로그 기록 |
| `Microsoft.Data.SqlClient` | SQL Server 공식 드라이버 |

---

## 빠른 시작

### 1. 설정 생성

```csharp
var cfg = new RelationalDbConfig(
    DbProviderType.MsSql,
    "Server=localhost;Database=IIoT;Integrated Security=true;",
    commandTimeoutSec: 30);
```

### 2. Context + Repository 생성

```csharp
await using var ctx = new MsSqlDbContext(cfg);
await ctx.OpenAsync();

RowMapper<SensorData> mapper = row => new SensorData
{
    SensorId = Convert.ToInt32(row["SENSOR_ID"]),
    Value    = Convert.ToDouble(row["SENSOR_VALUE"]),
    RegDt    = Convert.ToDateTime(row["REG_DT"]),
};

var repo = new MsSqlRepository<SensorData>(ctx, mapper);
```

### 3. SQL 조회

```csharp
// 목록 조회
DbResult<List<SensorData>> r = await repo.QueryAsync(
    "SELECT * FROM SENSOR_DATA WHERE PLANT_CD = @P1 AND USE_YN = 'Y'",
    [DbParam.In("@P1", "A01")]);

if (r.IsOk) DgSensor.ItemsSource = r.Value;

// 단건 조회
DbResult<SensorData?> r2 = await repo.QuerySingleAsync(
    "SELECT * FROM SENSOR_DATA WHERE SENSOR_ID = @ID",
    [DbParam.In("@ID", 42)]);

// 스칼라 조회
DbResult<int?> r3 = await repo.QueryScalarAsync<int>(
    "SELECT COUNT(*) FROM SENSOR_DATA WHERE USE_YN = 'Y'");
```

### 4. SP 호출 (표준 패턴)

```csharp
// SpResult 원형 반환
DbResult<SpResult> sp = await repo.CallSpAsync(
    "SP_SENSOR_SAVE",
    DbParam.StandardSp("SELECT '001','99.5','2024-01-01' FROM DUAL"));

if (sp.IsOk && sp.Value!.IsSuccess)
    MessageBox.Show(sp.Value.ReturnMessage);

// 엔티티 목록 자동 변환 (OUT_CURSOR → List<SensorData>)
DbResult<List<SensorData>> list = await repo.CallSpQueryAsync(
    "SP_SENSOR_GET",
    DbParam.StandardSp("SELECT 'A01','2024-01-01' FROM DUAL"));

if (list.IsOk) DgSensor.ItemsSource = list.Value;
```

### 5. BulkInsert

```csharp
// DataTable 직접 전달
var dt = new DataTable();
dt.Columns.Add("SENSOR_ID",    typeof(int));
dt.Columns.Add("SENSOR_VALUE", typeof(double));
dt.Columns.Add("REG_DT",       typeof(DateTime));
foreach (var s in sensors)
    dt.Rows.Add(s.Id, s.Value, s.RegDt);

DbResult<int> br = await repo.BulkInsertAsync("SENSOR_DATA", dt, batchSize: 500);

// 엔티티 목록 직접 전달 (DataTable 자동 생성)
DbResult<int> br2 = await repo.BulkInsertAsync(
    destinationTable: "SENSOR_DATA",
    entities: sensors,
    toRow: (s, row) =>
    {
        row["SENSOR_ID"]    = s.Id;
        row["SENSOR_VALUE"] = s.Value;
        row["REG_DT"]       = s.RegDt;
    },
    columns:
    [
        ("SENSOR_ID",    typeof(int)),
        ("SENSOR_VALUE", typeof(double)),
        ("REG_DT",       typeof(DateTime)),
    ]);
```

### 6. 쿼리 빌더

```csharp
var qb = new MsSqlQueryBuilder();

// SELECT TOP 100
var (sql, ps) = qb
    .From("SENSOR_DATA")
    .Select("SENSOR_ID", "SENSOR_VALUE", "REG_DT")
    .Where("PLANT_CD", QueryOp.Eq,   "A01")
    .Where("REG_DT",   QueryOp.GtEq, DateTime.Today)
    .OrderBy("REG_DT", false)
    .Limit(100)
    .Build();

var r = await repo.QueryAsync(sql, ps);

// INSERT
var (sql2, ps2) = qb.Reset()
    .Insert("SENSOR_DATA")
    .Value("SENSOR_ID",    42)
    .Value("SENSOR_VALUE", 99.5)
    .Value("REG_DT",       DateTime.Now)
    .BuildInsert();

await repo.ExecuteAsync(sql2, ps2);
```

### 7. 트랜잭션

```csharp
await ctx.BeginTransactionAsync();
try
{
    await repo.ExecuteAsync(
        "UPDATE SENSOR_DATA SET USE_YN='N' WHERE SENSOR_ID=@ID",
        [DbParam.In("@ID", 42)]);

    await repo.ExecuteAsync(
        "INSERT INTO SENSOR_LOG (SENSOR_ID, LOG_MSG) VALUES (@ID, @MSG)",
        [DbParam.In("@ID", 42), DbParam.In("@MSG", "비활성화")]);

    await ctx.CommitAsync();
}
catch
{
    await ctx.RollbackAsync();
    throw;
}
```

---

## API 레퍼런스

### MsSqlDbContext

| 메서드 | 반환 | 설명 |
|---|---|---|
| `OpenAsync(ct)` | `Task` | SQL Server 연결 (비동기) |
| `CloseAsync()` | `Task` | 연결 해제 |
| `BeginTransactionAsync(ct)` | `Task` | 트랜잭션 시작 |
| `CommitAsync()` | `Task` | 트랜잭션 커밋 |
| `RollbackAsync()` | `Task` | 트랜잭션 롤백 |
| `ExecuteAsync(sql, type, ps, ct)` | `Task<DbResult<int>>` | DML 실행 |
| `QueryTableAsync(sql, type, ps, ct)` | `Task<DbResult<DataTable>>` | SELECT → DataTable |
| `CallSpAsync(spName, ps, ct)` | `Task<DbResult<SpResult>>` | SP 실행 |
| `BulkInsertAsync(table, dt, batch, ct)` | `Task<DbResult<int>>` | 대량 삽입 |

### MsSqlRepository\<T\>

`RepositoryBase<T>` 전체 메서드 + 아래 추가

| 메서드 | 설명 |
|---|---|
| `BulkInsertAsync(table, DataTable, batch, ct)` | DataTable 대량 삽입 |
| `BulkInsertAsync(table, entities, toRow, columns, batch, ct)` | 엔티티 목록 대량 삽입 |

### MsSqlQueryBuilder

| 항목 | 값 |
|---|---|
| 파라미터 접두사 | `@` |
| LIMIT 구현 | `SELECT TOP N` (접두어 방식) |
| 후행 LIMIT 절 | 없음 |

---

## DbParamType → SqlDbType 매핑

| DbParamType | SqlDbType |
|---|---|
| VarChar | NVarChar |
| Char | NChar |
| Text | NText |
| TinyInt | TinyInt |
| SmallInt | SmallInt |
| Int | Int |
| BigInt | BigInt |
| Float / Double | Float |
| Decimal | Decimal |
| Date | Date |
| DateTime | DateTime2 |
| DateTimeOffset | DateTimeOffset |
| Boolean | Bit |
| Guid | UniqueIdentifier |
| Binary | VarBinary |
| Auto | NVarChar (기본값) |

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
lssLib.DB.MsSql
  ├── MsSqlDbContext.cs      DbContextBase 파생
  ├── MsSqlRepository.cs     RepositoryBase<T> 파생
  └── MsSqlQueryBuilder.cs   QueryBuilderBase 파생
```

---

## 주의사항

1. **ConnectionString 형식** — `Microsoft.Data.SqlClient` 5.x 형식 사용 (`Encrypt=False` 기본 해제됨)
2. **BulkInsert 컬럼 매핑** — DataTable 컬럼명과 대상 테이블 컬럼명이 일치해야 합니다.
3. **SP 표준 패턴** — `OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR` 파라미터 필수.
4. **트랜잭션 + BulkInsert** — 동일 컨텍스트에서 `BeginTransactionAsync()` 후 BulkInsert 시 자동으로 트랜잭션에 포함됩니다.
5. **`await using` 사용 권장** — `DisposeAsync()` 자동 호출로 연결이 안전하게 해제됩니다.
