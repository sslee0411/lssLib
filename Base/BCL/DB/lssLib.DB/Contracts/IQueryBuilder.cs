// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Contracts/IQueryBuilder.cs
//  역할: 코드 기반 쿼리 빌더 계약 (SQL 문자열 직접 작성 없이 쿼리 조립)
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Core;

namespace lssLib.DB.Contracts;

/// <summary>
/// 코드 기반 쿼리 빌더 계약.
/// Provider별 구현체에서 각 DB 문법에 맞는 SQL을 생성한다.
/// </summary>
/// <remarks>
/// 사용 패턴.
/// <code>
/// IQueryBuilder qb = new MsSqlQueryBuilder();
///
/// // SELECT 빌드
/// var (sql, ps) = qb
///     .From("SENSOR_DATA")
///     .Select("SENSOR_ID", "SENSOR_VALUE", "REG_DT")
///     .Where("SENSOR_ID", QueryOp.Eq,      42)
///     .Where("REG_DT",    QueryOp.GtEq,    DateTime.Today)
///     .OrderBy("REG_DT",  false)
///     .Limit(100)
///     .Build();
///
/// // INSERT 빌드
/// var (sql2, ps2) = qb
///     .Insert("SENSOR_DATA")
///     .Value("SENSOR_ID",    42)
///     .Value("SENSOR_VALUE", 3.14)
///     .Value("REG_DT",       DateTime.Now)
///     .BuildInsert();
/// </code>
/// </remarks>
public interface IQueryBuilder
{
    // §1 ─ SELECT 절

    /// <summary>대상 테이블 지정 (FROM 절).</summary>
    /// <param name="tableName">테이블 이름.</param>
    IQueryBuilder From(string tableName);

    /// <summary>조회 컬럼 지정 (SELECT 절). 생략 시 SELECT *.</summary>
    /// <param name="columns">컬럼 이름 목록.</param>
    IQueryBuilder Select(params string[] columns);

    // §2 ─ WHERE 절

    /// <summary>WHERE 조건 추가. 여러 번 호출 시 AND로 연결.</summary>
    /// <param name="column">컬럼 이름.</param>
    /// <param name="op">비교 연산자.</param>
    /// <param name="value">비교 값.</param>
    IQueryBuilder Where(string column, QueryOp op, object? value);

    /// <summary>WHERE 조건 추가 (OR 연결).</summary>
    /// <param name="column">컬럼 이름.</param>
    /// <param name="op">비교 연산자.</param>
    /// <param name="value">비교 값.</param>
    IQueryBuilder OrWhere(string column, QueryOp op, object? value);

    /// <summary>IN 조건 추가.</summary>
    /// <param name="column">컬럼 이름.</param>
    /// <param name="values">IN 목록 값.</param>
    IQueryBuilder WhereIn(string column, IEnumerable<object> values);

    // §3 ─ ORDER / LIMIT

    /// <summary>ORDER BY 절 추가.</summary>
    /// <param name="column">정렬 컬럼.</param>
    /// <param name="ascending">오름차순 여부 (기본값: true).</param>
    IQueryBuilder OrderBy(string column, bool ascending = true);

    /// <summary>결과 행 수 제한 (TOP / LIMIT / ROWNUM).</summary>
    /// <param name="count">최대 행 수.</param>
    IQueryBuilder Limit(int count);

    // §4 ─ INSERT / UPDATE / DELETE

    /// <summary>INSERT 대상 테이블 지정.</summary>
    /// <param name="tableName">테이블 이름.</param>
    IQueryBuilder Insert(string tableName);

    /// <summary>INSERT 컬럼-값 쌍 추가.</summary>
    /// <param name="column">컬럼 이름.</param>
    /// <param name="value">삽입 값.</param>
    IQueryBuilder Value(string column, object? value);

    /// <summary>UPDATE 대상 테이블 지정.</summary>
    /// <param name="tableName">테이블 이름.</param>
    IQueryBuilder Update(string tableName);

    /// <summary>UPDATE SET 컬럼-값 쌍 추가.</summary>
    /// <param name="column">컬럼 이름.</param>
    /// <param name="value">갱신 값.</param>
    IQueryBuilder Set(string column, object? value);

    /// <summary>DELETE 대상 테이블 지정.</summary>
    /// <param name="tableName">테이블 이름.</param>
    IQueryBuilder Delete(string tableName);

    // §5 ─ 빌드

    /// <summary>
    /// SELECT SQL 문자열과 파라미터 배열을 생성합니다.
    /// </summary>
    /// <returns>(SQL 문자열, DbParam 배열) 튜플.</returns>
    (string Sql, DbParam[] Parameters) Build();

    /// <summary>
    /// INSERT SQL 문자열과 파라미터 배열을 생성합니다.
    /// </summary>
    (string Sql, DbParam[] Parameters) BuildInsert();

    /// <summary>
    /// UPDATE SQL 문자열과 파라미터 배열을 생성합니다.
    /// </summary>
    (string Sql, DbParam[] Parameters) BuildUpdate();

    /// <summary>
    /// DELETE SQL 문자열과 파라미터 배열을 생성합니다.
    /// </summary>
    (string Sql, DbParam[] Parameters) BuildDelete();

    /// <summary>
    /// 현재 빌더 상태를 초기화합니다.
    /// </summary>
    IQueryBuilder Reset();
}

// §6 ─ 쿼리 비교 연산자
// ─────────────────────────────────────────────────────────────────────

/// <summary>WHERE 절 비교 연산자.</summary>
public enum QueryOp
{
    /// <summary> = (같음).</summary>
    Eq,
    /// <summary> != (다름).</summary>
    NotEq,
    /// <summary> < (초과).</summary>
    Gt,
    /// <summary> <= (이상).</summary>
    GtEq,
    /// <summary> > (미만).</summary>
    Lt,
    /// <summary> >= (이하).</summary>
    LtEq,
    /// <summary>LIKE '%value%'.</summary>
    Contains,
    /// <summary>LIKE 'value%'.</summary>
    StartsWith,
    /// <summary>LIKE '%value'.</summary>
    EndsWith,
    /// <summary>IS NULL.</summary>
    IsNull,
    /// <summary>IS NOT NULL.</summary>
    IsNotNull,
}