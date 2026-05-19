// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Contracts/IRepository.cs
//  역할: Generic CRUD 계약 — Provider·엔티티 타입 독립
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using lssLib.DB.Core;

namespace lssLib.DB.Contracts;

/// <summary>
/// Generic CRUD Repository 계약.
/// <typeparam name="T">엔티티 타입 (class 제약).</typeparam>
/// </summary>
/// <remarks>
/// 사용 패턴.
/// <code>
/// // Oracle SP 방식
/// IRepository<SensorData> repo = new OracleRepository<SensorData>(ctx, MapRow);
/// DbResult<SpResult> r = await repo.CallSpAsync("SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT '2024-01-01' FROM DUAL"));
///
/// // SQL 직접 실행 방식
/// DbResult<List<SensorData>> r2 =
///     await repo.QueryAsync("SELECT * FROM SENSOR WHERE ID = :ID",
///         DbParam.In("ID", 42));
/// </code>
/// </remarks>
public interface IRepository<T> where T : class
{
    // §1 ─ 조회

    /// <summary>
    /// SQL을 실행하고 엔티티 목록을 반환합니다.
    /// </summary>
    /// <param name="sql">SELECT 문.</param>
    /// <param name="parameters">파라미터 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>엔티티 목록을 담은 DbResult.</returns>
    Task<DbResult<List<T>>> QueryAsync(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    /// <summary>
    /// SQL을 실행하고 단일 엔티티를 반환합니다.
    /// 결과가 없으면 Value = null, IsOk = true.
    /// </summary>
    /// <param name="sql">SELECT 문 (단건 기대).</param>
    /// <param name="parameters">파라미터 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>단일 엔티티를 담은 DbResult.</returns>
    Task<DbResult<T?>> QuerySingleAsync(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    /// <summary>
    /// SQL을 실행하고 스칼라 값(첫 번째 행 첫 번째 열)을 반환합니다.
    /// </summary>
    /// <typeparam name="TScalar">스칼라 값 타입.</typeparam>
    /// <param name="sql">SELECT 문.</param>
    /// <param name="parameters">파라미터 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>스칼라 값을 담은 DbResult.</returns>
    Task<DbResult<TScalar?>> QueryScalarAsync<TScalar>(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    // §2 ─ 실행 (INSERT / UPDATE / DELETE)

    /// <summary>
    /// SQL을 실행하고 영향 받은 행 수를 반환합니다.
    /// </summary>
    /// <param name="sql">DML 문.</param>
    /// <param name="parameters">파라미터 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>영향 받은 행 수를 담은 DbResult.</returns>
    Task<DbResult<int>> ExecuteAsync(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    // §3 ─ SP 실행

    /// <summary>
    /// 저장 프로시저를 실행하고 SpResult를 반환합니다.
    /// IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR 표준 패턴.
    /// </summary>
    /// <param name="spName">SP 이름.</param>
    /// <param name="parameters">파라미터 목록 (DbParam.StandardSp() 활용 권장).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>SpResult를 담은 DbResult.</returns>
    Task<DbResult<SpResult>> CallSpAsync(
        string spName,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    /// <summary>
    /// 저장 프로시저를 실행하고 엔티티 목록으로 매핑해서 반환합니다.
    /// OUT_CURSOR 결과를 자동으로 엔티티 목록으로 변환한다.
    /// </summary>
    /// <param name="spName">SP 이름.</param>
    /// <param name="parameters">파라미터 목록 (DbParam.StandardSp() 활용 권장).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>엔티티 목록을 담은 DbResult.</returns>
    Task<DbResult<List<T>>> CallSpQueryAsync(
        string spName,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    // §4 ─ 트랜잭션 일괄 실행

    /// <summary>
    /// 여러 SQL을 하나의 트랜잭션으로 묶어 실행합니다.
    /// 하나라도 실패하면 전체 롤백.
    /// </summary>
    /// <param name="commands">실행할 (sql, parameters) 쌍 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>전체 영향 행 수 합계를 담은 DbResult.</returns>
    Task<DbResult<int>> ExecuteBatchAsync(
        IEnumerable<(string Sql, DbParam[]? Parameters)> commands,
        CancellationToken ct = default);
}

// §5 ─ 행 매핑 델리게이트
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// DataRow → 엔티티 변환 델리게이트.
/// RepositoryBase 파생 클래스 생성자에서 주입한다.
/// </summary>
/// <typeparam name="T">엔티티 타입.</typeparam>
/// <param name="row">DataRow 인스턴스.</param>
/// <returns>매핑된 엔티티.</returns>
/// <example><code>
/// // 사용 예
/// RowMapper<SensorData> mapper = row => new SensorData
/// {
///     Id    = Convert.ToInt32(row["SENSOR_ID"]),
///     Value = Convert.ToDouble(row["SENSOR_VALUE"]),
///     Time  = Convert.ToDateTime(row["REG_DT"]),
/// };
/// </code></example>
public delegate T RowMapper<T>(DataRow row) where T : class;