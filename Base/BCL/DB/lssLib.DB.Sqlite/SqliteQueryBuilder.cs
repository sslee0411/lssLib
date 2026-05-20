// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.Sqlite · SqliteQueryBuilder.cs
//  역할: SQLite 쿼리 빌더 — @ParamName / LIMIT N 문법 구현
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Abstractions;

namespace lssLib.DB.Sqlite;

/// <summary>
/// SQLite 쿼리 빌더.
/// MySQL과 동일한 @ParamName / LIMIT N 문법을 사용한다.
/// </summary>
/// <example><code>
/// var qb = new SqliteQueryBuilder();
///
/// var (sql, ps) = qb
///     .From("app_config")
///     .Select("key", "value", "reg_dt")
///     .Where("key", QueryOp.StartsWith, "sensor_")
///     .OrderBy("key")
///     .Limit(50)
///     .Build();
/// // → SELECT key, value, reg_dt
/// //   FROM app_config
/// //   WHERE key LIKE @p0
/// //   ORDER BY key ASC
/// //   LIMIT 50
/// </code></example>
public sealed class SqliteQueryBuilder : QueryBuilderBase
{
    // §1 ─ QueryBuilderBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>SQLite 파라미터 접두사는 @ 입니다 ($ 또는 : 도 동작).</remarks>
    protected override string ParamPrefix => "@";

    /// <inheritdoc/>
    /// <remarks>SQLite는 SELECT TOP N 미지원. LIMIT 절은 BuildLimitClause에서 처리.</remarks>
    protected override string BuildSelectPrefix(int? limit) => "SELECT";

    /// <inheritdoc/>
    /// <remarks>SQLite LIMIT 방식 — 후행에 LIMIT N 추가.</remarks>
    protected override string BuildLimitClause(int count) => $"LIMIT {count}";
}