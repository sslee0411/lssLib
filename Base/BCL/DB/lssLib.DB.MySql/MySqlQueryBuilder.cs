// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.MySql · MySqlQueryBuilder.cs
//  역할: MySQL 쿼리 빌더 — @ParamName / LIMIT N 문법 구현
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Abstractions;

namespace lssLib.DB.MySql;

/// <summary>
/// MySQL 쿼리 빌더.
/// QueryBuilderBase 공통 로직 위에 MySQL 고유 문법을 구현한다.
/// </summary>
/// <example><code>
/// var qb = new MySqlQueryBuilder();
///
/// var (sql, ps) = qb
///     .From("sensor_data")
///     .Select("sensor_id", "sensor_value", "reg_dt")
///     .Where("plant_cd",  QueryOp.Eq,   "A01")
///     .Where("reg_dt",    QueryOp.GtEq, DateTime.Today)
///     .OrderBy("reg_dt",  false)
///     .Limit(100)
///     .Build();
/// // → SELECT sensor_id, sensor_value, reg_dt
/// //   FROM sensor_data
/// //   WHERE plant_cd = @p0 AND reg_dt >= @p1
/// //   ORDER BY reg_dt DESC
/// //   LIMIT 100
/// </code></example>
public sealed class MySqlQueryBuilder : QueryBuilderBase
{
    // §1 ─ QueryBuilderBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>MySQL 파라미터 접두사는 @ 입니다.</remarks>
    protected override string ParamPrefix => "@";

    /// <inheritdoc/>
    /// <remarks>MySQL은 SELECT TOP N 미지원. LIMIT 절은 BuildLimitClause에서 처리.</remarks>
    protected override string BuildSelectPrefix(int? limit) => "SELECT";

    /// <inheritdoc/>
    /// <remarks>MySQL LIMIT 방식 — SELECT 후행에 LIMIT N 추가.</remarks>
    protected override string BuildLimitClause(int count) => $"LIMIT {count}";
}