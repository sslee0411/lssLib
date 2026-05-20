// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.Oracle · OracleDbContext.cs
//  역할: Oracle DbContext 구현
//        OracleDB.CallProc / OracleHelper 패턴 계승·현대화
//
//  원본 참조:
//    OracleDB.cs    → CallProc / CallProc1 / CallProc_Reader
//                     Sp_Save / Sp_MutiSave / Cmd_Select / Cmd_Save
//    OracleHelper.cs → PrepareCommand / AttachParameters
//                      ExecuteNonQuery / ExecuteDataset / ExecuteReader
//
//  주요 변경:
//    Oracle.DataAccess.Client (구버전 ODP.NET)
//      → Oracle.ManagedDataAccess.Core (Managed ODP.NET, .NET 8 지원)
//    OracleDataAdapter.Fill(DataSet)
//      → ExecuteReader + DataTable.Load() (IDbDataAdapter 의존성 제거)
//    정적 전역 상태 (static connStr / oraConn)
//      → 인스턴스 기반 (DbContextBase 생명주기 관리)
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
//  v1.0.1  2025-05-19  GetOutParam CS8780 수정 (not 패턴 내 변수 선언 → 타입 분기로 변경)
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using lssLib.DB.Abstractions;
using lssLib.DB.Core;

namespace lssLib.DB.Oracle;

