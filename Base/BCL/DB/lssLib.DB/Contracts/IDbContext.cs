// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Contracts/IDbContext.cs
//  역할: DB 연결·트랜잭션·실행 생명주기 계약 (Provider 독립)
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using lssLib.DB.Core;

namespace lssLib.DB.Contracts;

/// <summary>
/// DB 연결·트랜잭션·실행 생명주기 계약.
/// Provider별 구현체(OracleDbContext, MsSqlDbContext 등)에서 구현한다.
/// </summary>
/// <remarks>
/// 사용 패턴.
/// <code>
/// await using IDbContext ctx = provider.CreateContext();
/// await ctx.OpenAsync();
///
/// await ctx.BeginTransactionAsync();
/// try
/// {
///     await ctx.ExecuteAsync("UPDATE ...", DbParam.In("ID", 1));
///     await ctx.CommitAsync();
/// }
/// catch
/// {
///     await ctx.RollbackAsync();
///     throw;
/// }
/// </code>
/// </remarks>
public interface IDbContext : IAsyncDisposable
{
    // §1 ─ 상태 조회

    /// <summary>현재 연결 상태.</summary>
    ConnectionState State { get; }

    /// <summary>트랜잭션 진행 중 여부.</summary>
    bool IsInTransaction { get; }

    /// <summary>Provider 종류.</summary>
    DbProviderType ProviderType { get; }

    // §2 ─ 연결 관리

    /// <summary>DB 연결을 비동기로 엽니다.</summary>
    /// <param name="ct">취소 토큰.</param>
    /// <exception cref="DbException">연결 실패 시.</exception>
    Task OpenAsync(CancellationToken ct = default);

    /// <summary>DB 연결을 닫습니다.</summary>
    Task CloseAsync();

    // §3 ─ 트랜잭션

    /// <summary>트랜잭션을 시작합니다.</summary>
    /// <param name="ct">취소 토큰.</param>
    /// <exception cref="DbException">트랜잭션 시작 실패 시.</exception>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>트랜잭션을 커밋합니다.</summary>
    /// <exception cref="DbException">커밋 실패 시.</exception>
    Task CommitAsync();

    /// <summary>트랜잭션을 롤백합니다.</summary>
    Task RollbackAsync();

    // §4 ─ 실행 (쿼리 반환 없음)

    /// <summary>
    /// SQL/SP를 실행하고 영향 받은 행 수를 반환합니다.
    /// </summary>
    /// <param name="sql">SQL 문 또는 SP 이름.</param>
    /// <param name="commandType">Text / StoredProcedure.</param>
    /// <param name="parameters">파라미터 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>영향 받은 행 수를 담은 DbResult.</returns>
    Task<DbResult<int>> ExecuteAsync(
        string sql,
        CommandType commandType = CommandType.Text,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    // §5 ─ 조회 (DataTable 반환)

    /// <summary>
    /// SQL을 실행하고 DataTable을 반환합니다.
    /// </summary>
    /// <param name="sql">SELECT 문.</param>
    /// <param name="commandType">Text / StoredProcedure.</param>
    /// <param name="parameters">파라미터 목록.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>DataTable을 담은 DbResult.</returns>
    Task<DbResult<DataTable>> QueryTableAsync(
        string sql,
        CommandType commandType = CommandType.Text,
        DbParam[]? parameters = null,
        CancellationToken ct = default);

    // §6 ─ SP 실행 (OracleDB 패턴 범용화)

    /// <summary>
    /// 저장 프로시저를 실행합니다.
    /// IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR 표준 패턴.
    /// </summary>
    /// <param name="spName">SP 이름.</param>
    /// <param name="parameters">파라미터 목록 (DbParam.StandardSp() 활용 권장).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>SpResult를 담은 DbResult.</returns>
    Task<DbResult<SpResult>> CallSpAsync(
        string spName,
        DbParam[]? parameters = null,
        CancellationToken ct = default);
}