// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Helpers/DbHelper.cs
//  역할: OracleHelper + OracleDB 패턴 범용화 정적 헬퍼
//        IDbConnection 기반으로 DB 벤더에 독립적으로 동작한다.
//
//  원본 참조:
//    OracleHelper.cs  — PrepareCommand / AttachParameters
//                       ExecuteNonQuery / ExecuteDataset / ExecuteReader
//                       SP 파라미터 캐싱 (Hashtable → ConcurrentDictionary)
//    OracleDB.cs      — CallProc / CallProc1 / Sp_Save
//                       Cmd_Select / Cmd_Save / DataSearch / Sp_MutiSave
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성 (OracleHelper + OracleDB 범용화)
//  v1.0.1  2025-05-19  로그 Action 델리게이트 주입 방식 추가 (ExtraInfoHandler 등)
//                      CreateAdapter 제거 → IDataReader + DataTable.Load 패턴 교체
// ══════════════════════════════════════════════════════════════════════

using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using lssLib.DB.Core;
//using lssLib.Log;

namespace lssLib.DB.Helpers;

/// <summary>
/// DB 작업 범용 정적 헬퍼.
/// IDbConnection 기반으로 Oracle / MSSQL / MySQL / SQLite 모두 동작한다.
/// </summary>
/// <remarks>
/// 레거시 호환 계층 — DbContextBase / RepositoryBase 사용 불가 시 직접 호출한다.
/// 신규 코드에서는 IDbContext / IRepository 계층 사용을 권장한다.
/// <code>
/// // 직접 사용 예
/// using var conn = new OracleConnection(connStr);
/// DataSet ds = DbHelper.ExecuteDataset(conn, CommandType.Text, "SELECT * FROM SENSOR");
///
/// // SP 호출 (OracleDB.CallProc 패턴)
/// DbResult&lt;SpResult&gt; r = await DbHelper.CallSpAsync(conn, "SP_SENSOR_GET",
///     DbParam.StandardSp("SELECT '2024' FROM DUAL"));
/// </code>
/// </remarks>
public static class DbHelper
{
    // §1 ─ 설정
    // ─────────────────────────────────────────────────────────────────

    /// <summary>기본 커맨드 타임아웃 (초). OracleHelper.CmdTimeout 대응.</summary>
    public static int DefaultCommandTimeoutSec { get; set; } = 180;

    // §1-1 ─ 로그 Action 델리게이트 (기본: lssLib.Log, 추가 핸들러 선택 등록)
    // ─────────────────────────────────────────────────────────────────
    //
    //  기본값: lssLib.Log 자동 사용 (별도 설정 불필요)
    //  추가 핸들러 등록 시 lssLib.Log + 추가 핸들러 둘 다 호출됨
    //
    //  파라미터: (string source, string message)
    //    source  : 로그 발생 위치 (예: "DB.Helper", "DB.Oracle")
    //    message : 로그 메시지
    //
    //  등록 예:
    //    // WPF 상태바에도 표시
    //    DbHelper.ExtraInfoHandler  = (src, msg) =>
    //        Dispatcher.Invoke(() => TxtStatus.Text = $"[{src}] {msg}");
    //
    //    // 추가 파일 로그
    //    DbHelper.ExtraErrorHandler = (src, msg) => MyFileLog.Write(src, msg);
    //
    //    // 해제
    //    DbHelper.ExtraInfoHandler  = null;

    /// <summary>
    /// 정보 로그 추가 핸들러.
    /// 기본 lssLib.Log 외에 추가로 호출할 Action. null이면 lssLib.Log만 동작.
    /// </summary>
    /// <example><code>
    /// DbHelper.ExtraInfoHandler = (src, msg) => TxtStatus.Text = $"[{src}] {msg}";
    /// </code></example>
    public static Action<string, string>? ExtraInfoHandler { get; set; }

    /// <summary>
    /// 경고 로그 추가 핸들러.
    /// 기본 lssLib.Log 외에 추가로 호출할 Action. null이면 lssLib.Log만 동작.
    /// </summary>
    public static Action<string, string>? ExtraWarnHandler { get; set; }

