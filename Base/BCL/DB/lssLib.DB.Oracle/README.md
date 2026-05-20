# lssLib.DB.Oracle

> lssLib.DB Oracle Provider  
> Oracle.ManagedDataAccess.Core 기반 Oracle DB 접근 모듈  
> OracleDB.CallProc / OracleHelper 패턴 현대화 계승

---

## Ver History

| 버전 | 날짜 | 내용 |
|---|---|---|
| v1.0.0 | 2025-05-19 | 최초 작성 — OracleDbContext / OracleRepository / OracleQueryBuilder |
| v1.0.1 | 2025-05-19 | GetOutParam CS8780 수정 / OracleRepository._rowMapper CS0649·CS8618 수정 |

---

## 개요

```
lssLib.DB.Oracle
├── OracleDbContext.cs      연결·쿼리·SP·SpMutiSave 구현
├── OracleRepository.cs     RowMapper 기반 CRUD + Oracle 전용 SP 확장 API
└── OracleQueryBuilder.cs   :ParamName / ROWNUM LIMIT 문법 쿼리 빌더
```

### 원본 대비 주요 변경

| 원본 | lssLib.DB.Oracle | 변경 이유 |
|---|---|---|
| `Oracle.DataAccess.Client` (구버전 ODP.NET) | `Oracle.ManagedDataAccess.Core` | .NET 8 지원 |
| `static connStr / oraConn` (전역 상태) | 인스턴스 기반 DbContextBase | 스레드 안전·생명주기 관리 |
| `OracleDataAdapter.Fill(DataSet)` | `ExecuteReader + DataTable.Load()` | IDbDataAdapter 의존성 제거 |
| `CallProc(spName, inData)` | `CallSpAsync(spName, params)` | `DbResult<SpResult>` 반환 |
| `CallProc1(spName, pram...)` | `CallSpArgsAsync(spName, ct, args)` | 가변인수 유지 |
| `Sp_MutiSave(dt, sp, where[], cols[])` | `SpMutiSaveAsync(dt, sp, tuples[], cols[])` | 튜플 배열로 타입 안전 |
| 위치 기반 파라미터 바인딩 | `BindByName = true` | 이름 기반 바인딩 (안전) |

---

## 의존성

```xml
<!-- lssLib.DB.Oracle.csproj -->
<ProjectReference Include="..\lssLib.DB\lssLib.DB.csproj" />
<PackageReference Include="Oracle.ManagedDataAccess.Core" Version="23.*" />
```

| 참조 | 용도 |
|---|---|
| `lssLib.DB` (Core) | DbContextBase / RepositoryBase / DbResult / DbParam |
| `lssLib.Log` | LogManager 로그 기록 |
| `Oracle.ManagedDataAccess.Core` | Oracle Managed ODP.NET (.NET 8 지원) |

---

## 빠른 시작

### 1. 설정 생성

```csharp
var cfg = new RelationalDbConfig(
    DbProviderType.Oracle,
    "Data Source=MyOracleDB;User Id=scott;Password=tiger;",
    commandTimeoutSec: 180);
```

### 2. Context + Repository 생성

```csharp
await using var ctx = new OracleDbContext(cfg);
await ctx.OpenAsync();

RowMapper<SensorData> mapper = row => new SensorData
{
    SensorId = Convert.ToInt32(row["SENSOR_ID"]),
    Value    = Convert.ToDouble(row["SENSOR_VALUE"]),
    RegDt    = Convert.ToDateTime(row["REG_DT"]),
    PlantCd  = row["PLANT_CD"].ToString() ?? string.Empty,
};

var repo = new OracleRepository<SensorData>(ctx, mapper);
```

### 3. SP 호출 — CallProc 패턴 (표준)

```csharp
// ① SpResult 원형 반환 (OracleDB.CallProc 동일)
DbResult<SpResult> r = await repo.CallSpAsync(
    "SP_SENSOR_GET",
    DbParam.StandardSp("SELECT 'A01','2024-01-01' FROM DUAL"));

if (r.IsOk && r.Value!.IsSuccess)
    MessageBox.Show(r.Value.ReturnMessage);

// ② OUT_CURSOR → List<T> 자동 변환
DbResult<List<SensorData>> list = await repo.CallSpQueryAsync(
    "SP_SENSOR_GET",
    DbParam.StandardSp("SELECT 'A01','2024-01-01' FROM DUAL"));

if (list.IsOk) DgSensor.ItemsSource = list.Value;
```

