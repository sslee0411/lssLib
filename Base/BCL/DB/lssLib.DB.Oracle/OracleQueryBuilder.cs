// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.Oracle · OracleQueryBuilder.cs
//  역할: Oracle 쿼리 빌더 — :ParamName / ROWNUM LIMIT 문법 구현
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Abstractions;

namespace lssLib.DB.Oracle;

/// <summary>
/// Oracle 쿼리 빌더.
/// QueryBuilderBase 공통 WHERE / ORDER / 파라미터 누적 위에
/// Oracle 고유 문법(:ParamName, ROWNUM)을 구현한다.
/// </summary>
/// <example><code>
/// var qb = new OracleQueryBuilder();
///
/// // SELECT with ROWNUM
/// var (sql, ps) = qb
///     .From("SENSOR_DATA")
///     .Select("SENSOR_ID", "SENSOR_VALUE", "REG_DT")
///     .Where("PLANT_CD",   QueryOp.Eq,   "A01")
///     .Where("REG_DT",     QueryOp.GtEq, DateTime.Today)
///     .OrderBy("REG_DT",   false)
///     .Limit(100)
///     .Build();
/// // → SELECT SENSOR_ID, SENSOR_VALUE, REG_DT
/// //   FROM SENSOR_DATA
/// //   WHERE PLANT_CD = :p0 AND REG_DT >= :p1 AND ROWNUM <= 100
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
/// //   VALUES (:p0, :p1, :p2)
/// </code></example>
public sealed class OracleQueryBuilder : QueryBuilderBase
{
    // §1 ─ QueryBuilderBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>Oracle 파라미터 접두사는 : 입니다.</remarks>
    protected override string ParamPrefix => ":";

    /// <inheritdoc/>
    /// <remarks>
    /// Oracle은 SELECT TOP N 미지원.
    /// ROWNUM 방식은 BuildLimitClause() 에서 WHERE 절에 추가한다.
    /// </remarks>
    protected override string BuildSelectPrefix(int? limit) => "SELECT";

    /// <inheritdoc/>
    /// <remarks>
    /// Oracle ROWNUM 방식 — WHERE 절 마지막에 AND ROWNUM <= N 추가.
    /// ORDER BY 이후에 ROWNUM 적용 시 서브쿼리가 필요하지만
    /// 단순 조회 시나리오에서는 WHERE 절 내 ROWNUM으로 동작한다.
    /// 복잡한 페이징은 SQL 직접 작성을 권장한다.
    /// </remarks>
    protected override string BuildLimitClause(int count)
        => $"AND ROWNUM <= {count}";
}