    /// <summary>
    /// 오류 로그 추가 핸들러.
    /// 기본 lssLib.Log 외에 추가로 호출할 Action. null이면 lssLib.Log만 동작.
    /// </summary>
    /// <example><code>
    /// DbHelper.ExtraErrorHandler = (src, msg) =>
    ///     MessageBox.Show($"DB 오류: {msg}", src, MessageBoxButton.OK, MessageBoxImage.Error);
    /// </code></example>
    public static Action<string, string>? ExtraErrorHandler { get; set; }

    // §2 ─ 파라미터 캐시 (OracleHelper.paramCache 범용화)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SP 파라미터 캐시.
    /// OracleHelper.Hashtable → ConcurrentDictionary 로 스레드 안전하게 개선.
    /// 키: "ConnectionString:SpName"
    /// </summary>
    private static readonly ConcurrentDictionary<string, DbParam[]> _paramCache = new();

    /// <summary>
    /// SP 파라미터를 캐시에 저장합니다. (OracleHelper.CacheParameterSet 대응)
    /// </summary>
    /// <param name="connectionString">연결 문자열.</param>
    /// <param name="commandText">SP 이름 또는 SQL.</param>
    /// <param name="parameters">캐시할 파라미터 배열.</param>
    public static void CacheParameterSet(
        string connectionString,
        string commandText,
        DbParam[] parameters)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentNullException(nameof(commandText));

