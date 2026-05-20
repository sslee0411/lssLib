// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Abstractions/DbContextBase.cs
//  역할: IDbContext 공통 구현 — 연결·트랜잭션·로그·재시도 기반 클래스
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using System.Diagnostics;
using lssLib.DB.Contracts;
using lssLib.DB.Core;
//using lssLib.Log;

namespace lssLib.DB.Abstractions;

/// <summary>
/// IDbContext 공통 구현 기반 클래스.
/// Provider별 구현체(OracleDbContext, MsSqlDbContext 등)에서 상속한다.
/// </summary>
/// <remarks>
/// 파생 클래스 최소 구현 예.
/// <code>
/// public sealed class OracleDbContext : DbContextBase
/// {
///     public OracleDbContext(RelationalDbConfig cfg) : base(cfg) { }
///
///     protected override IDbConnection CreateConnection()
///         => new OracleConnection(((RelationalDbConfig)Config).ConnectionString);
///
///     protected override Task<DbResult<DataTable>> QueryTableCoreAsync(
///         string sql, CommandType ct, DbParam[]? ps, CancellationToken token)
///         => OracleDbHelper.QueryTableAsync(Connection!, sql, ct, ps, token);
///
///     protected override Task<DbResult<int>> ExecuteCoreAsync(
///         string sql, CommandType ct, DbParam[]? ps, CancellationToken token)
///         => OracleDbHelper.ExecuteAsync(Connection!, sql, ct, ps, token);
///
///     protected override Task<DbResult<SpResult>> CallSpCoreAsync(
///         string spName, DbParam[]? ps, CancellationToken token)
///         => OracleDbHelper.CallSpAsync(Connection!, spName, ps, token);
/// }
/// </code>
/// </remarks>
public abstract class DbContextBase : IDbContext
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────
    /// <param name="config">DB 연결 설정.</param>
    /// <exception cref="DbConfigException">설정 유효성 검사 실패 시.</exception>
    protected DbContextBase(DbConfigBase config)
    {
        _config = config;
        config.Validate();
    }

    // §3 ─ 보호 프로퍼티 (파생 클래스 접근용)
    // ─────────────────────────────────────────────────────────────────
    /// <summary>DB 연결 설정.</summary>
    protected DbConfigBase Config => _config;
    private DbConfigBase _config;

    /// <summary>현재 열린 DB 연결 (OpenAsync 호출 전 null).</summary>
    protected IDbConnection? Connection => _connection;

    /// <summary>현재 트랜잭션 (BeginTransactionAsync 호출 전 null).</summary>
    protected IDbTransaction? Transaction => _transaction;

    // §4 ─ IDbContext 구현 — 상태 조회
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ConnectionState State =>
        _connection?.State ?? ConnectionState.Closed;

    /// <inheritdoc/>
    public bool IsInTransaction => _transaction is not null;

    /// <inheritdoc/>
    public abstract DbProviderType ProviderType { get; }

    // §5 ─ IDbContext 구현 — 연결 관리
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task OpenAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (State == ConnectionState.Open) return;

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (State == ConnectionState.Open) return;

            _connection = CreateConnection();

            // 재시도 로직
            int attempt = 0;
            while (true)
            {
                try
                {
                    if (_connection is IAsyncDisposable)
                        await OpenConnectionAsync(_connection, ct).ConfigureAwait(false);
                    else
                        _connection.Open();

                    Log($"DB 연결 성공 [{ProviderType}]");
                    return;
                }
                catch (Exception ex) when (attempt < _config.MaxRetry)
                {
                    attempt++;
                    LogWarn($"연결 재시도 {attempt}/{_config.MaxRetry}: {ex.Message}");
                    await Task.Delay(_config.RetryDelayMs, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogError($"DB 연결 실패: {ex.Message}");
                    throw new DbException(
                        DbErrorCode.ConnectionFailed, ProviderType,
                        ex.Message, innerException: ex);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync()
    {
        if (_connection is null || State == ConnectionState.Closed) return;

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _connection.Close();
            Log($"DB 연결 종료 [{ProviderType}]");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // §6 ─ IDbContext 구현 — 트랜잭션
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        EnsureConnected();

        if (IsInTransaction)
            throw new InvalidOperationException("이미 트랜잭션이 진행 중입니다.");

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _transaction = _connection!.BeginTransaction();
            Log("트랜잭션 시작");
        }
        catch (Exception ex)
        {
            LogError($"트랜잭션 시작 실패: {ex.Message}");
            throw new DbException(DbErrorCode.TransactionFailed, ProviderType,
                ex.Message, innerException: ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CommitAsync()
    {
        ThrowIfDisposed();
        if (_transaction is null)
            throw new InvalidOperationException("진행 중인 트랜잭션이 없습니다.");

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _transaction.Commit();
            _transaction.Dispose();
            _transaction = null;
            Log("트랜잭션 커밋");
        }
        catch (Exception ex)
        {
            LogError($"커밋 실패: {ex.Message}");
            throw new DbException(DbErrorCode.CommitFailed, ProviderType,
                ex.Message, innerException: ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync()
    {
        if (_transaction is null) return;

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = null;
            Log("트랜잭션 롤백");
        }
        catch (Exception ex)
        {
            LogError($"롤백 실패: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // §7 ─ IDbContext 구현 — 실행
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<DbResult<int>> ExecuteAsync(
        string sql,
        CommandType commandType = CommandType.Text,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        EnsureConnected();

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await ExecuteCoreAsync(sql, commandType, parameters, ct)
                .ConfigureAwait(false);
            sw.Stop();
            Log($"Execute ({sw.ElapsedMilliseconds}ms): {sql[..Math.Min(80, sql.Length)]}");
            return DbResult<int>.Ok(result.Value, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return DbResult<int>.Timeout("쿼리 취소됨", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"Execute 실패: {ex.Message} | SQL: {sql}");
            return DbResult<int>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<DbResult<DataTable>> QueryTableAsync(
        string sql,
        CommandType commandType = CommandType.Text,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        EnsureConnected();

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await QueryTableCoreAsync(sql, commandType, parameters, ct)
                .ConfigureAwait(false);
            sw.Stop();
            Log($"Query ({sw.ElapsedMilliseconds}ms): {sql[..Math.Min(80, sql.Length)]}");
            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return DbResult<DataTable>.Timeout("쿼리 취소됨", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"QueryTable 실패: {ex.Message} | SQL: {sql}");
            return DbResult<DataTable>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<DbResult<SpResult>> CallSpAsync(
        string spName,
        DbParam[]? parameters = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        EnsureConnected();

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await CallSpCoreAsync(spName, parameters, ct)
                .ConfigureAwait(false);
            sw.Stop();
            Log($"CallSP ({sw.ElapsedMilliseconds}ms): {spName}");
            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return DbResult<SpResult>.Timeout("SP 실행 취소됨", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"CallSP 실패: {ex.Message} | SP: {spName}");
            return DbResult<SpResult>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §8 ─ 추상 메서드 (파생 클래스 필수 구현)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>DB 연결 객체 생성. Provider별로 구현.</summary>
    protected abstract IDbConnection CreateConnection();

    /// <summary>비동기 연결 열기. 기본 구현은 동기 Open() 호출.</summary>
    protected virtual Task OpenConnectionAsync(IDbConnection conn, CancellationToken ct)
    {
        conn.Open();
        return Task.CompletedTask;
    }

    /// <summary>SQL 실행 (영향 행 수 반환). Provider별로 구현.</summary>
    protected abstract Task<DbResult<int>> ExecuteCoreAsync(
        string sql, CommandType commandType, DbParam[]? parameters, CancellationToken ct);

    /// <summary>SELECT 쿼리 실행 (DataTable 반환). Provider별로 구현.</summary>
    protected abstract Task<DbResult<DataTable>> QueryTableCoreAsync(
        string sql, CommandType commandType, DbParam[]? parameters, CancellationToken ct);

    /// <summary>SP 실행 (SpResult 반환). Provider별로 구현.</summary>
    protected abstract Task<DbResult<SpResult>> CallSpCoreAsync(
        string spName, DbParam[]? parameters, CancellationToken ct);

    // §9 ─ 보호 로그 메서드
    // ─────────────────────────────────────────────────────────────────
    /// <summary>정보 로그 기록.</summary>
    protected virtual void Log(string message)
    { }
    //=> LogManager.Instance.Info($"DB.{ProviderType}", message);

    /// <summary>경고 로그 기록.</summary>
    protected virtual void LogWarn(string message)
    { }
    //=> LogManager.Instance.Warn($"DB.{ProviderType}", message);

    /// <summary>오류 로그 기록.</summary>
    protected virtual void LogError(string message)
    { }
    // => LogManager.Instance.Error($"DB.{ProviderType}", message);

    // §10 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }

    private void EnsureConnected()
    {
        if (State != ConnectionState.Open)
            throw new InvalidOperationException(
                "DB 연결이 열려 있지 않습니다. OpenAsync()를 먼저 호출하세요.");
    }

    // §11 ─ IAsyncDisposable
    // ─────────────────────────────────────────────────────────────────
    /// <remarks>
    /// virtual 선언으로 파생 클래스에서 override 가능.
    /// 파생 클래스 override 시 base.DisposeAsync() 호출 필수.
    /// </remarks>
    public async virtual ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await RollbackAsync().ConfigureAwait(false);
        await CloseAsync().ConfigureAwait(false);

        _transaction?.Dispose();
        _connection?.Dispose();
        _semaphore.Dispose();

        GC.SuppressFinalize(this);
    }
}