### 4. SP 호출 — CallProc1 패턴 (가변인수)

```csharp
// ① SpResult 원형 (OracleDB.CallProc1 동일)
DbResult<SpResult> r = await repo.CallSpArgsAsync(
    "SP_SENSOR_GET",
    default,            // CancellationToken
    "A01",              // 1번째 IN_DATA 값
    "2024-01-01",       // 2번째
    "2024-12-31");      // 3번째
// → 내부에서 "SELECT 'A01','2024-01-01','2024-12-31' FROM DUAL" 자동 조립

// ② OUT_CURSOR → List<T> 자동 변환
DbResult<List<SensorData>> list = await repo.CallSpArgsQueryAsync(
    "SP_SENSOR_GET",
    default,
    "A01", "2024-01-01");
```

### 5. SP 저장 — Sp_MutiSave 패턴 (DataTable 일괄)

```csharp
// OracleDB.Sp_MutiSave 동일 패턴
// WHERE 조건에 맞는 행만 SP로 일괄 저장
DbResult<int> r = await repo.SpMutiSaveAsync(
    dt:               sensorDataTable,
    spName:           "SP_SENSOR_SAVE",
    whereConditions:  [("USE_YN", "Y"), ("STATUS", "ACTIVE")],
    paramColumns:     ["SENSOR_ID", "SENSOR_VALUE", "REG_DT"]);

if (r.IsOk)
    LogManager.Instance.Info("DB", $"{r.Value}건 저장 완료");
```

### 6. SQL 직접 실행

```csharp
// 목록 조회
DbResult<List<SensorData>> r = await repo.QueryAsync(
    "SELECT * FROM SENSOR_DATA WHERE PLANT_CD = :P1 AND USE_YN = 'Y'",
    [DbParam.In(":P1", "A01")]);

// 단건 조회
DbResult<SensorData?> r2 = await repo.QuerySingleAsync(
    "SELECT * FROM SENSOR_DATA WHERE SENSOR_ID = :ID",
    [DbParam.In(":ID", 42)]);

// 스칼라 조회
DbResult<int?> r3 = await repo.QueryScalarAsync<int>(
    "SELECT COUNT(*) FROM SENSOR_DATA WHERE USE_YN = 'Y'");

// DML 실행
DbResult<int> r4 = await repo.ExecuteAsync(
    "UPDATE SENSOR_DATA SET USE_YN='N' WHERE SENSOR_ID = :ID",
    [DbParam.In(":ID", 42)]);
```

### 7. 쿼리 빌더

