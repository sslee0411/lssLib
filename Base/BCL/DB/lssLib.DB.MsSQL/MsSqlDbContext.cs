// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.MsSql · MsSqlDbContext.cs
//  역할: MSSQL DbContext 구현
//        ADO.NET SqlClient 기반 연결·쿼리·SP·트랜잭션 처리
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
//  v1.0.1  2025-05-19  BulkInsertAsync — await using → using 수정 (SqlBulkCopy IDisposable 전용)
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using Microsoft.Data.SqlClient;
using lssLib.DB.Abstractions;
using lssLib.DB.Core;

namespace lssLib.DB.MsSql;

/// <summary>
/// MSSQL DbContext 구현.
/// Microsoft.Data.SqlClient 기반으로 연결·쿼리·SP·트랜잭션을 처리한다.
/// </summary>
/// <example><code>
/// var cfg = new RelationalDbConfig(
///     DbProviderType.MsSql,
///     "Server=localhost;Database=IIoT;Integrated Security=true;",
///     commandTimeoutSec: 30);
///
/// await using var ctx = new MsSqlDbContext(cfg);
/// await ctx.OpenAsync();
///
/// // SQL 조회
/// DbResult<DataTable> r = await ctx.QueryTableAsync(
///     "SELECT * FROM SENSOR WHERE PLANT_CD = @P1",
///     parameters: [DbParam.In("@P1", "A01")]);
///
/// // SP 호출 (IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR 표준 패턴)
/// DbResult<SpResult> sp = await ctx.CallSpAsync("SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT 'A01','2024-01-01' FROM DUAL"));
/// </code></example>
public sealed class MsSqlDbContext : DbContextBase
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly RelationalDbConfig _cfg;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="config">MSSQL 연결 설정.</param>
    /// <exception cref="DbConfigException">설정 유효성 검사 실패 시.</exception>
    public MsSqlDbContext(RelationalDbConfig config) : base(config)
    {
        if (config.ProviderType != DbProviderType.MsSql)
            throw new DbConfigException(config.ProviderType,
                "MsSqlDbContext는 DbProviderType.MsSql 설정만 허용합니다.");
        _cfg = config;
    }

    // §3 ─ DbContextBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override DbProviderType ProviderType => DbProviderType.MsSql;

    /// <inheritdoc/>
    protected override IDbConnection CreateConnection()
        => new SqlConnection(_cfg.ConnectionString);

    /// <inheritdoc/>
    protected override async Task OpenConnectionAsync(
        IDbConnection conn, CancellationToken ct)
    {
        if (conn is SqlConnection sqlConn)
            await sqlConn.OpenAsync(ct).ConfigureAwait(false);
        else
            conn.Open();
    }

    // §4 ─ Execute (INSERT / UPDATE / DELETE)
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override async Task<DbResult<int>> ExecuteCoreAsync(
        string sql,
        CommandType commandType,
        DbParam[]? parameters,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var cmd = BuildCommand(sql, commandType, parameters);
            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            sw.Stop();
            return DbResult<int>.Ok(affected, sw.ElapsedMilliseconds);
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            sw.Stop();
            return DbResult<int>.Timeout(
                $"쿼리 타임아웃 ({_cfg.CommandTimeoutSec}초 초과)", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DbResult<int>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §5 ─ QueryTable (SELECT → DataTable)
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override async Task<DbResult<DataTable>> QueryTableCoreAsync(
        string sql,
        CommandType commandType,
        DbParam[]? parameters,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var cmd = BuildCommand(sql, commandType, parameters);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var dt = new DataTable();
            dt.Load(reader);

            sw.Stop();
            return DbResult<DataTable>.Ok(dt, sw.ElapsedMilliseconds);
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            sw.Stop();
            return DbResult<DataTable>.Timeout(
                $"쿼리 타임아웃 ({_cfg.CommandTimeoutSec}초 초과)", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DbResult<DataTable>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §6 ─ CallSp (IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR 표준 패턴)
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override async Task<DbResult<SpResult>> CallSpCoreAsync(
        string spName,
        DbParam[]? parameters,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var cmd = BuildCommand(
                spName, CommandType.StoredProcedure, parameters);

            // SP 실행 — ExecuteReader로 OUT_CURSOR 결과셋 수신
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            // OUT_CURSOR → DataTable 적재
            var dt = new DataTable();
            dt.Load(reader);
            reader.Close();

            // OUT 파라미터 추출 (reader 닫힌 후 접근 가능)
            var returnCode = GetOutParam(cmd, "OUT_RETURNCODE");
            var returnMsg = GetOutParam(cmd, "OUT_RETURNMSG");

            sw.Stop();

            var sp = returnCode == "1"
                ? SpResult.Ok(returnMsg, dt.Rows.Count > 0 ? dt : null)
                : SpResult.Fail(returnCode, returnMsg);

            if (!sp.IsSuccess)
            {
                LogWarn($"SP 반환 오류 [{spName}] Code={returnCode} Msg={returnMsg}");
                return DbResult<SpResult>.Fail(
                    $"[{returnCode}] {returnMsg}", sw.ElapsedMilliseconds);
            }

            return DbResult<SpResult>.Ok(sp, sw.ElapsedMilliseconds);
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            sw.Stop();
            return DbResult<SpResult>.Timeout(
                $"SP 타임아웃 ({_cfg.CommandTimeoutSec}초 초과): {spName}",
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DbResult<SpResult>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §7 ─ MSSQL 전용 — BulkInsert
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SqlBulkCopy를 사용해 DataTable을 대량 삽입합니다.
    /// 수천~수만 건 배치 삽입에 최적화된 방법입니다.
    /// </summary>
    /// <param name="destinationTable">대상 테이블 이름.</param>
    /// <param name="data">삽입할 DataTable (컬럼명이 테이블 컬럼과 일치해야 함).</param>
    /// <param name="batchSize">배치 크기 (기본값: 1000).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>삽입 행 수를 담은 DbResult.</returns>
    /// <example><code>
    /// var dt = new DataTable();
    /// dt.Columns.Add("SENSOR_ID",    typeof(int));
    /// dt.Columns.Add("SENSOR_VALUE", typeof(double));
    /// dt.Columns.Add("REG_DT",       typeof(DateTime));
    ///
    /// foreach (var s in sensors)
    ///     dt.Rows.Add(s.Id, s.Value, s.RegDt);
    ///
    /// DbResult<int> r = await ctx.BulkInsertAsync("SENSOR_DATA", dt);
    /// </code></example>
    public async Task<DbResult<int>> BulkInsertAsync(
        string destinationTable,
        DataTable data,
        int batchSize = 1000,
        CancellationToken ct = default)
    {
        if (Connection is not SqlConnection sqlConn)
            return DbResult<int>.Fail("BulkInsert는 SqlConnection이 필요합니다.");
        if (data.Rows.Count == 0)
            return DbResult<int>.Ok(0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var bulkCopy = Transaction is SqlTransaction sqlTx
                ? new SqlBulkCopy(sqlConn, SqlBulkCopyOptions.Default, sqlTx)
                : new SqlBulkCopy(sqlConn);

            // SqlBulkCopy 는 IDisposable 만 구현 (IAsyncDisposable 미지원)
            // → await using 대신 using 사용
            using (bulkCopy)
            {
                bulkCopy.DestinationTableName = destinationTable;
                bulkCopy.BatchSize = batchSize;
                bulkCopy.BulkCopyTimeout = _cfg.CommandTimeoutSec;

                // 컬럼 매핑 (DataTable 컬럼명 → 테이블 컬럼명 1:1 매핑)
                foreach (DataColumn col in data.Columns)
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);

                await bulkCopy.WriteToServerAsync(data, ct).ConfigureAwait(false);
            }

            sw.Stop();
            int count = data.Rows.Count;
            Log($"BulkInsert ({sw.ElapsedMilliseconds}ms): {count}행 → {destinationTable}");
            return DbResult<int>.Ok(count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"BulkInsert 실패: {ex.Message} → {destinationTable}");
            return DbResult<int>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §8 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// <summary>SqlCommand 생성 및 파라미터 설정.</summary>
    private SqlCommand BuildCommand(
        string sql,
        CommandType commandType,
        DbParam[]? parameters)
    {
        var cmd = new SqlCommand(sql, (SqlConnection)Connection!)
        {
            CommandType = commandType,
            CommandTimeout = _cfg.CommandTimeoutSec,
        };

        if (Transaction is SqlTransaction sqlTx)
            cmd.Transaction = sqlTx;

        if (parameters is not null)
            AttachParameters(cmd, parameters);

        return cmd;
    }

    /// <summary>DbParam[] → SqlParameter[] 변환 후 커맨드에 부착.</summary>
    private static void AttachParameters(SqlCommand cmd, DbParam[] parameters)
    {
        foreach (var p in parameters)
        {
            var sp = new SqlParameter
            {
                ParameterName = p.Name,
                Direction = p.Direction,
                Value = p.Value ?? DBNull.Value,
            };

            if (p.Size > 0) sp.Size = p.Size;

            // DbParamType → SqlDbType 매핑
            sp.SqlDbType = p.ParamType switch
            {
                DbParamType.VarChar => SqlDbType.NVarChar,
                DbParamType.Char => SqlDbType.NChar,
                DbParamType.Text => SqlDbType.NText,
                DbParamType.TinyInt => SqlDbType.TinyInt,
                DbParamType.SmallInt => SqlDbType.SmallInt,
                DbParamType.Int => SqlDbType.Int,
                DbParamType.BigInt => SqlDbType.BigInt,
                DbParamType.Float => SqlDbType.Float,
                DbParamType.Double => SqlDbType.Float,
                DbParamType.Decimal => SqlDbType.Decimal,
                DbParamType.Date => SqlDbType.Date,
                DbParamType.DateTime => SqlDbType.DateTime2,
                DbParamType.DateTimeOffset => SqlDbType.DateTimeOffset,
                DbParamType.Boolean => SqlDbType.Bit,
                DbParamType.Guid => SqlDbType.UniqueIdentifier,
                DbParamType.Binary => SqlDbType.VarBinary,
                DbParamType.Cursor => SqlDbType.Structured,
                _ => SqlDbType.NVarChar,   // Auto → NVarChar 기본
            };

            cmd.Parameters.Add(sp);
        }
    }

    /// <summary>OUT 파라미터 값을 문자열로 추출.</summary>
    private static string GetOutParam(SqlCommand cmd, string name)
    {
        if (cmd.Parameters.Contains(name) &&
            cmd.Parameters[name].Value is not null and not DBNull)
            return cmd.Parameters[name].Value!.ToString() ?? string.Empty;
        return string.Empty;
    }
}