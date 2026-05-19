// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Core/DbConfig.cs
//  역할: DB 연결 설정을 담는 불변 설정 타입 (Provider별 파생)
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.DB.Core;

// §1 ─ DB 종류 열거형
// ─────────────────────────────────────────────────────────────────────

/// <summary>지원 DB 종류.</summary>
public enum DbProviderType
{
    /// <summary>InfluxDB v2.0 (시계열 DB).</summary>
    InfluxDB,

    /// <summary>Microsoft SQL Server.</summary>
    MsSql,

    /// <summary>Oracle Database.</summary>
    Oracle,

    /// <summary>MySQL / MariaDB.</summary>
    MySql,

    /// <summary>SQLite (파일 기반).</summary>
    Sqlite,
}

// §2 ─ 공통 연결 설정 (Base)
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// DB 연결 설정 기본 타입.
/// Provider별 파생 레코드에서 확장해서 사용한다.
/// </summary>
/// <example><code>
/// // 관계형 DB 공통
/// var cfg = new RelationalDbConfig(
///     DbProviderType.MsSql,
///     "Server=localhost;Database=IIoT;Integrated Security=true;",
///     commandTimeoutSec: 30);
///
/// // InfluxDB
/// var influxCfg = new InfluxDbConfig(
///     url:    "http://localhost:8086",
///     token:  "my-token",
///     org:    "my-org",
///     bucket: "sensor-data");
/// </code></example>
public abstract record DbConfigBase
{
    /// <summary>DB 종류.</summary>
    public DbProviderType ProviderType { get; init; }

    /// <summary>커맨드 타임아웃 (초, 기본값 30).</summary>
    public int CommandTimeoutSec { get; init; } = 30;

    /// <summary>연결 타임아웃 (초, 기본값 15).</summary>
    public int ConnectTimeoutSec { get; init; } = 15;

    /// <summary>최대 재시도 횟수 (기본값 3).</summary>
    public int MaxRetry { get; init; } = 3;

    /// <summary>재시도 간격 (ms, 기본값 500).</summary>
    public int RetryDelayMs { get; init; } = 500;

    /// <summary>설정 유효성 검사. 파생 클래스에서 override.</summary>
    /// <exception cref="DbConfigException">설정 값이 유효하지 않을 때.</exception>
    public virtual void Validate() { }
}

// §3 ─ 관계형 DB 설정 (MSSQL / Oracle / MySQL / SQLite 공통)
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// 관계형 DB 공통 연결 설정.
/// MSSQL / Oracle / MySQL / SQLite 에서 공용으로 사용한다.
/// </summary>
public sealed record RelationalDbConfig : DbConfigBase
{
    /// <summary>연결 문자열.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// 관계형 DB 설정 생성자.
    /// </summary>
    /// <param name="providerType">DB 종류.</param>
    /// <param name="connectionString">연결 문자열.</param>
    /// <param name="commandTimeoutSec">커맨드 타임아웃 (초).</param>
    public RelationalDbConfig(
        DbProviderType providerType,
        string connectionString,
        int commandTimeoutSec = 30)
    {
        ProviderType = providerType;
        ConnectionString = connectionString;
        CommandTimeoutSec = commandTimeoutSec;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new DbConfigException(ProviderType, "ConnectionString이 비어 있습니다.");
        if (CommandTimeoutSec <= 0)
            throw new DbConfigException(ProviderType, "CommandTimeoutSec는 1 이상이어야 합니다.");
    }
}

// §4 ─ InfluxDB v2.0 설정
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// InfluxDB v2.0 연결 설정.
/// HTTP REST API + Flux 쿼리 + Line Protocol 기반.
/// </summary>
public sealed record InfluxDbConfig : DbConfigBase
{
    /// <summary>InfluxDB 서버 URL (예: http://localhost:8086).</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>API 인증 토큰.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>조직 이름.</summary>
    public string Org { get; init; } = string.Empty;

    /// <summary>기본 버킷 이름.</summary>
    public string Bucket { get; init; } = string.Empty;

    /// <summary>
    /// InfluxDB 설정 생성자.
    /// </summary>
    /// <param name="url">서버 URL.</param>
    /// <param name="token">API 인증 토큰.</param>
    /// <param name="org">조직 이름.</param>
    /// <param name="bucket">기본 버킷 이름.</param>
    /// <param name="commandTimeoutSec">쿼리 타임아웃 (초).</param>
    public InfluxDbConfig(
        string url,
        string token,
        string org,
        string bucket,
        int commandTimeoutSec = 30)
    {
        ProviderType = DbProviderType.InfluxDB;
        Url = url;
        Token = token;
        Org = org;
        Bucket = bucket;
        CommandTimeoutSec = commandTimeoutSec;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            throw new DbConfigException(ProviderType, "InfluxDB URL이 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(Token))
            throw new DbConfigException(ProviderType, "InfluxDB Token이 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(Org))
            throw new DbConfigException(ProviderType, "InfluxDB Org가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(Bucket))
            throw new DbConfigException(ProviderType, "InfluxDB Bucket이 비어 있습니다.");
    }
}

// §5 ─ 설정 예외 (DbException.cs 에서 선언 전 선행 정의)
// ─────────────────────────────────────────────────────────────────────

/// <summary>DB 설정 오류 예외.</summary>
public sealed class DbConfigException : Exception
{
    /// <summary>오류 발생 Provider.</summary>
    public DbProviderType ProviderType { get; }

    /// <param name="providerType">오류 발생 Provider.</param>
    /// <param name="message">오류 메시지.</param>
    public DbConfigException(DbProviderType providerType, string message)
        : base($"[{providerType}] 설정 오류: {message}")
    {
        ProviderType = providerType;
    }
}