```csharp
var qb = new OracleQueryBuilder();

// SELECT + ROWNUM
var (sql, ps) = qb
    .From("SENSOR_DATA")
    .Select("SENSOR_ID", "SENSOR_VALUE", "REG_DT")
    .Where("PLANT_CD", QueryOp.Eq,   "A01")
    .Where("REG_DT",   QueryOp.GtEq, DateTime.Today)
    .OrderBy("REG_DT", false)
    .Limit(100)
    .Build();
// → SELECT SENSOR_ID, SENSOR_VALUE, REG_DT
//   FROM SENSOR_DATA
//   WHERE PLANT_CD = :p0 AND REG_DT >= :p1 AND ROWNUM <= 100
//   ORDER BY REG_DT DESC

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

### 8. 트랜잭션

```csharp
await ctx.BeginTransactionAsync();
try
{
    await repo.ExecuteAsync(
        "UPDATE SENSOR_DATA SET USE_YN='N' WHERE SENSOR_ID = :ID",
        [DbParam.In(":ID", 42)]);

    await repo.ExecuteAsync(
        "INSERT INTO SENSOR_LOG (SENSOR_ID, LOG_MSG) VALUES (:ID, :MSG)",
        [DbParam.In(":ID", 42), DbParam.In(":MSG", "비활성화")]);

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

### OracleDbContext

| 메서드 | 반환 | 설명 |
|---|---|---|
| `OpenAsync(ct)` | `Task` | Oracle 연결 (비동기) |
| `CloseAsync()` | `Task` | 연결 해제 |
| `BeginTransactionAsync(ct)` | `Task` | 트랜잭션 시작 |
| `CommitAsync()` | `Task` | 트랜잭션 커밋 |
| `RollbackAsync()` | `Task` | 트랜잭션 롤백 |
| `ExecuteAsync(sql, type, ps, ct)` | `Task<DbResult<int>>` | DML 실행 |
| `QueryTableAsync(sql, type, ps, ct)` | `Task<DbResult<DataTable>>` | SELECT → DataTable |
| `CallSpAsync(spName, ps, ct)` | `Task<DbResult<SpResult>>` | SP 실행 (표준 패턴) |
| `CallSpArgsAsync(spName, ct, args)` | `Task<DbResult<SpResult>>` | SP 가변인수 호출 |
| `SpMutiSaveAsync(dt, sp, where, cols, ct)` | `Task<DbResult<int>>` | DataTable 일괄 저장 |

### OracleRepository\<T\>

`RepositoryBase<T>` 전체 메서드 + 아래 추가

| 메서드 | 설명 |
|---|---|
| `CallSpArgsAsync(spName, ct, args)` | 가변인수 SP 호출 → SpResult |
| `CallSpArgsQueryAsync(spName, ct, args)` | 가변인수 SP 호출 → List\<T\> |
| `SpMutiSaveAsync(dt, sp, where, cols, ct)` | DataTable 조건 필터 후 SP 일괄 저장 |

### OracleQueryBuilder

| 항목 | 값 |
|---|---|
| 파라미터 접두사 | `:` |
| LIMIT 구현 | `AND ROWNUM <= N` (WHERE 절 후행) |
| SELECT 접두어 | `SELECT` (TOP N 없음) |

---

## DbParamType → OracleDbType 매핑

| DbParamType | OracleDbType |
|---|---|
| VarChar | Varchar2 |
| Char | Char |
| Text | Clob |
| TinyInt | Byte |
| SmallInt | Int16 |
| Int | Int32 |
| BigInt | Int64 |
| Float | Single |
| Double | Double |
| Decimal | Decimal |
| Date | Date |
| DateTime | TimeStamp |
| DateTimeOffset | TimeStampTZ |
| Boolean | Byte (Oracle Boolean 없음) |
| Guid | Raw |
| Binary | Blob |
| Cursor | RefCursor (OUT_CURSOR 전용) |
| Auto | Varchar2 (기본값) |

---

## DbParam.StandardSp 표준 패턴

Oracle SP 표준 파라미터 4개를 자동 생성합니다.

```csharp
// 내부 생성 결과
DbParam[] ps = DbParam.StandardSp("SELECT 'A01' FROM DUAL");
// →
// DbParam.In ("IN_DATA",        "SELECT 'A01' FROM DUAL", VarChar)
// DbParam.Out("OUT_RETURNCODE", VarChar, size: 10)
// DbParam.Out("OUT_RETURNMSG",  VarChar, size: 200)
// DbParam.Out("OUT_CURSOR",     Cursor)
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
lssLib.DB.Oracle
  ├── OracleDbContext.cs      DbContextBase 파생
  ├── OracleRepository.cs     RepositoryBase<T> 파생
  └── OracleQueryBuilder.cs   QueryBuilderBase 파생
```

---

## 주의사항

1. **Managed ODP.NET 전용** — `Oracle.DataAccess.Client` (구버전 ODP.NET) 와 혼용 불가합니다.
2. **BindByName = true** — 파라미터를 이름으로 바인딩합니다. SP 파라미터 순서가 달라도 안전합니다.
3. **Oracle Boolean 없음** — `DbParamType.Boolean` → `OracleDbType.Byte` (0/1)로 매핑됩니다.
4. **ROWNUM 주의** — `ORDER BY` 이후에 ROWNUM 필터 적용 시 서브쿼리가 필요합니다. 복잡한 페이징은 SQL 직접 작성을 권장합니다.
5. **SP IN_DATA 형식** — `SELECT '값1','값2' FROM DUAL` 형식으로 전달합니다. `DbParam.StandardSp()` 활용을 권장합니다.
6. **`await using` 사용 권장** — `DisposeAsync()` 자동 호출로 연결이 안전하게 해제됩니다.
7. **CommandTimeout 기본값** — Oracle 쿼리는 시간이 걸릴 수 있으므로 기본값 `180초`를 권장합니다.
