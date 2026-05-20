// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.MsSql · MsSqlRepository.cs
//  역할: MSSQL Repository 구현 — RepositoryBase 파생 + BulkInsert 확장
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using lssLib.DB.Abstractions;
using lssLib.DB.Contracts;
using lssLib.DB.Core;

namespace lssLib.DB.MsSql;

/// <summary>
/// MSSQL Repository 구현.
/// RepositoryBase 공통 CRUD에 SqlBulkCopy 기반 BulkInsert를 추가한다.
/// </summary>
/// <typeparam name="T">엔티티 타입.</typeparam>
/// <example><code>
/// // ① RowMapper 정의
/// RowMapper<SensorData> mapper = row => new SensorData
/// {
///     SensorId   = Convert.ToInt32(row["SENSOR_ID"]),
///     Value      = Convert.ToDouble(row["SENSOR_VALUE"]),
///     RegDt      = Convert.ToDateTime(row["REG_DT"]),
///     SensorName = row["SENSOR_NM"].ToString() ?? string.Empty,
/// };
///
/// // ② Repository 생성
/// var repo = new MsSqlRepository<SensorData>(ctx, mapper);
///
/// // ③ SP 조회
/// DbResult<List<SensorData>> r = await repo.CallSpQueryAsync(
///     "SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT 'A01','2024-01-01' FROM DUAL"));
///
/// // ④ BulkInsert
/// DbResult<int> br = await repo.BulkInsertAsync("SENSOR_DATA", dataTable);
/// </code></example>
public sealed class MsSqlRepository<T> : RepositoryBase<T> where T : class
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly MsSqlDbContext _msSqlCtx;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="context">MSSQL 컨텍스트.</param>
    /// <param name="mapper">DataRow → T 변환 함수.</param>
    public MsSqlRepository(MsSqlDbContext context, RowMapper<T> mapper)
        : base(context, mapper)
    {
        _msSqlCtx = context;
    }

    // §3 ─ MSSQL 전용 — BulkInsert
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// DataTable을 SqlBulkCopy로 대량 삽입합니다.
    /// </summary>
    /// <param name="destinationTable">대상 테이블 이름.</param>
    /// <param name="data">삽입할 DataTable.</param>
    /// <param name="batchSize">배치 크기 (기본값: 1000).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>삽입 행 수를 담은 DbResult.</returns>
    public Task<DbResult<int>> BulkInsertAsync(
        string destinationTable,
        DataTable data,
        int batchSize = 1000,
        CancellationToken ct = default)
        => _msSqlCtx.BulkInsertAsync(destinationTable, data, batchSize, ct);

    /// <summary>
    /// 엔티티 목록을 DataTable로 변환 후 SqlBulkCopy로 대량 삽입합니다.
    /// </summary>
    /// <param name="destinationTable">대상 테이블 이름.</param>
    /// <param name="entities">삽입할 엔티티 목록.</param>
    /// <param name="toRow">엔티티 → DataRow 변환 함수.</param>
    /// <param name="columns">DataTable 컬럼 정의 (이름-타입 튜플 배열).</param>
    /// <param name="batchSize">배치 크기 (기본값: 1000).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>삽입 행 수를 담은 DbResult.</returns>
    /// <example><code>
    /// DbResult<int> r = await repo.BulkInsertAsync(
    ///     destinationTable: "SENSOR_DATA",
    ///     entities: sensorList,
    ///     toRow: (s, row) =>
    ///     {
    ///         row["SENSOR_ID"]    = s.SensorId;
    ///         row["SENSOR_VALUE"] = s.Value;
    ///         row["REG_DT"]       = s.RegDt;
    ///     },
    ///     columns:
    ///     [
    ///         ("SENSOR_ID",    typeof(int)),
    ///         ("SENSOR_VALUE", typeof(double)),
    ///         ("REG_DT",       typeof(DateTime)),
    ///     ]);
    /// </code></example>
    public Task<DbResult<int>> BulkInsertAsync(
        string destinationTable,
        IEnumerable<T> entities,
        Action<T, DataRow> toRow,
        (string ColumnName, Type DataType)[] columns,
        int batchSize = 1000,
        CancellationToken ct = default)
    {
        // 엔티티 목록 → DataTable 변환
        var dt = new DataTable(destinationTable);
        foreach (var (colName, colType) in columns)
            dt.Columns.Add(colName, colType);

        foreach (var entity in entities)
        {
            var row = dt.NewRow();
            toRow(entity, row);
            dt.Rows.Add(row);
        }

        return _msSqlCtx.BulkInsertAsync(destinationTable, dt, batchSize, ct);
    }
}