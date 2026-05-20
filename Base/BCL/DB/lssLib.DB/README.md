# lssLib.DB

> lssLib DB 접근 추상화 Core 모듈  
> BCL + lssLib.Log 전용 — DB 벤더 독립 기반 계층

---

## Ver History

| 버전 | 날짜 | 내용 |
|---|---|---|
| v1.0.0 | 2025-05-19 | 최초 작성 — Core / Contracts / Abstractions / Helpers 전체 구성 |

---

## 전체 솔루션 구성

```
lssLib.DB.sln
│
├── lssLib.Log                       공통 로그 의존
├── lssLib.DB                        Core ← 이 모듈
├── lssLib.DB.InfluxDB               InfluxDB v2.0 Provider
├── lssLib.DB.MsSql                  MSSQL Provider
├── lssLib.DB.Oracle                 Oracle Provider
├── lssLib.DB.MySql                  MySQL / MariaDB Provider
└── lssLib.DB.Sqlite                 SQLite Provider
```

### 의존성 방향

```
lssLib.Log
    ▲
    │
lssLib.DB (Core — BCL only)
    ▲
    ├── lssLib.DB.InfluxDB    ← InfluxDB.Client
    ├── lssLib.DB.MsSql       ← Microsoft.Data.SqlClient
    ├── lssLib.DB.Oracle      ← Oracle.ManagedDataAccess.Core
    ├── lssLib.DB.MySql       ← MySqlConnector
    └── lssLib.DB.Sqlite      ← Microsoft.Data.Sqlite
```

---

## 모듈 내부 구조

```
lssLib.DB/
├── lssLib.DB.csproj
│
├── Core/                        공통 값 타입
│   ├── DbResult.cs              실행 결과 (DbResult<T> / SpResult)
│   ├── DbParam.cs               파라미터 래퍼 (DbParam / DbParamType)
│   ├── DbConfig.cs              연결 설정 (DbConfigBase / RelationalDbConfig / InfluxDbConfig)
│   └── DbException.cs           전용 예외 (DbException / DbErrorCode)
│
├── Contracts/                   인터페이스 계층
│   ├── IDbContext.cs            연결·트랜잭션·실행 생명주기
│   ├── IRepository.cs           Generic CRUD 계약 + RowMapper 델리게이트
│   └── IQueryBuilder.cs         코드 기반 쿼리 빌더 계약 + QueryOp 열거형
│
├── Abstractions/                추상 클래스 계층
│   ├── DbContextBase.cs         IDbContext 공통 구현 (연결·재시도·로그)
│   ├── RepositoryBase.cs        IRepository 공통 구현 (CRUD·RowMapper·Batch)
│   └── QueryBuilderBase.cs      IQueryBuilder 공통 구현 (WHERE·ORDER·파라미터 누적)
│
└── Helpers/
    └── DbHelper.cs              OracleHelper + OracleDB 패턴 범용화 정적 헬퍼
```

---

## Core 타입 상세

### DbResult\<T\>

DB 실행 결과를 담는 범용 값 타입입니다.

```csharp
// 팩토리 메서드
DbResult<T>.Ok(value, elapsedMs)      // 성공
DbResult<T>.Fail(message, elapsedMs)  // 실패
DbResult<T>.Error(exception)          // 예외
DbResult<T>.Timeout(message)          // 타임아웃

// 주요 프로퍼티
bool            IsOk       // 성공 여부
DbResultStatus  Status     // Ok / Fail / Error / Timeout
T?              Value      // 결과 데이터
string          Message    // 결과 메시지
Exception?      Exception  // 발생 예외
long            ElapsedMs  // 실행 소요 시간 (ms)
```

**사용 예:**
```csharp
var r = await repo.QueryAsync("SELECT * FROM SENSOR");
if (r.IsOk)
    grid.ItemsSource = r.Value;
else
    LogManager.Instance.Error("DB", r.Message);
```

### SpResult

SP 실행 결과 (IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR 표준 패턴)

```csharp
// 팩토리 메서드
SpResult.Ok(message, table)       // 성공 (ReturnCode = "1")
SpResult.Fail(code, message)      // 실패

// 주요 프로퍼티
string     ReturnCode     // DB 반환 코드 ("1" = 성공)
string     ReturnMessage  // DB 반환 메시지
DataTable? Table          // SELECT 결과 (없으면 null)
bool       IsSuccess      // ReturnCode == "1" 여부
```

### DbParam

DB 파라미터 범용 래퍼입니다.

```csharp
// 팩토리 메서드
DbParam.In("@P1",    value)               // Input 파라미터
DbParam.Out("@OUT",  DbParamType.VarChar) // Output 파라미터
DbParam.InOut("@IO", value)               // InputOutput 파라미터

// SP 표준 4개 파라미터 자동 생성
DbParam[] ps = DbParam.StandardSp("SELECT 'A01' FROM DUAL");
// → IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR
```

### DbConfig

