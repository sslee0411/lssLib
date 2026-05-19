// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Core/DbException.cs
//  역할: DB 실행 중 발생하는 전용 예외 타입 및 오류 코드 정의
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.DB.Core;

// §1 ─ 오류 코드 열거형
// ─────────────────────────────────────────────────────────────────────

/// <summary>DB 실행 오류 코드.</summary>
public enum DbErrorCode
{
    /// <summary>알 수 없는 오류.</summary>
    Unknown = 0,

    // 연결 관련
    /// <summary>연결 실패.</summary>
    ConnectionFailed = 1001,
    /// <summary>연결 타임아웃.</summary>
    ConnectionTimeout = 1002,
    /// <summary>연결 문자열 오류.</summary>
    InvalidConnectionString = 1003,

    // 쿼리/실행 관련
    /// <summary>SQL 문법 오류.</summary>
    InvalidQuery = 2001,
    /// <summary>실행 타임아웃.</summary>
    CommandTimeout = 2002,
    /// <summary>파라미터 오류.</summary>
    InvalidParameter = 2003,
    /// <summary>저장 프로시저 없음.</summary>
    SpNotFound = 2004,

    // 트랜잭션 관련
    /// <summary>트랜잭션 시작 실패.</summary>
    TransactionFailed = 3001,
    /// <summary>커밋 실패.</summary>
    CommitFailed = 3002,
    /// <summary>롤백 실패.</summary>
    RollbackFailed = 3003,

    // 데이터 관련
    /// <summary>중복 키 오류.</summary>
    DuplicateKey = 4001,
    /// <summary>외래 키 제약 위반.</summary>
    ForeignKeyViolation = 4002,
    /// <summary>데이터 변환 오류.</summary>
    DataConversion = 4003,

    // SP 반환 관련
    /// <summary>SP 반환 코드 오류 (OUT_RETURNCODE != "1").</summary>
    SpReturnError = 5001,

    // InfluxDB 전용
    /// <summary>Flux 쿼리 문법 오류.</summary>
    FluxQueryError = 6001,
    /// <summary>Line Protocol 형식 오류.</summary>
    LineProtocolError = 6002,
    /// <summary>버킷 없음.</summary>
    BucketNotFound = 6003,
}

// §2 ─ DB 전용 예외
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// DB 실행 중 발생하는 전용 예외.
/// DbResult.Error() 팩토리에서 내부적으로 사용한다.
/// </summary>
/// <example><code>
/// try
/// {
///     await context.ExecuteAsync(sql, params);
/// }
/// catch (DbException ex) when (ex.ErrorCode == DbErrorCode.DuplicateKey)
/// {
///     LogManager.Instance.Warn("DB", $"중복 키: {ex.Message}");
/// }
/// </code></example>
public sealed class DbException : Exception
{
    /// <summary>오류 코드.</summary>
    public DbErrorCode ErrorCode { get; }

    /// <summary>오류 발생 Provider.</summary>
    public DbProviderType ProviderType { get; }

    /// <summary>실행 중이던 SQL 또는 SP 이름 (알 수 없으면 빈 문자열).</summary>
    public string CommandText { get; }

    /// <param name="errorCode">오류 코드.</param>
    /// <param name="providerType">오류 발생 Provider.</param>
    /// <param name="message">오류 메시지.</param>
    /// <param name="commandText">실행 중이던 SQL/SP 이름.</param>
    /// <param name="innerException">원본 예외.</param>
    public DbException(
        DbErrorCode errorCode,
        DbProviderType providerType,
        string message,
        string commandText = "",
        Exception? innerException = null)
        : base($"[{providerType}][{errorCode}] {message}", innerException)
    {
        ErrorCode = errorCode;
        ProviderType = providerType;
        CommandText = commandText;
    }

    // §2-1 ─ 자주 쓰는 팩토리

    /// <summary>연결 실패 예외 생성.</summary>
    public static DbException ConnectionFailed(DbProviderType provider, Exception inner) =>
        new(DbErrorCode.ConnectionFailed, provider,
            $"DB 연결 실패: {inner.Message}", innerException: inner);

    /// <summary>커맨드 타임아웃 예외 생성.</summary>
    public static DbException CommandTimeout(DbProviderType provider, string sql) =>
        new(DbErrorCode.CommandTimeout, provider,
            "쿼리 실행 타임아웃", commandText: sql);

    /// <summary>SP 반환 오류 예외 생성.</summary>
    public static DbException SpReturnError(DbProviderType provider,
        string spName, string returnCode, string returnMsg) =>
        new(DbErrorCode.SpReturnError, provider,
            $"SP 반환 오류 [{returnCode}]: {returnMsg}", commandText: spName);

    /// <summary>파라미터 오류 예외 생성.</summary>
    public static DbException InvalidParameter(DbProviderType provider,
        string paramName, string reason) =>
        new(DbErrorCode.InvalidParameter, provider,
            $"파라미터 오류 '{paramName}': {reason}");

    /// <summary>InfluxDB Line Protocol 오류 예외 생성.</summary>
    public static DbException LineProtocolError(string detail) =>
        new(DbErrorCode.LineProtocolError, DbProviderType.InfluxDB,
            $"Line Protocol 형식 오류: {detail}");
}