/// <summary>
/// Oracle DbContext 구현.
/// OracleDB.CallProc / OracleHelper 패턴을 현대화하여 계승한다.
/// </summary>
/// <example><code>
/// var cfg = new RelationalDbConfig(
///     DbProviderType.Oracle,
///     "Data Source=MyOracleDB;User Id=scott;Password=tiger;",
///     commandTimeoutSec: 180);
///
/// await using var ctx = new OracleDbContext(cfg);
/// await ctx.OpenAsync();
///
/// // SP 호출 (OracleDB.CallProc 동일 패턴)
/// DbResult<SpResult> r = await ctx.CallSpAsync("SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT 'A01','2024-01-01' FROM DUAL"));
///
/// // CallProc1 가변인수 패턴
/// DbResult<SpResult> r2 = await ctx.CallSpArgsAsync("SP_SENSOR_GET",
///     "A01", "2024-01-01", "2024-12-31");
/// </code></example>
public sealed class OracleDbContext : DbContextBase
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly RelationalDbConfig _cfg;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="config">Oracle 연결 설정.</param>
    /// <exception cref="DbConfigException">설정 유효성 검사 실패 시.</exception>
    public OracleDbContext(RelationalDbConfig config) : base(config)
    {
        if (config.ProviderType != DbProviderType.Oracle)
            throw new DbConfigException(config.ProviderType,
                "OracleDbContext는 DbProviderType.Oracle 설정만 허용합니다.");
        _cfg = config;
    }

    // §3 ─ DbContextBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override DbProviderType ProviderType => DbProviderType.Oracle;

    /// <inheritdoc/>
    protected override IDbConnection CreateConnection()
        => new OracleConnection(_cfg.ConnectionString);

    /// <inheritdoc/>
    protected override async Task OpenConnectionAsync(
        IDbConnection conn, CancellationToken ct)
    {
        if (conn is OracleConnection oraConn)
            await oraConn.OpenAsync(ct).ConfigureAwait(false);
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
        catch (OracleException ex) when (ex.Number == 1013) // ORA-01013: 사용자가 현재 작업의 취소를 요청했습니다
        {
            sw.Stop();
            return DbResult<int>.Timeout(
                $"쿼리 타임아웃: {ex.Message}", sw.ElapsedMilliseconds);
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
        catch (OracleException ex) when (ex.Number == 1013)
        {
            sw.Stop();
            return DbResult<DataTable>.Timeout(
                $"쿼리 타임아웃: {ex.Message}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DbResult<DataTable>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §6 ─ CallSp — OracleDB.CallProc 패턴 계승
    // ─────────────────────────────────────────────────────────────────
    // 원본 OracleDB.CallProc 구조:
    //   OracleComm.Parameters.Add("IN_DATA",        AsInDate).Direction = Input
    //   OracleComm.Parameters.Add("OUT_RETURNCODE", OracleDbType.Varchar2, 10).Direction = Output
    //   OracleComm.Parameters.Add("OUT_RETURNMSG",  OracleDbType.Varchar2, 200).Direction = Output
    //   OracleComm.Parameters.Add("OUT_CURSOR",     OracleDbType.RefCursor).Direction = Output
    //   OracleDataAdapter.Fill(DataSet) → ExecuteReader + DataTable.Load() 로 변경

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

            // OracleDB.CallProc_Reader 패턴 — ExecuteReader로 OUT_CURSOR 수신
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
        catch (OracleException ex) when (ex.Number == 1013)
        {
            sw.Stop();
            return DbResult<SpResult>.Timeout(
                $"SP 타임아웃: {spName}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DbResult<SpResult>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §7 ─ Oracle 전용 — CallSpArgs (OracleDB.CallProc1 계승)
    // ─────────────────────────────────────────────────────────────────
    // 원본 OracleDB.CallProc1 패턴:
    //   params object[] pram → SELECT 'val1','val2',... FROM DUAL 조립
    //   → DbParam.StandardSp(inData) 자동 생성

    /// <summary>
    /// 가변 인수를 IN_DATA SELECT 구문으로 자동 조립해 SP를 호출합니다.
    /// OracleDB.CallProc1(spName, pram...) 현대화 버전.
    /// </summary>
    /// <param name="spName">SP 이름.</param>
    /// <param name="args">IN_DATA에 넣을 값 목록.</param>
    /// <returns>SpResult를 담은 DbResult.</returns>
    /// <example><code>
    /// // OracleDB.CallProc1("SP_SENSOR_GET", "A01", "2024-01-01") 동일
    /// DbResult<SpResult> r = await ctx.CallSpArgsAsync("SP_SENSOR_GET",
    ///     "A01", "2024-01-01", "2024-12-31");
    /// </code></example>
    public Task<DbResult<SpResult>> CallSpArgsAsync(
        string spName,
        CancellationToken ct = default,
        params object?[] args)
    {
        var inData = BuildInDataSelect(args);
        return CallSpAsync(spName, DbParam.StandardSp(inData), ct);
    }

    // §8 ─ Oracle 전용 — SpMutiSave (OracleDB.Sp_MutiSave 계승)
    // ─────────────────────────────────────────────────────────────────
    // 원본 OracleDB.Sp_MutiSave 구조:
    //   DataTable 행을 순회하며 조건 검사 후 SP 반복 호출
    //   트랜잭션 내에서 일괄 처리

    /// <summary>
    /// DataTable 행을 조건 필터 후 SP로 일괄 저장합니다.
    /// OracleDB.Sp_MutiSave 현대화 버전.
    /// </summary>
    /// <param name="dt">처리할 DataTable.</param>
    /// <param name="spName">호출할 SP 이름.</param>
    /// <param name="whereConditions">행 필터 조건 (컬럼명:비교값 쌍 배열).</param>
    /// <param name="paramColumns">SP에 전달할 DataTable 컬럼명 배열.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>처리된 행 수를 담은 DbResult.</returns>
    /// <example><code>
    /// // OracleDB.Sp_MutiSave(dt, "SP_SENSOR_SAVE",
    /// //     new[]{"USE_YN:Y"}, new[]{"SENSOR_ID","SENSOR_VALUE"}) 동일
    /// DbResult<int> r = await ctx.SpMutiSaveAsync(dt, "SP_SENSOR_SAVE",
    ///     whereConditions: [("USE_YN", "Y")],
    ///     paramColumns:    ["SENSOR_ID", "SENSOR_VALUE"]);
    /// </code></example>
    public async Task<DbResult<int>> SpMutiSaveAsync(
        DataTable dt,
        string spName,
        (string Column, string Value)[] whereConditions,
        string[] paramColumns,
        CancellationToken ct = default)
    {
        if (dt.Rows.Count == 0) return DbResult<int>.Ok(0);

        var alreadyInTx = IsInTransaction;
        if (!alreadyInTx)
            await BeginTransactionAsync(ct).ConfigureAwait(false);

        int retVal = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            foreach (DataRow row in dt.Rows)
            {
                // 원본 whereChk 로직 — 모든 조건 일치 시만 실행
                bool match = whereConditions.All(w =>
                    row[w.Column]?.ToString() == w.Value);

                if (!match) continue;

                // 파라미터 조립 (원본: OracleDbType.Varchar2 고정)
                var ps = paramColumns.Select(col =>
                    DbParam.In(col, row[col]?.ToString() ?? string.Empty,
                        DbParamType.VarChar)).ToArray();

                await using var cmd = BuildCommand(
                    spName, CommandType.StoredProcedure, ps);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                retVal++;
            }

            if (!alreadyInTx)
                await CommitAsync().ConfigureAwait(false);

            sw.Stop();
            Log($"SpMutiSave ({sw.ElapsedMilliseconds}ms): {retVal}행 처리 → {spName}");
            return DbResult<int>.Ok(retVal, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            if (!alreadyInTx)
                await RollbackAsync().ConfigureAwait(false);
            sw.Stop();
            LogError($"SpMutiSave 실패: {ex.Message} → {spName}");
            return DbResult<int>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §9 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// <summary>OracleCommand 생성 및 파라미터 설정.</summary>
    private OracleCommand BuildCommand(
        string sql,
        CommandType commandType,
        DbParam[]? parameters)
    {
        var cmd = new OracleCommand(sql, (OracleConnection)Connection!)
        {
            CommandType = commandType,
            CommandTimeout = _cfg.CommandTimeoutSec,
            // OracleDB 원본 패턴: BindByName = true (파라미터 이름으로 바인딩)
            BindByName = true,
        };

        if (Transaction is OracleTransaction oraTrn)
            cmd.Transaction = oraTrn;

        if (parameters is not null)
            AttachParameters(cmd, parameters);

        return cmd;
    }

    /// <summary>
    /// DbParam[] → OracleParameter[] 변환 후 커맨드에 부착.
    /// OracleHelper.AttachParameters 현대화 버전.
    /// </summary>
    private static void AttachParameters(OracleCommand cmd, DbParam[] parameters)
    {
        foreach (var p in parameters)
        {
            var op = new OracleParameter
            {
                ParameterName = p.Name,
                Direction = p.Direction,
                Value = p.Value ?? DBNull.Value,
            };

            if (p.Size > 0) op.Size = p.Size;

            // DbParamType → OracleDbType 매핑
            // 원본 OracleDB.CallProc 참조:
            //   "OUT_RETURNCODE" → Varchar2, 10
            //   "OUT_RETURNMSG"  → Varchar2, 200
            //   "OUT_CURSOR"     → RefCursor
            op.OracleDbType = p.ParamType switch
            {
                DbParamType.VarChar => OracleDbType.Varchar2,
                DbParamType.Char => OracleDbType.Char,
                DbParamType.Text => OracleDbType.Clob,
                DbParamType.TinyInt => OracleDbType.Byte,
                DbParamType.SmallInt => OracleDbType.Int16,
                DbParamType.Int => OracleDbType.Int32,
                DbParamType.BigInt => OracleDbType.Int64,
                DbParamType.Float => OracleDbType.Single,
                DbParamType.Double => OracleDbType.Double,
                DbParamType.Decimal => OracleDbType.Decimal,
                DbParamType.Date => OracleDbType.Date,
                DbParamType.DateTime => OracleDbType.TimeStamp,
                DbParamType.DateTimeOffset => OracleDbType.TimeStampTZ,
                DbParamType.Boolean => OracleDbType.Byte,     // Oracle Boolean 없음 → Byte
                DbParamType.Guid => OracleDbType.Raw,
                DbParamType.Binary => OracleDbType.Blob,
                DbParamType.Cursor => OracleDbType.RefCursor, // OUT_CURSOR
                _ => OracleDbType.Varchar2,  // Auto → Varchar2 기본
            };

            cmd.Parameters.Add(op);
        }
    }

    /// <summary>OUT 파라미터 값을 문자열로 추출.</summary>
    private static string GetOutParam(OracleCommand cmd, string name)
    {
        if (!cmd.Parameters.Contains(name)) return string.Empty;

        var val = cmd.Parameters[name].Value;

        // OracleString 타입 전용 처리
        if (val is OracleString oracleStr)
            return oracleStr.IsNull ? string.Empty : oracleStr.Value;

        // DBNull / null 제외 후 일반 ToString
        if (val is null || val is DBNull) return string.Empty;

        return val.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 가변 인수 → IN_DATA SELECT 구문 조립.
    /// OracleDB.CallProc1 패턴 계승.
    /// 예: ("A01", "2024") → "SELECT 'A01','2024' FROM DUAL"
    /// </summary>
    private static string BuildInDataSelect(object?[] args)
    {
        if (args.Length == 0)
            return "SELECT '' FROM DUAL";

        var parts = args.Select(a =>
            a is null || a.ToString() == string.Empty
                ? "''"
                : $"'{a.ToString()!.Trim()}'");

        return $"SELECT {string.Join(",", parts)} FROM DUAL";
    }
}