// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.MySql · MySqlRepository.cs
//  역할: MySQL / MariaDB Repository 구현
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Abstractions;
using lssLib.DB.Contracts;

namespace lssLib.DB.MySql;

/// <summary>
/// MySQL / MariaDB Repository 구현.
/// RepositoryBase 공통 CRUD를 그대로 사용하며
/// MySQL 전용 확장이 필요할 때 여기서 추가한다.
/// </summary>
/// <typeparam name="T">엔티티 타입.</typeparam>
/// <example><code>
/// RowMapper<SensorData> mapper = row => new SensorData
/// {
///     SensorId = Convert.ToInt32(row["sensor_id"]),
///     Value    = Convert.ToDouble(row["sensor_value"]),
///     RegDt    = Convert.ToDateTime(row["reg_dt"]),
/// };
///
/// var repo = new MySqlRepository<SensorData>(ctx, mapper);
///
/// // 목록 조회
/// DbResult<List<SensorData>> r = await repo.QueryAsync(
///     "SELECT * FROM sensor_data WHERE plant_cd = @P1 LIMIT 100",
///     [DbParam.In("@P1", "A01")]);
///
/// // SP 조회
/// DbResult<List<SensorData>> r2 = await repo.CallSpQueryAsync(
///     "SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT 'A01','2024-01-01'"));
/// </code></example>
public sealed class MySqlRepository<T> : RepositoryBase<T> where T : class
{
    // §1 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="context">MySQL 컨텍스트.</param>
    /// <param name="mapper">DataRow → T 변환 함수.</param>
    public MySqlRepository(MySqlDbContext context, RowMapper<T> mapper)
        : base(context, mapper) { }
}