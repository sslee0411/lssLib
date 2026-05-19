// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Abstractions/RepositoryBase.cs
//  역할: IRepository 공통 구현 — CRUD·RowMapper·BatchExecute 기반 클래스
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using lssLib.DB.Contracts;
using lssLib.DB.Core;
//using lssLib.Log;

namespace lssLib.DB.Abstractions;

/// <summary>
/// IRepository 공통 구현 기반 클래스.
/// Provider별 구현체(OracleRepository, MsSqlRepository 등)에서 상속한다.
/// </summary>
/// <typeparam name="T">엔티티 타입.</typeparam>
/// <remarks>
/// 파생 클래스 최소 구현 예.
/// <code>
/// public sealed class OracleRepository<T> : RepositoryBase<T>
///     where T : class
/// {
///     public OracleRepository(IDbContext context, RowMapper<T> mapper)
///         : base(context, mapper) { }
/// }
///
/// // 사용
/// RowMapper<SensorData> mapper = row => new SensorData
/// {
///     Id    = Convert.ToInt32(row["SENSOR_ID"]),
///     Value = Convert.ToDouble(row["SENSOR_VALUE"]),
/// };
/// var repo = new OracleRepository<SensorData>(ctx, mapper);
/// var r = await repo.CallSpQueryAsync("SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT '001' FROM DUAL"));
/// </code>
/// </remarks>
public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly IDbContext _context;
    private readonly RowMapper<T> _mapper;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────
    /// <param name="context">DB 컨텍스트.</param>
    /// <param name="mapper">DataRow → T 변환 함수.</param>
    protected RepositoryBase(IDbContext context, RowMapper<T> mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    // §3 ─ 보호 프로퍼티
    // ─────────────────────────────────────────────────────────────────
    /// <summary>내부 DB 컨텍스트 접근.</summary>
    protected IDbContext Context => _context;

    // §4 ─ IRepository 구현 — 조회
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<DbResult<List<T>>> QueryAsync(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
    {
        var tableResult = await _context.QueryTableAsync(
            sql, CommandType.Text, parameters, ct).ConfigureAwait(false);

        if (!tableResult.IsOk)
            return DbResult<List<T>>.Fail(tableResult.Message, tableResult.ElapsedMs);

        try
        {
            var list = MapTable(tableResult.Value!);
            return DbResult<List<T>>.Ok(list, tableResult.ElapsedMs);
        }
        catch (Exception ex)
        {
            LogError($"행 매핑 실패: {ex.Message}");
            return DbResult<List<T>>.Error(ex, tableResult.ElapsedMs);
        }
    }

    /// <inheritdoc/>
    public async Task<DbResult<T?>> QuerySingleAsync(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
    {
        var tableResult = await _context.QueryTableAsync(
            sql, CommandType.Text, parameters, ct).ConfigureAwait(false);

        if (!tableResult.IsOk)
            return DbResult<T?>.Fail(tableResult.Message, tableResult.ElapsedMs);

        try
        {
            var table = tableResult.Value!;
            if (table.Rows.Count == 0)
                return DbResult<T?>.Ok(null, tableResult.ElapsedMs);

            var entity = _mapper(table.Rows[0]);
            return DbResult<T?>.Ok(entity, tableResult.ElapsedMs);
        }
        catch (Exception ex)
        {
            LogError($"단건 매핑 실패: {ex.Message}");
            return DbResult<T?>.Error(ex, tableResult.ElapsedMs);
        }
    }

    /// <inheritdoc/>
    public async Task<DbResult<TScalar?>> QueryScalarAsync<TScalar>(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
    {
        var tableResult = await _context.QueryTableAsync(
            sql, CommandType.Text, parameters, ct).ConfigureAwait(false);

        if (!tableResult.IsOk)
            return DbResult<TScalar?>.Fail(tableResult.Message, tableResult.ElapsedMs);

        try
        {
            var table = tableResult.Value!;
            if (table.Rows.Count == 0 || table.Columns.Count == 0)
                return DbResult<TScalar?>.Ok(default, tableResult.ElapsedMs);

            var raw = table.Rows[0][0];
            if (raw is DBNull) return DbResult<TScalar?>.Ok(default, tableResult.ElapsedMs);

            var scalar = (TScalar)Convert.ChangeType(raw, typeof(TScalar));
            return DbResult<TScalar?>.Ok(scalar, tableResult.ElapsedMs);
        }
        catch (Exception ex)
        {
            LogError($"스칼라 변환 실패: {ex.Message}");
            return DbResult<TScalar?>.Error(ex, tableResult.ElapsedMs);
        }
    }

    // §5 ─ IRepository 구현 — 실행
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<DbResult<int>> ExecuteAsync(
        string sql,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
        => _context.ExecuteAsync(sql, CommandType.Text, parameters, ct);

    // §6 ─ IRepository 구현 — SP 실행
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<DbResult<SpResult>> CallSpAsync(
        string spName,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
        => _context.CallSpAsync(spName, parameters, ct);

    /// <inheritdoc/>
    public async Task<DbResult<List<T>>> CallSpQueryAsync(
        string spName,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
    {
        var spResult = await _context.CallSpAsync(spName, parameters, ct)
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
            var list = MapTable(sp.Table);
            return DbResult<List<T>>.Ok(list, spResult.ElapsedMs);
        }
        catch (Exception ex)
        {
            LogError($"SP 결과 매핑 실패: {ex.Message} | SP: {spName}");
            return DbResult<List<T>>.Error(ex, spResult.ElapsedMs);
        }
    }

    // §7 ─ IRepository 구현 — 트랜잭션 일괄 실행
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<DbResult<int>> ExecuteBatchAsync(
        IEnumerable<(string Sql, DbParam[]? Parameters)> commands,
        CancellationToken ct = default)
    {
        var cmdList = commands.ToList();
        if (cmdList.Count == 0)
            return DbResult<int>.Ok(0);

        var alreadyInTx = _context.IsInTransaction;

        if (!alreadyInTx)
            await _context.BeginTransactionAsync(ct).ConfigureAwait(false);

        int total = 0;
        try
        {
            foreach (var (sql, ps) in cmdList)
            {
                var r = await _context.ExecuteAsync(
                    sql, CommandType.Text, ps, ct).ConfigureAwait(false);

                if (!r.IsOk)
                {
                    if (!alreadyInTx) await _context.RollbackAsync().ConfigureAwait(false);
                    return DbResult<int>.Fail($"배치 실행 실패: {r.Message}");
                }
                total += r.Value;
            }

            if (!alreadyInTx)
                await _context.CommitAsync().ConfigureAwait(false);

            return DbResult<int>.Ok(total);
        }
        catch (Exception ex)
        {
            if (!alreadyInTx) await _context.RollbackAsync().ConfigureAwait(false);
            LogError($"배치 실행 예외: {ex.Message}");
            return DbResult<int>.Error(ex);
        }
    }

    // §8 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// <summary>DataTable 전체 행을 엔티티 목록으로 변환.</summary>
    private List<T> MapTable(DataTable table)
    {
        var list = new List<T>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
            list.Add(_mapper(row));
        return list;
    }

    /// <summary>오류 로그 기록.</summary>
    protected virtual void LogError(string message)
    { }
    // => LogManager.Instance.Error($"DB.Repository<{typeof(T).Name}>", message);
}