```csharp
// 관계형 DB (MSSQL / Oracle / MySQL / SQLite)
var cfg = new RelationalDbConfig(
    DbProviderType.MsSql,
    "Server=localhost;Database=IIoT;Integrated Security=true;",
    commandTimeoutSec: 30);

// InfluxDB v2.0
var cfg2 = new InfluxDbConfig(
    url:    "http://localhost:8086",
    token:  "my-token",
    org:    "my-org",
    bucket: "sensor-data");
```

### DbException / DbErrorCode

```csharp
// 자주 쓰는 팩토리
DbException.ConnectionFailed(provider, innerEx)
DbException.CommandTimeout(provider, sql)
DbException.SpReturnError(provider, spName, code, msg)
DbException.InvalidParameter(provider, paramName, reason)
DbException.LineProtocolError(detail)  // InfluxDB 전용

// 패턴 매칭 활용
catch (DbException ex) when (ex.ErrorCode == DbErrorCode.DuplicateKey)
{
    // 중복 키 처리
}
```

---

## Contracts 인터페이스 상세

### IDbContext

```csharp
// 연결
await ctx.OpenAsync(ct);
await ctx.CloseAsync();

// 트랜잭션
await ctx.BeginTransactionAsync(ct);
await ctx.CommitAsync();
await ctx.RollbackAsync();

// 실행
Task<DbResult<int>>       ExecuteAsync(sql, type, ps, ct)
Task<DbResult<DataTable>> QueryTableAsync(sql, type, ps, ct)
Task<DbResult<SpResult>>  CallSpAsync(spName, ps, ct)

// 상태
ConnectionState State
bool            IsInTransaction
DbProviderType  ProviderType
```

### IRepository\<T\>

```csharp
// 조회
Task<DbResult<List<T>>>  QueryAsync(sql, ps, ct)
Task<DbResult<T?>>       QuerySingleAsync(sql, ps, ct)
Task<DbResult<TScalar?>> QueryScalarAsync<TScalar>(sql, ps, ct)

// 실행
Task<DbResult<int>>      ExecuteAsync(sql, ps, ct)

// SP 실행
Task<DbResult<SpResult>> CallSpAsync(spName, ps, ct)
Task<DbResult<List<T>>>  CallSpQueryAsync(spName, ps, ct)

// 배치 트랜잭션
Task<DbResult<int>>      ExecuteBatchAsync(commands, ct)
```

### IQueryBuilder

```csharp
// SELECT
qb.From("TABLE").Select("COL1","COL2")
  .Where("COL", QueryOp.Eq, value)
  .OrWhere("COL", QueryOp.GtEq, value)
  .WhereIn("COL", values)
  .OrderBy("COL", ascending: true)
  .Limit(100)
  .Build()            // → (sql, DbParam[])

// INSERT / UPDATE / DELETE
qb.Insert("TABLE").Value("COL", val).BuildInsert()
qb.Update("TABLE").Set("COL", val).Where(...).BuildUpdate()
qb.Delete("TABLE").Where(...).BuildDelete()

qb.Reset()            // 빌더 초기화
```

---

## Abstractions 추상 클래스 상세

### DbContextBase

Provider별 구현체에서 **추상 메서드 3~4개만 구현**하면 동작합니다.

```csharp
public sealed class MsSqlDbContext : DbContextBase
{
    // ① 연결 객체 생성 (필수)
    protected override IDbConnection CreateConnection()
        => new SqlConnection(cfg.ConnectionString);

    // ② 비동기 연결 열기 (선택 — 기본: 동기 Open)
    protected override async Task OpenConnectionAsync(IDbConnection conn, CancellationToken ct)
        => await ((SqlConnection)conn).OpenAsync(ct);

    // ③ SQL 실행 (필수)
    protected override Task<DbResult<int>> ExecuteCoreAsync(...)
        => ...;

    // ④ SELECT 실행 (필수)
    protected override Task<DbResult<DataTable>> QueryTableCoreAsync(...)
        => ...;

    // ⑤ SP 실행 (필수)
    protected override Task<DbResult<SpResult>> CallSpCoreAsync(...)
        => ...;
}
```

**DbContextBase가 자동으로 처리하는 것들:**

| 기능 | 내용 |
|---|---|
| 재시도 | 연결 실패 시 `MaxRetry` 횟수만큼 자동 재시도 |
| 로그 | 모든 실행에 lssLib.Log 자동 기록 |
| 타임아웃 | `OperationCanceledException` → `DbResult.Timeout` 변환 |
| 예외 포장 | 모든 예외 → `DbResult.Error` 변환 |
| 트랜잭션 | `BeginTransaction / Commit / Rollback` 공통 처리 |
| Dispose | `IAsyncDisposable` — `await using` 자동 해제 |

### RepositoryBase\<T\>

```csharp
// RowMapper 주입으로 DataRow → T 자동 변환
RowMapper<SensorData> mapper = row => new SensorData
{
    SensorId = Convert.ToInt32(row["SENSOR_ID"]),
    Value    = Convert.ToDouble(row["SENSOR_VALUE"]),
};

// Provider별 Repository는 생성자만 구현
public sealed class MsSqlRepository<T> : RepositoryBase<T> where T : class
{
    public MsSqlRepository(MsSqlDbContext ctx, RowMapper<T> mapper)
        : base(ctx, mapper) { }
}
```

