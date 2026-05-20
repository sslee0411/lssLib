// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.MsSql · MsSqlQueryBuilder.cs
//  역할: MSSQL 쿼리 빌더 — TOP N / @ParamName 문법 구현
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Abstractions;

namespace lssLib.DB.MsSql;

/// <summary>
/// MSSQL 쿼리 빌더.
/// QueryBuilderBase 공통 WHERE / ORDER / 파라미터 누적 위에
/// MSSQL 고유 문법(TOP N, @ParamName)을 구현한다.
/// </summary>
/// <example><code>
/// var qb = new MsSqlQueryBuilder();
///
/// // SELECT TOP 100
/// var (sql, ps) = qb
///     .From("SENSOR_DATA")
///     .Select("SENSOR_ID", "SENSOR_VALUE", "REG_DT")
///     .Where("PLANT_CD",   QueryOp.Eq,   "A01")
///     .Where("REG_DT",     QueryOp.GtEq, DateTime.Today)
///     .OrderBy("REG_DT",   false)
///     .Limit(100)
///     .Build();
/// // → SELECT TOP 100 SENSOR_ID, SENSOR_VALUE, REG_DT
/// //   FROM SENSOR_DATA
/// //   WHERE PLANT_CD = @p0 AND REG_DT >= @p1
/// //   ORDER BY REG_DT DESC
///
/// // INSERT
/// var (sql2, ps2) = qb.Reset()
///     .Insert("SENSOR_DATA")
///     .Value("SENSOR_ID",    42)
///     .Value("SENSOR_VALUE", 99.5)
///     .Value("REG_DT",       DateTime.Now)
///     .BuildInsert();
/// // → INSERT INTO SENSOR_DATA (SENSOR_ID, SENSOR_VALUE, REG_DT)
/// //   VALUES (@p0, @p1, @p2)
/// </code></example>
public sealed class MsSqlQueryBuilder : QueryBuilderBase
{
    // §1 ─ QueryBuilderBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>MSSQL 파라미터 접두사는 @ 입니다.</remarks>
    protected override string ParamPrefix => "@";

    /// <inheritdoc/>
    /// <remarks>
    /// MSSQL LIMIT 구현: SELECT TOP N ... 형식.
    /// Limit이 없으면 일반 SELECT, 있으면 SELECT TOP N.
    /// </remarks>
    protected override string BuildSelectPrefix(int? limit)
        => limit.HasValue ? $"SELECT TOP {limit}" : "SELECT";

    /// <inheritdoc/>
    /// <remarks>
    /// MSSQL은 SELECT TOP N 접두어 방식이므로 후행 LIMIT 절은 빈 문자열.
    /// </remarks>
    protected override string BuildLimitClause(int count) => string.Empty;
}