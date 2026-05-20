// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.Oracle · OracleRepository.cs
//  역할: Oracle Repository 구현
//        RepositoryBase 공통 CRUD + Oracle 전용 SP 확장 API
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
//  v1.0.1  2025-05-19  _rowMapper CS0649/CS8618 수정 (생성자에서 초기화)
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using lssLib.DB.Abstractions;
using lssLib.DB.Contracts;
using lssLib.DB.Core;

namespace lssLib.DB.Oracle;

/// <summary>
/// Oracle Repository 구현.
/// RepositoryBase 공통 CRUD에 Oracle 전용 SP 확장 API를 추가한다.
/// </summary>
/// <typeparam name="T">엔티티 타입.</typeparam>
/// <example><code>
/// // ① RowMapper 정의
/// RowMapper<SensorData> mapper = row => new SensorData
/// {
///     SensorId   = Convert.ToInt32(row["SENSOR_ID"]),
///     Value      = Convert.ToDouble(row["SENSOR_VALUE"]),
///     RegDt      = Convert.ToDateTime(row["REG_DT"]),
///     PlantCd    = row["PLANT_CD"].ToString() ?? string.Empty,
/// };
///
/// // ② Repository 생성
/// var repo = new OracleRepository<SensorData>(ctx, mapper);
///
/// // ③ CallProc 패턴 (표준 SP)
/// DbResult<List<SensorData>> r = await repo.CallSpQueryAsync(
///     "SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT 'A01','2024-01-01' FROM DUAL"));
///
/// // ④ CallProc1 패턴 (가변인수)
/// DbResult<SpResult> r2 = await repo.CallSpArgsAsync(
///     "SP_SENSOR_GET", ct, "A01", "2024-01-01");
///
/// // ⑤ Sp_MutiSave 패턴 (DataTable 일괄 저장)
/// DbResult<int> r3 = await repo.SpMutiSaveAsync(dt, "SP_SENSOR_SAVE",
///     whereConditions: [("USE_YN", "Y")],
///     paramColumns:    ["SENSOR_ID", "SENSOR_VALUE"]);
/// </code></example>
public sealed class OracleRepository<T> : RepositoryBase<T> where T : class
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly OracleDbContext _oracleCtx;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="context">Oracle 컨텍스트.</param>
    /// <param name="mapper">DataRow → T 변환 함수.</param>
    public OracleRepository(OracleDbContext context, RowMapper<T> mapper)
        : base(context, mapper)
    {
        _oracleCtx = context;
        _rowMapper = mapper;   // RepositoryBase._mapper는 private → 로컬 보관
    }

    // §3 ─ Oracle 전용 — CallSpArgs (OracleDB.CallProc1 계승)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 가변 인수를 IN_DATA SELECT 구문으로 자동 조립해 SP를 호출합니다.
    /// OracleDB.CallProc1(spName, pram...) 현대화 버전.
    /// </summary>
    /// <param name="spName">SP 이름.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <param name="args">IN_DATA에 넣을 값 목록.</param>
    /// <returns>SpResult를 담은 DbResult.</returns>
    public Task<DbResult<SpResult>> CallSpArgsAsync(
        string spName,
        CancellationToken ct = default,
        params object?[] args)
        => _oracleCtx.CallSpArgsAsync(spName, ct, args);

    /// <summary>
    /// 가변 인수 SP 호출 후 OUT_CURSOR를 엔티티 목록으로 자동 변환합니다.
    /// </summary>
    /// <param name="spName">SP 이름.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <param name="args">IN_DATA에 넣을 값 목록.</param>
    /// <returns>엔티티 목록을 담은 DbResult.</returns>
    public async Task<DbResult<List<T>>> CallSpArgsQueryAsync(
        string spName,
        CancellationToken ct = default,
        params object?[] args)
    {
        var spResult = await _oracleCtx.CallSpArgsAsync(spName, ct, args)
            .ConfigureAwait(false);

        if (!spResult.IsOk)
            return DbResult<List<T>>.Fail(spResult.Message, spResult.ElapsedMs);

        var sp = spResult.Value!;
        if (!sp.IsSuccess)
            return DbResult<List<T>>.Fail(
                $"[{sp.ReturnCode}] {sp.ReturnMessage}", spResult.ElapsedMs);

        if (sp.Table is null)
            return DbResult<List<T>>.Ok([], spResult.ElapsedMs);

        try
        {
            var list = new List<T>();
            foreach (DataRow row in sp.Table.Rows)
                list.Add(_rowMapper(row));
            return DbResult<List<T>>.Ok(list, spResult.ElapsedMs);
        }
        catch (Exception ex)
        {
            return DbResult<List<T>>.Error(ex, spResult.ElapsedMs);
        }
    }

    // §4 ─ Oracle 전용 — SpMutiSave (OracleDB.Sp_MutiSave 계승)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// DataTable 행을 조건 필터 후 SP로 일괄 저장합니다.
    /// OracleDB.Sp_MutiSave 현대화 버전.
    /// </summary>
    /// <param name="dt">처리할 DataTable.</param>
    /// <param name="spName">호출할 SP 이름.</param>
    /// <param name="whereConditions">행 필터 조건 (컬럼명-비교값 튜플 배열).</param>
    /// <param name="paramColumns">SP에 전달할 DataTable 컬럼명 배열.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>처리된 행 수를 담은 DbResult.</returns>
    public Task<DbResult<int>> SpMutiSaveAsync(
        DataTable dt,
        string spName,
        (string Column, string Value)[] whereConditions,
        string[] paramColumns,
        CancellationToken ct = default)
        => _oracleCtx.SpMutiSaveAsync(dt, spName, whereConditions, paramColumns, ct);

    // §5 ─ RowMapper 보관 (RepositoryBase._mapper private → 생성자에서 별도 초기화)
    // ─────────────────────────────────────────────────────────────────
    private readonly RowMapper<T> _rowMapper;
}