### QueryBuilderBase

Provider별 QueryBuilder는 **추상 멤버 3개만 구현**합니다.

```csharp
// MSSQL
protected override string ParamPrefix             => "@";
protected override string BuildSelectPrefix(int? n) => n.HasValue ? $"SELECT TOP {n}" : "SELECT";
protected override string BuildLimitClause(int n)   => string.Empty;

// Oracle
protected override string ParamPrefix             => ":";
protected override string BuildSelectPrefix(int? n) => "SELECT";
protected override string BuildLimitClause(int n)   => $"AND ROWNUM <= {n}";

// MySQL / SQLite
protected override string ParamPrefix             => "@";
protected override string BuildSelectPrefix(int? n) => "SELECT";
protected override string BuildLimitClause(int n)   => $"LIMIT {n}";
```

---

## DbHelper 정적 헬퍼

`IDbConnection` 기반 레거시 호환 헬퍼입니다.
신규 코드에서는 `IDbContext / IRepository` 계층 사용을 권장합니다.

```csharp
// OracleHelper.ExecuteNonQuery 범용화
int affected = DbHelper.ExecuteNonQuery(conn, CommandType.Text, sql, ps);

// OracleHelper.ExecuteDataset 범용화
DataSet ds = DbHelper.ExecuteDataset(conn, CommandType.Text, sql, ps);

// OracleDB.CallProc 범용화
DbResult<SpResult> r = DbHelper.CallSp(conn, "SP_NAME",
    DbParam.StandardSp("SELECT 'A01' FROM DUAL"));

// OracleDB.CallProc1 가변인수 범용화
DbResult<SpResult> r2 = DbHelper.CallSpArgs(conn, "SP_NAME", "A01", "2024-01-01");

// DataTable 필터링 (OracleDB.DataSearch 범용화)
DataTable filtered = DbHelper.DataSearch(dt, "USE_YN = 'Y'");

// 로그 추가 핸들러 등록
DbHelper.ExtraErrorHandler = (src, msg) =>
    Dispatcher.Invoke(() => TxtStatus.Text = $"[{src}] {msg}");
```

---

## 전체 사용 흐름

```
① 설정 생성
   RelationalDbConfig / InfluxDbConfig

② Context 생성 + 연결
   new MsSqlDbContext(cfg)
   await ctx.OpenAsync()

③ Repository 생성
   RowMapper<T> 정의
   new MsSqlRepository<T>(ctx, mapper)

④ 쿼리 / 실행
   repo.QueryAsync()
   repo.CallSpQueryAsync()
   repo.ExecuteAsync()
   repo.ExecuteBatchAsync()

⑤ 결과 처리
   if (r.IsOk) grid.ItemsSource = r.Value
   else LogManager.Instance.Error(r.Message)

⑥ 자동 해제
   await using → DisposeAsync() 자동 호출
```

---

## Provider별 빠른 비교

| 항목 | InfluxDB | MSSQL | Oracle | MySQL | SQLite |
|---|---|---|---|---|---|
| 연결 방식 | HTTP REST | ADO.NET | ADO.NET | ADO.NET | 파일 |
| 쿼리 언어 | Flux | SQL | SQL | SQL | SQL |
| 파라미터 접두사 | 없음 | `@` | `:` | `@` | `@` |
| LIMIT 방식 | `\|> limit(n:N)` | `SELECT TOP N` | `ROWNUM <= N` | `LIMIT N` | `LIMIT N` |
| SP 지원 | ❌ | ✅ | ✅ | ✅ | ❌ |
| 트랜잭션 | ❌ | ✅ | ✅ | ✅ | ✅ |
| 전용 기능 | Line Protocol / Bucket | BulkInsert | CallProc1 / SpMutiSave | — | Upsert / PRAGMA |
| NuGet | InfluxDB.Client | Microsoft.Data.SqlClient | Oracle.ManagedDataAccess.Core | MySqlConnector | Microsoft.Data.Sqlite |

---

## 주의사항

1. **BCL only** — lssLib.DB Core는 외부 NuGet 패키지를 참조하지 않습니다. Provider별 패키지는 각 Provider 프로젝트에서만 참조합니다.
2. **`await using` 사용 권장** — `DbContextBase`는 `IAsyncDisposable`을 구현합니다. `await using` 으로 연결 해제를 보장하세요.
3. **RowMapper null 안전** — `DataRow` 값이 `DBNull`일 수 있습니다. `Convert.ToXxx` 또는 null 조건 연산자를 사용하세요.
4. **SP 표준 패턴** — `DbParam.StandardSp()` 는 `IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR` 4개를 자동 생성합니다.
5. **QueryBuilder ROWNUM 주의** — Oracle에서 `ORDER BY + LIMIT` 조합 시 서브쿼리가 필요한 경우 SQL 직접 작성을 권장합니다.
