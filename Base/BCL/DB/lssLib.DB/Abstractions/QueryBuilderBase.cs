// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Abstractions/QueryBuilderBase.cs
//  역할: IQueryBuilder 공통 구현 — 파라미터 누적·WHERE·ORDER 기반 클래스
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using lssLib.DB.Contracts;
using lssLib.DB.Core;

namespace lssLib.DB.Abstractions;

/// <summary>
/// IQueryBuilder 공통 구현 기반 클래스.
/// Provider별 구현체(MsSqlQueryBuilder, OracleQueryBuilder 등)에서 상속한다.
/// </summary>
/// <remarks>
/// 파생 클래스 최소 구현 예.
/// <code>
/// public sealed class MsSqlQueryBuilder : QueryBuilderBase
/// {
///     // MSSQL: TOP N 문법
///     protected override string BuildLimitClause(int count) => string.Empty;
///     protected override string BuildSelectPrefix(int? limit)
///         => limit.HasValue ? $"SELECT TOP {limit}" : "SELECT";
///
///     // MSSQL: @ParamName 형식
///     protected override string ParamPrefix => "@";
/// }
///
/// public sealed class OracleQueryBuilder : QueryBuilderBase
/// {
///     // Oracle: ROWNUM 문법
///     protected override string BuildLimitClause(int count)
///         => $"AND ROWNUM &lt;= {count}";
///     protected override string BuildSelectPrefix(int? limit) => "SELECT";
///
///     // Oracle: :ParamName 형식
///     protected override string ParamPrefix => ":";
/// }
/// </code>
/// </remarks>
public abstract class QueryBuilderBase : IQueryBuilder
{
    // §1 ─ 내부 상태
    // ─────────────────────────────────────────────────────────────────
    private string? _tableName;
    private readonly List<string> _selectCols = [];
    private readonly List<WhereClause> _wheres = [];
    private readonly List<OrderClause> _orders = [];
    private readonly List<ValueClause> _values = [];
    private int? _limit;
    private BuildMode _mode = BuildMode.Select;

    // §2 ─ 추상 프로퍼티 (파생 클래스 필수 구현)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>파라미터 접두사 (MSSQL: "@", Oracle: ":", MySQL: "@", SQLite: "@").</summary>
    protected abstract string ParamPrefix { get; }

    /// <summary>SELECT 접두어 (MSSQL: "SELECT TOP N", Oracle: "SELECT").</summary>
    protected abstract string BuildSelectPrefix(int? limit);

    /// <summary>LIMIT 절 문자열 (MSSQL: "", Oracle: "AND ROWNUM <=N", MySQL/SQLite: "LIMIT N").</summary>
    protected abstract string BuildLimitClause(int count);

