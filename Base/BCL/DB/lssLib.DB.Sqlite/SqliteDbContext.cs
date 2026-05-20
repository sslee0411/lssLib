// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.Sqlite · SqliteDbContext.cs
//  역할: SQLite DbContext 구현
//        파일 기반 경량 DB — 로컬 설정/로그/캐시 저장에 적합
//
//  SQLite 특성:
//    ① 파일 1개 = DB 1개 (ConnectionString = 파일 경로)
//    ② Stored Procedure 없음 → CallSpAsync 미지원
//    ③ 트랜잭션 지원 (단, 동시 쓰기는 단일 Writer 제한)
//    ④ 파라미터 접두사: $ 또는 @ 또는 : (모두 동작)
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using Microsoft.Data.Sqlite;
using lssLib.DB.Abstractions;
using lssLib.DB.Core;
using System.Transactions;
using System.IO;

namespace lssLib.DB.Sqlite;

/// <summary>
/// SQLite DbContext 구현.
/// 파일 기반 경량 DB로 로컬 설정·로그·캐시 저장에 적합하다.
/// </summary>
/// <example><code>
/// // 파일 경로를 ConnectionString으로 사용
/// var cfg = new RelationalDbConfig(
///     DbProviderType.Sqlite,
///     "Data Source=D:\\IIoT\\config.db",
///     commandTimeoutSec: 30);
///
/// await using var ctx = new SqliteDbContext(cfg);
/// await ctx.OpenAsync();
///
/// // 테이블 생성 (없을 경우)
/// await ctx.EnsureTableAsync("""
///     CREATE TABLE IF NOT EXISTS sensor_config (
///         id         INTEGER PRIMARY KEY AUTOINCREMENT,
///         plant_cd   TEXT NOT NULL,
///         sensor_id  INTEGER NOT NULL,
///         threshold  REAL,
///         reg_dt     TEXT DEFAULT (datetime('now','localtime'))
///     )
///     """);
///
/// // 데이터 삽입
/// await ctx.ExecuteAsync(
///     "INSERT INTO sensor_config (plant_cd, sensor_id, threshold) VALUES (@P1, @P2, @P3)",
///     parameters: [
///         DbParam.In("@P1", "A01"),
///         DbParam.In("@P2", 42),
///         DbParam.In("@P3", 80.0),
///     ]);
/// </code></example>
public sealed class SqliteDbContext : DbContextBase
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly RelationalDbConfig _cfg;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="config">SQLite 연결 설정.</param>
    /// <exception cref="DbConfigException">설정 유효성 검사 실패 시.</exception>
    public SqliteDbContext(RelationalDbConfig config) : base(config)
    {
        if (config.ProviderType != DbProviderType.Sqlite)
            throw new DbConfigException(config.ProviderType,
                "SqliteDbContext는 DbProviderType.Sqlite 설정만 허용합니다.");
        _cfg = config;
    }

    // §3 ─ DbContextBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override DbProviderType ProviderType => DbProviderType.Sqlite;

    /// <inheritdoc/>
    protected override IDbConnection CreateConnection()
        => new SqliteConnection(_cfg.ConnectionString);

    /// <inheritdoc/>
    protected override async Task OpenConnectionAsync(
        IDbConnection conn, CancellationToken ct)
    {
        if (conn is SqliteConnection sqliteConn)
            await sqliteConn.OpenAsync(ct).ConfigureAwait(false);
        else
            conn.Open();
    }

    // §4 ─ Execute (INSERT / UPDATE / DELETE)
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override async Task<DbResult<int>> ExecuteCoreAsync(
        string sql,
        CommandType commandType,
        DbParam[]? parameters,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var cmd = BuildCommand(sql, commandType, parameters);
            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            sw.Stop();
            return DbResult<int>.Ok(affected, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return DbResult<int>.Timeout("쿼리 취소됨", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DbResult<int>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §5 ─ QueryTable (SELECT → DataTable)
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override async Task<DbResult<DataTable>> QueryTableCoreAsync(
        string sql,
        CommandType commandType,
        DbParam[]? parameters,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var cmd = BuildCommand(sql, commandType, parameters);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var dt = new DataTable();
            dt.Load(reader);

            sw.Stop();
            return DbResult<DataTable>.Ok(dt, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return DbResult<DataTable>.Timeout("쿼리 취소됨", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DbResult<DataTable>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §6 ─ CallSp — SQLite 미지원 명시
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>SQLite는 Stored Procedure를 지원하지 않습니다.</remarks>
    protected override Task<DbResult<SpResult>> CallSpCoreAsync(
        string spName,
        DbParam[]? parameters,
        CancellationToken ct)
        => Task.FromResult(DbResult<SpResult>.Fail(
            "SQLite는 Stored Procedure를 지원하지 않습니다. ExecuteAsync() / QueryTableAsync()를 사용하세요."));

    // §7 ─ SQLite 전용 — 테이블 초기화
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 테이블이 없으면 생성합니다 (CREATE TABLE IF NOT EXISTS).
    /// 앱 시작 시 스키마 초기화에 사용한다.
    /// </summary>
    /// <param name="createTableSql">CREATE TABLE IF NOT EXISTS ... DDL 문.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>실행 결과를 담은 DbResult.</returns>
    /// <example><code>
    /// await ctx.EnsureTableAsync("""
    ///     CREATE TABLE IF NOT EXISTS app_config (
    ///         key   TEXT PRIMARY KEY,
    ///         value TEXT NOT NULL,
    ///         reg_dt TEXT DEFAULT (datetime('now','localtime'))
    ///     )
    ///     """);
    /// </code></example>
    public Task<DbResult<int>> EnsureTableAsync(
        string createTableSql,
        CancellationToken ct = default)
        => ExecuteAsync(createTableSql, CommandType.Text, null, ct);

    /// <summary>
    /// 여러 테이블을 한 트랜잭션으로 초기화합니다.
    /// </summary>
    /// <param name="createTableSqls">DDL 문 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    public async Task<DbResult<int>> EnsureTablesAsync(
        IEnumerable<string> createTableSqls,
        CancellationToken ct = default)
    {
        var commands = createTableSqls
            .Select(sql => (sql, (DbParam[]?)null));
        return await ExecuteAsync(
            string.Join(";", createTableSqls),
            CommandType.Text, null, ct).ConfigureAwait(false);
    }

    // §8 ─ SQLite 전용 — PRAGMA 설정
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite PRAGMA를 설정합니다.
    /// WAL 모드 / 외래키 / 캐시 크기 등 DB 동작 제어에 사용한다.
    /// </summary>
    /// <param name="pragma">PRAGMA 이름 (예: "journal_mode", "foreign_keys").</param>
    /// <param name="value">설정 값 (예: "WAL", "ON").</param>
    /// <param name="ct">취소 토큰.</param>
    /// <example><code>
    /// // WAL 모드 활성화 (동시 읽기 성능 향상)
    /// await ctx.SetPragmaAsync("journal_mode", "WAL");
    ///
    /// // 외래키 제약 활성화
    /// await ctx.SetPragmaAsync("foreign_keys", "ON");
    /// </code></example>
    public Task<DbResult<int>> SetPragmaAsync(
        string pragma,
        string value,
        CancellationToken ct = default)
        => ExecuteAsync($"PRAGMA {pragma} = {value};", CommandType.Text, null, ct);

    /// <summary>
    /// PRAGMA 값을 조회합니다.
    /// </summary>
    public async Task<string> GetPragmaAsync(
        string pragma,
        CancellationToken ct = default)
    {
        var r = await QueryTableAsync(
            $"PRAGMA {pragma};", CommandType.Text, null, ct).ConfigureAwait(false);

        if (!r.IsOk || r.Value!.Rows.Count == 0) return string.Empty;
        return r.Value.Rows[0][0]?.ToString() ?? string.Empty;
    }

    // §9 ─ SQLite 전용 — DB 파일 경로
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite DB 파일 경로를 반환합니다.
    /// ConnectionString에서 Data Source 값을 추출한다.
    /// </summary>
    public string DbFilePath
    {
        get
        {
            var builder = new SqliteConnectionStringBuilder(_cfg.ConnectionString);
            return builder.DataSource;
        }
    }

    /// <summary>DB 파일 존재 여부.</summary>
    public bool DbFileExists => File.Exists(DbFilePath);

    // §10 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// <summary>SqliteCommand 생성 및 파라미터 설정.</summary>
    private SqliteCommand BuildCommand(
        string sql,
        CommandType commandType,
        DbParam[]? parameters)
    {
        var cmd = new SqliteCommand(sql, (SqliteConnection)Connection!)
        {
            CommandType = commandType,
            CommandTimeout = _cfg.CommandTimeoutSec,
        };

        if (Transaction is SqliteTransaction sqliteTx)
            cmd.Transaction = sqliteTx;

        if (parameters is not null)
            AttachParameters(cmd, parameters);

        return cmd;
    }

    /// <summary>
    /// DbParam[] → SqliteParameter[] 변환 후 커맨드에 부착.
    /// SQLite는 타입을 느슨하게 처리하므로 SqliteType.Text가 기본.
    /// </summary>
    private static void AttachParameters(SqliteCommand cmd, DbParam[] parameters)
    {
        foreach (var p in parameters)
        {
            var sp = new SqliteParameter
            {
                ParameterName = p.Name,
                Direction = p.Direction,
                Value = p.Value ?? DBNull.Value,
            };

            // SQLite 타입 매핑 (4가지 기본 타입)
            // TEXT / INTEGER / REAL / BLOB → SQLite 타입 친화적 선택
            sp.SqliteType = p.ParamType switch
            {
                DbParamType.TinyInt or
                DbParamType.SmallInt or
                DbParamType.Int or
                DbParamType.BigInt or
                DbParamType.Boolean => SqliteType.Integer,

                DbParamType.Float or
                DbParamType.Double or
                DbParamType.Decimal => SqliteType.Real,

                DbParamType.Binary => SqliteType.Blob,

                _ => SqliteType.Text,  // 기본: TEXT
            };

            cmd.Parameters.Add(sp);
        }
    }
}