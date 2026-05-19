// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Core/DbResult.cs
//  역할: DB 실행 결과를 담는 범용 값 타입 (성공/실패/예외 통합)
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;

namespace lssLib.DB.Core;

// §1 ─ 결과 상태 열거형
// ─────────────────────────────────────────────────────────────────────

/// <summary>DB 실행 결과 상태.</summary>
public enum DbResultStatus
{
    /// <summary>성공.</summary>
    Ok,

    /// <summary>실패 (DB 반환 코드 0 또는 실행 오류).</summary>
    Fail,

    /// <summary>예외 발생.</summary>
    Error,

    /// <summary>연결 없음 또는 타임아웃.</summary>
    Timeout,
}

// §2 ─ 제네릭 결과 타입
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// DB 실행 결과를 담는 범용 값 타입.
/// </summary>
/// <typeparam name="T">결과 데이터 타입 (List<T>, int, SpResult 등).</typeparam>
/// <example><code>
/// // 조회 결과
/// DbResult<List<SensorRow>> r = await repo.QueryAsync<SensorRow>(query);
/// if (r.IsOk) grid.ItemsSource = r.Value;
///
/// // 실행 결과
/// DbResult<int> r2 = await repo.ExecuteAsync(sql);
/// if (!r2.IsOk) LogManager.Instance.Error("DB", r2.Message);
/// </code></example>
public readonly record struct DbResult<T>
{
    // §2-1 ─ 프로퍼티
    /// <summary>실행 성공 여부.</summary>
    public bool IsOk { get; init; }

    /// <summary>결과 상태.</summary>
    public DbResultStatus Status { get; init; }

    /// <summary>결과 데이터 (실패 시 default).</summary>
    public T? Value { get; init; }

    /// <summary>결과 메시지 (성공 시 빈 문자열, 실패 시 사유).</summary>
    public string Message { get; init; }

    /// <summary>발생 예외 (정상 실행 시 null).</summary>
    public Exception? Exception { get; init; }

    /// <summary>실행 소요 시간 (ms).</summary>
    public long ElapsedMs { get; init; }

    // §2-2 ─ 팩토리 메서드

    /// <summary>성공 결과 생성.</summary>
    /// <param name="value">결과 데이터.</param>
    /// <param name="elapsedMs">실행 소요 시간 (ms).</param>
    public static DbResult<T> Ok(T value, long elapsedMs = 0) => new()
    {
        IsOk = true,
        Status = DbResultStatus.Ok,
        Value = value,
        Message = string.Empty,
        ElapsedMs = elapsedMs,
    };

    /// <summary>실패 결과 생성.</summary>
    /// <param name="message">실패 사유 메시지.</param>
    /// <param name="elapsedMs">실행 소요 시간 (ms).</param>
    public static DbResult<T> Fail(string message, long elapsedMs = 0) => new()
    {
        IsOk = false,
        Status = DbResultStatus.Fail,
        Value = default,
        Message = message,
        ElapsedMs = elapsedMs,
    };

    /// <summary>예외 결과 생성.</summary>
    /// <param name="ex">발생 예외.</param>
    /// <param name="elapsedMs">실행 소요 시간 (ms).</param>
    public static DbResult<T> Error(Exception ex, long elapsedMs = 0) => new()
    {
        IsOk = false,
        Status = DbResultStatus.Error,
        Value = default,
        Message = ex.Message,
        Exception = ex,
        ElapsedMs = elapsedMs,
    };

    /// <summary>타임아웃 결과 생성.</summary>
    /// <param name="message">타임아웃 메시지.</param>
    /// <param name="elapsedMs">실행 소요 시간 (ms).</param>
    public static DbResult<T> Timeout(string message, long elapsedMs = 0) => new()
    {
        IsOk = false,
        Status = DbResultStatus.Timeout,
        Value = default,
        Message = message,
        ElapsedMs = elapsedMs,
    };

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{Status}] {(IsOk ? "OK" : Message)} ({ElapsedMs}ms)";
}

// §3 ─ SP 실행 결과 (저장 프로시저 전용)
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// 저장 프로시저 실행 결과.
/// OracleDB 패턴 (OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR) 범용화.
/// </summary>
/// <example><code>
/// DbResult<SpResult> r = await repo.CallSpAsync("SP_SENSOR_GET", inData);
/// if (r.IsOk && r.Value!.IsSuccess)
///     grid.ItemsSource = r.Value.Table?.DefaultView;
/// </code></example>
public sealed record SpResult
{
    /// <summary>DB 반환 코드 ("1" = 성공, 그 외 실패).</summary>
    public string ReturnCode { get; init; } = string.Empty;

    /// <summary>DB 반환 메시지.</summary>
    public string ReturnMessage { get; init; } = string.Empty;

    /// <summary>SELECT 결과 테이블 (없으면 null).</summary>
    public DataTable? Table { get; init; }

    /// <summary>SP 반환 코드 "1" 여부로 성공 판별.</summary>
    public bool IsSuccess => ReturnCode == "1";

    /// <summary>성공 SpResult 생성.</summary>
    public static SpResult Ok(string message = "", DataTable? table = null) => new()
    {
        ReturnCode = "1",
        ReturnMessage = message,
        Table = table,
    };

    /// <summary>실패 SpResult 생성.</summary>
    public static SpResult Fail(string code, string message) => new()
    {
        ReturnCode = code,
        ReturnMessage = message,
        Table = null,
    };
}