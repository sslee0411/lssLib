// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.Sqlite · SqliteRepository.cs
//  역할: SQLite Repository 구현 — RepositoryBase + Upsert 확장
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Abstractions;
using lssLib.DB.Contracts;
using lssLib.DB.Core;

namespace lssLib.DB.Sqlite;

/// <summary>
/// SQLite Repository 구현.
/// RepositoryBase 공통 CRUD에 INSERT OR REPLACE(Upsert) 간편 API를 추가한다.
/// </summary>
/// <typeparam name="T">엔티티 타입.</typeparam>
/// <example><code>
/// RowMapper<AppConfig> mapper = row => new AppConfig
/// {
///     Key   = row["key"].ToString()!,
///     Value = row["value"].ToString()!,
///     RegDt = row["reg_dt"].ToString()!,
/// };
///
/// var repo = new SqliteRepository<AppConfig>(ctx, mapper);
///
/// // 설정 저장 (Upsert)
/// await repo.UpsertAsync("app_config",
///     columns: [("key", "theme"), ("value", "dark")]);
///
/// // 설정 조회
/// DbResult<List<AppConfig>> r = await repo.QueryAsync(
///     "SELECT * FROM app_config ORDER BY key");
/// </code></example>
public sealed class SqliteRepository<T> : RepositoryBase<T> where T : class
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly SqliteDbContext _sqliteCtx;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="context">SQLite 컨텍스트.</param>
    /// <param name="mapper">DataRow → T 변환 함수.</param>
    public SqliteRepository(SqliteDbContext context, RowMapper<T> mapper)
        : base(context, mapper)
    {
        _sqliteCtx = context;
    }

    // §3 ─ SQLite 전용 — Upsert (INSERT OR REPLACE)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// INSERT OR REPLACE INTO 를 실행합니다.
    /// PRIMARY KEY 중복 시 기존 행을 교체합니다.
    /// </summary>
    /// <param name="tableName">대상 테이블 이름.</param>
    /// <param name="columns">컬럼-값 쌍 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>영향 받은 행 수를 담은 DbResult.</returns>
    /// <example><code>
    /// await repo.UpsertAsync("app_config",
    ///     [("key", "theme"), ("value", "dark")]);
    /// </code></example>
    public Task<DbResult<int>> UpsertAsync(
        string tableName,
        (string Column, object? Value)[] columns,
        CancellationToken ct = default)
    {
        if (columns.Length == 0)
            return Task.FromResult(DbResult<int>.Fail("컬럼이 없습니다."));

        var colPart = string.Join(", ", columns.Select(c => c.Column));
        var paramPart = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var sql = $"INSERT OR REPLACE INTO {tableName} ({colPart}) VALUES ({paramPart})";
        var ps = columns.Select((c, i) => DbParam.In($"@p{i}", c.Value)).ToArray();

        return _sqliteCtx.ExecuteAsync(sql, System.Data.CommandType.Text, ps, ct);
    }

    /// <summary>
    /// INSERT OR IGNORE INTO 를 실행합니다.
    /// PRIMARY KEY 중복 시 무시하고 넘어갑니다.
    /// </summary>
    /// <param name="tableName">대상 테이블 이름.</param>
    /// <param name="columns">컬럼-값 쌍 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    public Task<DbResult<int>> InsertIgnoreAsync(
        string tableName,
        (string Column, object? Value)[] columns,
        CancellationToken ct = default)
    {
        if (columns.Length == 0)
            return Task.FromResult(DbResult<int>.Fail("컬럼이 없습니다."));

        var colPart = string.Join(", ", columns.Select(c => c.Column));
        var paramPart = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var sql = $"INSERT OR IGNORE INTO {tableName} ({colPart}) VALUES ({paramPart})";
        var ps = columns.Select((c, i) => DbParam.In($"@p{i}", c.Value)).ToArray();

        return _sqliteCtx.ExecuteAsync(sql, System.Data.CommandType.Text, ps, ct);
    }
}