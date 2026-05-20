// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.MySql · MySqlDbContext.cs
//  역할: MySQL / MariaDB DbContext 구현
//        MySqlConnector 기반 연결·쿼리·SP·트랜잭션 처리
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using MySqlConnector;
using lssLib.DB.Abstractions;
using lssLib.DB.Core;

namespace lssLib.DB.MySql;

/// <summary>
/// MySQL / MariaDB DbContext 구현.
/// MySqlConnector 기반으로 연결·쿼리·SP·트랜잭션을 처리한다.
/// </summary>
/// <example><code>
/// var cfg = new RelationalDbConfig(
///     DbProviderType.MySql,
///     "Server=localhost;Database=IIoT;Uid=root;Pwd=password;",
///     commandTimeoutSec: 30);
///
/// await using var ctx = new MySqlDbContext(cfg);
/// await ctx.OpenAsync();
///
/// // SQL 조회
/// DbResult<DataTable> r = await ctx.QueryTableAsync(
///     "SELECT * FROM sensor_data WHERE plant_cd = @P1",
///     parameters: [DbParam.In("@P1", "A01")]);
///
/// // SP 호출 (IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG 표준 패턴)
/// DbResult<SpResult> sp = await ctx.CallSpAsync("SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT 'A01','2024-01-01'"));
/// </code></example>
public sealed class MySqlDbContext : DbContextBase
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly RelationalDbConfig _cfg;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="config">MySQL 연결 설정.</param>
    /// <exception cref="DbConfigException">설정 유효성 검사 실패 시.</exception>
    public MySqlDbContext(RelationalDbConfig config) : base(config)
    {
        if (config.ProviderType != DbProviderType.MySql)
            throw new DbConfigException(config.ProviderType,
                "MySqlDbContext는 DbProviderType.MySql 설정만 허용합니다.");
        _cfg = config;
    }

    // §3 ─ DbContextBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override DbProviderType ProviderType => DbProviderType.MySql;

    /// <inheritdoc/>
    protected override IDbConnection CreateConnection()
        => new MySqlConnection(_cfg.ConnectionString);

    /// <inheritdoc/>
    protected override async Task OpenConnectionAsync(
        IDbConnection conn, CancellationToken ct)
    {
        if (conn is MySqlConnection mysqlConn)
            await mysqlConn.OpenAsync(ct).ConfigureAwait(false);
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
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.QueryInterrupted)
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
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.QueryInterrupted)
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
    // MySQL SP 참고:
    //   Oracle RefCursor 대신 MySQL은 SELECT 결과셋을 직접 반환한다.
    //   OUT_CURSOR 파라미터 대신 ExecuteReader로 결과셋 수신.

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

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            // 첫 번째 결과셋 → DataTable (OUT_CURSOR 역할)
            var dt = new DataTable();
            dt.Load(reader);
            reader.Close();

            // OUT 파라미터 추출
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
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.QueryInterrupted)
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

    // §7 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// <summary>MySqlCommand 생성 및 파라미터 설정.</summary>
    private MySqlCommand BuildCommand(
        string sql,
        CommandType commandType,
        DbParam[]? parameters)
    {
        var cmd = new MySqlCommand(sql, (MySqlConnection)Connection!)
        {
            CommandType = commandType,
            CommandTimeout = _cfg.CommandTimeoutSec,
        };

        if (Transaction is MySqlTransaction mysqlTx)
            cmd.Transaction = mysqlTx;

        if (parameters is not null)
            AttachParameters(cmd, parameters);

        return cmd;
    }

    /// <summary>DbParam[] → MySqlParameter[] 변환 후 커맨드에 부착.</summary>
    private static void AttachParameters(MySqlCommand cmd, DbParam[] parameters)
    {
        foreach (var p in parameters)
        {
            var mp = new MySqlParameter
            {
                ParameterName = p.Name,
                Direction = p.Direction,
                Value = p.Value ?? DBNull.Value,
            };

            if (p.Size > 0) mp.Size = p.Size;

            // DbParamType → MySqlDbType 매핑
            mp.MySqlDbType = p.ParamType switch
            {
                DbParamType.VarChar => MySqlDbType.VarChar,
                DbParamType.Char => MySqlDbType.String,
                DbParamType.Text => MySqlDbType.LongText,
                DbParamType.TinyInt => MySqlDbType.Byte,
                DbParamType.SmallInt => MySqlDbType.Int16,
                DbParamType.Int => MySqlDbType.Int32,
                DbParamType.BigInt => MySqlDbType.Int64,
                DbParamType.Float => MySqlDbType.Float,
                DbParamType.Double => MySqlDbType.Double,
                DbParamType.Decimal => MySqlDbType.Decimal,
                DbParamType.Date => MySqlDbType.Date,
                DbParamType.DateTime => MySqlDbType.DateTime,
                DbParamType.DateTimeOffset => MySqlDbType.DateTime,
                DbParamType.Boolean => MySqlDbType.Bit,
                DbParamType.Guid => MySqlDbType.Guid,
                DbParamType.Binary => MySqlDbType.Blob,
                _ => MySqlDbType.VarChar,  // Auto → VarChar 기본
            };

            cmd.Parameters.Add(mp);
        }
    }

    /// <summary>OUT 파라미터 값을 문자열로 추출.</summary>
    private static string GetOutParam(MySqlCommand cmd, string name)
    {
        if (!cmd.Parameters.Contains(name)) return string.Empty;

        var val = cmd.Parameters[name].Value;
        if (val is null || val is DBNull) return string.Empty;
        return val.ToString() ?? string.Empty;
    }
}