    // §3 ─ IQueryBuilder 구현 — SELECT 절
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IQueryBuilder From(string tableName)
    {
        _tableName = tableName;
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder Select(params string[] columns)
    {
        _selectCols.AddRange(columns);
        return this;
    }

    // §4 ─ IQueryBuilder 구현 — WHERE 절
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IQueryBuilder Where(string column, QueryOp op, object? value)
    {
        _wheres.Add(new WhereClause(column, op, value, Logic: "AND"));
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder OrWhere(string column, QueryOp op, object? value)
    {
        _wheres.Add(new WhereClause(column, op, value, Logic: "OR"));
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder WhereIn(string column, IEnumerable<object> values)
    {
        _wheres.Add(new WhereClause(column, QueryOp.Eq, values.ToArray(), Logic: "AND", IsIn: true));
        return this;
    }

    // §5 ─ IQueryBuilder 구현 — ORDER / LIMIT
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IQueryBuilder OrderBy(string column, bool ascending = true)
    {
        _orders.Add(new OrderClause(column, ascending));
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder Limit(int count)
    {
        _limit = count;
        return this;
    }

    // §6 ─ IQueryBuilder 구현 — INSERT / UPDATE / DELETE
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IQueryBuilder Insert(string tableName)
    {
        _tableName = tableName;
        _mode = BuildMode.Insert;
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder Value(string column, object? value)
    {
        _values.Add(new ValueClause(column, value));
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder Update(string tableName)
    {
        _tableName = tableName;
        _mode = BuildMode.Update;
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder Set(string column, object? value)
    {
        _values.Add(new ValueClause(column, value));
        return this;
    }

    /// <inheritdoc/>
    public IQueryBuilder Delete(string tableName)
    {
        _tableName = tableName;
        _mode = BuildMode.Delete;
        return this;
    }

    // §7 ─ IQueryBuilder 구현 — 빌드
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public (string Sql, DbParam[] Parameters) Build()
    {
        ValidateTable();
        var ps = new List<DbParam>();
        var sql = new System.Text.StringBuilder();
        int idx = 0;

        // SELECT 접두어 (TOP N 등)
        sql.Append(BuildSelectPrefix(_limit));
        sql.Append(' ');

        // 컬럼
        sql.Append(_selectCols.Count > 0
            ? string.Join(", ", _selectCols)
            : "*");

        // FROM
        sql.Append($" FROM {_tableName}");

        // WHERE
        AppendWhere(sql, ps, ref idx);

        // ORDER BY
        if (_orders.Count > 0)
        {
            sql.Append(" ORDER BY ");
            sql.Append(string.Join(", ", _orders.Select(
                o => $"{o.Column} {(o.Ascending ? "ASC" : "DESC")}")));
        }

        // LIMIT (Oracle ROWNUM 등 후행 방식)
        if (_limit.HasValue)
        {
            var limitClause = BuildLimitClause(_limit.Value);
            if (!string.IsNullOrEmpty(limitClause))
                sql.Append($" {limitClause}");
        }

        return (sql.ToString(), ps.ToArray());
    }

    /// <inheritdoc/>
    public (string Sql, DbParam[] Parameters) BuildInsert()
    {
        ValidateTable();
        if (_values.Count == 0)
            throw new InvalidOperationException("INSERT 값이 없습니다. Value()를 호출하세요.");

        var ps = new List<DbParam>();
        int idx = 0;

        var colPart = string.Join(", ", _values.Select(v => v.Column));
        var paramPart = string.Join(", ", _values.Select(v =>
        {
            var pName = $"{ParamPrefix}p{idx++}";
            ps.Add(DbParam.In(pName, v.Value));
            return pName;
        }));

        return ($"INSERT INTO {_tableName} ({colPart}) VALUES ({paramPart})", ps.ToArray());
    }

    /// <inheritdoc/>
    public (string Sql, DbParam[] Parameters) BuildUpdate()
    {
        ValidateTable();
        if (_values.Count == 0)
            throw new InvalidOperationException("UPDATE 값이 없습니다. Set()을 호출하세요.");

        var ps = new List<DbParam>();
        int idx = 0;
        var sql = new System.Text.StringBuilder();

        sql.Append($"UPDATE {_tableName} SET ");
        sql.Append(string.Join(", ", _values.Select(v =>
        {
            var pName = $"{ParamPrefix}p{idx++}";
            ps.Add(DbParam.In(pName, v.Value));
            return $"{v.Column} = {pName}";
        })));

        AppendWhere(sql, ps, ref idx);

        return (sql.ToString(), ps.ToArray());
    }

    /// <inheritdoc/>
    public (string Sql, DbParam[] Parameters) BuildDelete()
    {
        ValidateTable();
        var ps = new List<DbParam>();
        int idx = 0;
        var sql = new System.Text.StringBuilder($"DELETE FROM {_tableName}");
        AppendWhere(sql, ps, ref idx);
        return (sql.ToString(), ps.ToArray());
    }

    /// <inheritdoc/>
    public IQueryBuilder Reset()
    {
        _tableName = null;
        _selectCols.Clear();
        _wheres.Clear();
        _orders.Clear();
        _values.Clear();
        _limit = null;
        _mode = BuildMode.Select;
        return this;
    }

    // §8 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// <summary>WHERE 절 문자열 및 파라미터 누적.</summary>
    private void AppendWhere(
    System.Text.StringBuilder sql,
    List<DbParam> ps,
    ref int idx)
    {
        if (_wheres.Count == 0) return;

        sql.Append(" WHERE ");
        for (int i = 0; i < _wheres.Count; i++)
        {
            var w = _wheres[i];
            if (i > 0) sql.Append($" {w.Logic} ");

            // IS NULL / IS NOT NULL — 파라미터 없음
            if (w.Op is QueryOp.IsNull or QueryOp.IsNotNull)
            {
                sql.Append($"{w.Column} {OpToSql(w.Op)}");
                continue;
            }

            // IN 절
            if (w.IsIn && w.Value is object[] inVals)
            {
                // ref 변수를 람다에 쓰지 못하므로 로컬 변수로 복사
                int localIdx = idx;

                // Select 내부에서 localIdx를 안전하게 증가시킴
                var inParams = inVals.Select(_ =>
                {
                    var pn = $"{ParamPrefix}p{localIdx++}";
                    ps.Add(DbParam.In(pn, _));
                    return pn;
                }).ToArray(); // 즉시 실행(Evaluate)하여 파라미터 순서 및 추가 보장

                idx = localIdx; // 증가된 인덱스를 다시 ref 변수에 저장

                sql.Append($"{w.Column} IN ({string.Join(", ", inParams)})");
                continue;
            }

            // 일반 비교
            var paramName = $"{ParamPrefix}p{idx++}";
            var val = w.Op switch
            {
                QueryOp.Contains => $"%{w.Value}%",
                QueryOp.StartsWith => $"{w.Value}%",
                QueryOp.EndsWith => $"%{w.Value}",
                _ => w.Value,
            };
            var opSql = w.Op is QueryOp.Contains or QueryOp.StartsWith or QueryOp.EndsWith
                ? "LIKE" : OpToSql(w.Op);

            sql.Append($"{w.Column} {opSql} {paramName}");
            ps.Add(DbParam.In(paramName, val));
        }
    }



    /// <summary>QueryOp → SQL 연산자 문자열 변환.</summary>
    private static string OpToSql(QueryOp op) => op switch
    {
        QueryOp.Eq => "=",
        QueryOp.NotEq => "!=",
        QueryOp.Gt => ">",
        QueryOp.GtEq => ">=",
        QueryOp.Lt => "<",
        QueryOp.LtEq => "<=",
        QueryOp.IsNull => "IS NULL",
        QueryOp.IsNotNull => "IS NOT NULL",
        _ => "=",
    };

    private void ValidateTable()
    {
        if (string.IsNullOrWhiteSpace(_tableName))
            throw new InvalidOperationException("테이블이 지정되지 않았습니다. From() 또는 Insert()/Update()/Delete()를 호출하세요.");
    }

    // §9 ─ 내부 레코드
    // ─────────────────────────────────────────────────────────────────
    private enum BuildMode { Select, Insert, Update, Delete }

    private sealed record WhereClause(
        string Column,
        QueryOp Op,
        object? Value,
        string Logic,
        bool IsIn = false);

    private sealed record OrderClause(string Column, bool Ascending);

    private sealed record ValueClause(string Column, object? Value);
}