        _paramCache[$"{connectionString}:{commandText}"] = parameters;
    }

    /// <summary>
    /// 캐시된 SP 파라미터를 가져옵니다. (OracleHelper.GetCachedParameterSet 대응)
    /// </summary>
    /// <param name="connectionString">연결 문자열.</param>
    /// <param name="commandText">SP 이름 또는 SQL.</param>
    /// <returns>캐시된 파라미터 배열 복사본. 없으면 null.</returns>
    public static DbParam[]? GetCachedParameterSet(
        string connectionString,
        string commandText)
    {
        var key = $"{connectionString}:{commandText}";
        return _paramCache.TryGetValue(key, out var cached)
            ? [.. cached]   // 복사본 반환 (원본 보호)
            : null;
    }

    /// <summary>전체 파라미터 캐시를 비웁니다.</summary>
    public static void ClearCache() => _paramCache.Clear();

    // §3 ─ ExecuteNonQuery (OracleHelper.ExecuteNonQuery 범용화)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQL / SP를 실행하고 영향 받은 행 수를 반환합니다.
    /// OracleHelper.ExecuteNonQuery 범용화.
    /// </summary>
    /// <param name="connection">DB 연결.</param>
    /// <param name="commandType">Text / StoredProcedure.</param>
    /// <param name="commandText">SQL 문 또는 SP 이름.</param>
    /// <param name="parameters">파라미터 배열 (없으면 null).</param>
    /// <returns>영향 받은 행 수.</returns>
    /// <exception cref="DbException">실행 실패 시.</exception>
    public static int ExecuteNonQuery(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentNullException(nameof(commandText));

        bool mustClose = EnsureOpen(connection);
        try
        {
            using var cmd = PrepareCommand(connection, null, commandType,
                commandText, parameters);
            return cmd.ExecuteNonQuery();
        }
        finally
        {
            if (mustClose) connection.Close();
        }
    }

    /// <summary>
    /// 트랜잭션 내에서 SQL / SP를 실행하고 영향 받은 행 수를 반환합니다.
    /// OracleHelper.ExecuteNonQuery(transaction, ...) 대응.
    /// </summary>
    public static int ExecuteNonQuery(
        IDbTransaction transaction,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null)
    {
        if (transaction is null) throw new ArgumentNullException(nameof(transaction));
        if (transaction.Connection is null)
            throw new ArgumentException("트랜잭션이 이미 종료되었습니다.", nameof(transaction));

        using var cmd = PrepareCommand(transaction.Connection, transaction,
            commandType, commandText, parameters);
        return cmd.ExecuteNonQuery();
    }

    // §4 ─ ExecuteDataset (OracleHelper.ExecuteDataset 범용화)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQL / SP를 실행하고 DataSet을 반환합니다.
    /// OracleHelper.ExecuteDataset + OracleDB.Cmd_Select 범용화.
    /// </summary>
    /// <param name="connection">DB 연결.</param>
    /// <param name="commandType">Text / StoredProcedure.</param>
    /// <param name="commandText">SQL 문 또는 SP 이름.</param>
    /// <param name="parameters">파라미터 배열 (없으면 null).</param>
    /// <returns>결과 DataSet.</returns>
    public static DataSet ExecuteDataset(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null)
    {
        if (connection is null)
            throw new ArgumentNullException(nameof(connection));

        bool mustClose = EnsureOpen(connection);
        try
        {
            using var cmd = PrepareCommand(connection, null,
                commandType, commandText, parameters);
            using var reader = cmd.ExecuteReader();

            var ds = new DataSet();
            // 결과셋이 여러 개일 경우 (NextResult) 모두 DataTable로 적재
            do
            {
                var dt = new DataTable();
                dt.Load(reader);
                ds.Tables.Add(dt);
            }
            while (!reader.IsClosed && reader.NextResult());

            return ds;
        }
        finally
        {
            if (mustClose) connection.Close();
        }
    }

    /// <summary>
    /// SQL / SP를 실행하고 DataTable을 반환합니다.
    /// </summary>
    public static DataTable ExecuteDataTable(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null)
    {
        var ds = ExecuteDataset(connection, commandType, commandText, parameters);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    // §5 ─ ExecuteReader (OracleHelper.ExecuteReader 범용화)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQL / SP를 실행하고 IDataReader를 반환합니다.
    /// OracleHelper.ExecuteReader 범용화.
    /// </summary>
    /// <param name="connection">DB 연결.</param>
    /// <param name="commandType">Text / StoredProcedure.</param>
    /// <param name="commandText">SQL 문 또는 SP 이름.</param>
    /// <param name="parameters">파라미터 배열 (없으면 null).</param>
    /// <returns>IDataReader (사용 후 반드시 Dispose).</returns>
    /// <remarks>
    /// 연결은 Reader가 닫힐 때 자동으로 닫힌다 (CommandBehavior.CloseConnection).
    /// </remarks>
    public static IDataReader ExecuteReader(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null)
    {
        if (connection is null)
            throw new ArgumentNullException(nameof(connection));

        EnsureOpen(connection);
        var cmd = PrepareCommand(connection, null,
            commandType, commandText, parameters);

        // CloseConnection → Reader 닫힐 때 연결 자동 해제
        return cmd.ExecuteReader(CommandBehavior.CloseConnection);
    }

    // §6 ─ ExecuteScalar
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQL을 실행하고 첫 번째 행 첫 번째 열의 스칼라 값을 반환합니다.
    /// </summary>
    public static object? ExecuteScalar(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null)
    {
        if (connection is null)
            throw new ArgumentNullException(nameof(connection));

        bool mustClose = EnsureOpen(connection);
        try
        {
            using var cmd = PrepareCommand(connection, null,
                commandType, commandText, parameters);
            var result = cmd.ExecuteScalar();
            return result is DBNull ? null : result;
        }
        finally
        {
            if (mustClose) connection.Close();
        }
    }

    // §7 ─ CallSp (OracleDB.CallProc 범용화)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 저장 프로시저를 호출하고 SpResult를 반환합니다.
    /// OracleDB.CallProc (IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR) 범용화.
    /// </summary>
    /// <param name="connection">DB 연결.</param>
    /// <param name="spName">SP 이름.</param>
    /// <param name="parameters">파라미터 배열 (DbParam.StandardSp() 활용 권장).</param>
    /// <returns>SpResult를 담은 DbResult.</returns>
    /// <example><code>
    /// DbResult&lt;SpResult&gt; r = DbHelper.CallSp(conn, "SP_SENSOR_GET",
    ///     DbParam.StandardSp("SELECT '001' FROM DUAL"));
    /// if (r.IsOk &amp;&amp; r.Value!.IsSuccess)
    ///     grid.DataSource = r.Value.Table;
    /// </code></example>
    public static DbResult<SpResult> CallSp(
        IDbConnection connection,
        string spName,
        DbParam[]? parameters = null)
    {
        if (connection is null)
            return DbResult<SpResult>.Fail("connection이 null입니다.");
        if (string.IsNullOrWhiteSpace(spName))
            return DbResult<SpResult>.Fail("SP 이름이 비어 있습니다.");

        var sw = Stopwatch.StartNew();
        bool mustClose = false;

        try
        {
            mustClose = EnsureOpen(connection);

            using var cmd = PrepareCommand(connection, null,
                CommandType.StoredProcedure, spName, parameters);
            using var reader = cmd.ExecuteReader();

            // OUT_CURSOR 결과를 DataTable로 적재 (reader → DataTable.Load)
            var dt = new DataTable();
            dt.Load(reader);
            reader.Close();

            // OUT_RETURNCODE / OUT_RETURNMSG 추출 (reader 닫힌 후 접근 가능)
            var returnCode = GetOutParamValue(cmd, "OUT_RETURNCODE");
            var returnMsg = GetOutParamValue(cmd, "OUT_RETURNMSG");

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

            Log($"CallSp 완료 ({sw.ElapsedMilliseconds}ms): {spName}");
            return DbResult<SpResult>.Ok(sp, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"CallSp 예외 [{spName}]: {ex.Message}");
            return DbResult<SpResult>.Error(ex, sw.ElapsedMilliseconds);
        }
        finally
        {
            if (mustClose) connection.Close();
        }
    }

    /// <summary>
    /// CallSp 비동기 버전.
    /// </summary>
    public static Task<DbResult<SpResult>> CallSpAsync(
        IDbConnection connection,
        string spName,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
        => Task.Run(() => CallSp(connection, spName, parameters), ct);

    // §8 ─ CallProc1 — params 가변인수 (OracleDB.CallProc1 범용화)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 가변 인수를 IN_DATA SELECT 구문으로 자동 조립해 SP를 호출합니다.
    /// OracleDB.CallProc1(spName, pram...) 범용화.
    /// </summary>
    /// <param name="connection">DB 연결.</param>
    /// <param name="spName">SP 이름.</param>
    /// <param name="args">IN_DATA에 넣을 값 목록.</param>
    /// <returns>SpResult를 담은 DbResult.</returns>
    /// <example><code>
    /// // OracleDB.CallProc1("SP_SENSOR_GET", "A01", "2024-01-01") 동일
    /// DbResult&lt;SpResult&gt; r = DbHelper.CallSpArgs(conn, "SP_SENSOR_GET",
    ///     "A01", "2024-01-01");
    /// </code></example>
    public static DbResult<SpResult> CallSpArgs(
        IDbConnection connection,
        string spName,
        params object?[] args)
    {
        // OracleDB.CallProc1 패턴: 값들을 SELECT '...',... FROM DUAL 구문으로 조립
        var inData = BuildInDataSelect(args);
        return CallSp(connection, spName, DbParam.StandardSp(inData));
    }

    /// <summary>CallSpArgs 비동기 버전.</summary>
    public static Task<DbResult<SpResult>> CallSpArgsAsync(
        IDbConnection connection,
        string spName,
        params object?[] args)
        => Task.Run(() => CallSpArgs(connection, spName, args));

    // §9 ─ DataSearch (OracleDB.DataSearch 계승)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// DataTable에 WHERE 조건을 적용한 필터링 결과를 새 DataTable로 반환합니다.
    /// OracleDB.DataSearch 계승.
    /// </summary>
    /// <param name="source">원본 DataTable.</param>
    /// <param name="filterExpression">DataTable.Select() 필터 식 (예: "SENSOR_ID = 42").</param>
    /// <returns>필터링된 새 DataTable.</returns>
    /// <example><code>
    /// DataTable filtered = DbHelper.DataSearch(dt, "USE_YN = 'Y' AND PLANT_CD = 'A01'");
    /// </code></example>
    public static DataTable DataSearch(DataTable source, string filterExpression)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(filterExpression))
            return source.Copy();

        var rows = source.Select(filterExpression);
        var result = source.Clone(); // 컬럼 구조만 복사

        foreach (var row in rows)
            result.ImportRow(row);

        return result;
    }

    // §10 ─ Async 래퍼 — ExecuteNonQuery / ExecuteDataset
    // ─────────────────────────────────────────────────────────────────

    /// <summary>ExecuteNonQuery 비동기 버전.</summary>
    public static Task<int> ExecuteNonQueryAsync(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
        => Task.Run(() =>
            ExecuteNonQuery(connection, commandType, commandText, parameters), ct);

    /// <summary>ExecuteDataset 비동기 버전.</summary>
    public static Task<DataSet> ExecuteDatasetAsync(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
        => Task.Run(() =>
            ExecuteDataset(connection, commandType, commandText, parameters), ct);

    /// <summary>ExecuteDataTable 비동기 버전.</summary>
    public static Task<DataTable> ExecuteDataTableAsync(
        IDbConnection connection,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
        => Task.Run(() =>
            ExecuteDataTable(connection, commandType, commandText, parameters), ct);

    // §11 ─ 내부 유틸리티 (OracleHelper private 메서드 범용화)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IDbCommand 준비. OracleHelper.PrepareCommand 범용화.
    /// 연결·트랜잭션·CommandType·파라미터를 커맨드에 설정한다.
    /// </summary>
    private static IDbCommand PrepareCommand(
        IDbConnection connection,
        IDbTransaction? transaction,
        CommandType commandType,
        string commandText,
        DbParam[]? parameters)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = commandText;
        cmd.CommandType = commandType;
        cmd.CommandTimeout = DefaultCommandTimeoutSec;

        if (transaction is not null)
            cmd.Transaction = transaction;

        if (parameters is not null)
            AttachParameters(cmd, parameters);

        return cmd;
    }

    /// <summary>
    /// 파라미터를 커맨드에 부착. OracleHelper.AttachParameters 범용화.
    /// InputOutput / Input 방향에서 null 값을 DBNull.Value로 대체한다.
    /// </summary>
    private static void AttachParameters(IDbCommand cmd, DbParam[] parameters)
    {
        foreach (var p in parameters)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = p.Name;
            param.Direction = p.Direction;

            // null → DBNull 변환 (OracleHelper.AttachParameters 동일 동작)
            param.Value = (p.Direction is ParameterDirection.Input
                           or ParameterDirection.InputOutput)
                          && (p.Value is null or DBNull)
                          ? DBNull.Value
                          : p.Value ?? DBNull.Value;

            if (p.Size > 0) param.Size = p.Size;

            cmd.Parameters.Add(param);
        }
    }

    /// <summary>
    /// 연결이 닫혀 있으면 열고, 열었으면 true 반환 (호출자가 닫아야 함).
    /// OracleHelper.PrepareCommand 내 연결 상태 처리 범용화.
    /// </summary>
    private static bool EnsureOpen(IDbConnection connection)
    {
        if (connection.State == ConnectionState.Open) return false;
        connection.Open();
        return true;
    }

    /// <summary>
    /// OUT 파라미터 값을 문자열로 추출.
    /// OracleDB.CallProc의 OUT_RETURNCODE / OUT_RETURNMSG 추출 패턴 범용화.
    /// </summary>
    private static string GetOutParamValue(IDbCommand cmd, string paramName)
    {
        if (cmd.Parameters.Contains(paramName) &&
            cmd.Parameters[paramName] is IDbDataParameter p &&
            p.Value is not null and not DBNull)
            return p.Value.ToString() ?? string.Empty;
        return string.Empty;
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
            a is null or "" ? "''" : $"'{a.ToString()!.Trim()}'");

        return $"SELECT {string.Join(",", parts)} FROM DUAL";
    }

    // §12 ─ 로그 — 기본 lssLib.Log + 추가 Action 핸들러 동시 호출
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 정보 로그 기록.
    /// lssLib.Log 기본 호출 후 ExtraInfoHandler 가 등록되어 있으면 추가 호출.
    /// </summary>
    private static void Log(string message)
    {
    //    LogManager.Instance.Info("DB.Helper", message);
        ExtraInfoHandler?.Invoke("DB.Helper", message);
    }

    /// <summary>
    /// 경고 로그 기록.
    /// lssLib.Log 기본 호출 후 ExtraWarnHandler 가 등록되어 있으면 추가 호출.
    /// </summary>
    private static void LogWarn(string message)
    {
    //    LogManager.Instance.Warn("DB.Helper", message);
        ExtraWarnHandler?.Invoke("DB.Helper", message);
    }

    /// <summary>
    /// 오류 로그 기록.
    /// lssLib.Log 기본 호출 후 ExtraErrorHandler 가 등록되어 있으면 추가 호출.
    /// </summary>
    private static void LogError(string message)
    {
    //    LogManager.Instance.Error("DB.Helper", message);
        ExtraErrorHandler?.Invoke("DB.Helper", message);
    }
}