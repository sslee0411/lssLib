// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB · Core/DbParam.cs
//  역할: DB 파라미터를 추상화하는 범용 값 타입 (DB 벤더 독립)
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;

namespace lssLib.DB.Core;

// §1 ─ 파라미터 값 타입 열거형
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// DB 파라미터 값 타입.
/// Provider 구현체에서 각 DB 고유 타입으로 매핑한다.
/// </summary>
public enum DbParamType
{
    /// <summary>자동 추론 (Provider에서 .NET 타입 기반 매핑).</summary>
    Auto,

    // 문자열
    /// <summary>가변 길이 문자열.</summary>
    VarChar,
    /// <summary>고정 길이 문자열.</summary>
    Char,
    /// <summary>대용량 문자열 (CLOB / TEXT).</summary>
    Text,

    // 정수
    /// <summary>1바이트 정수.</summary>
    TinyInt,
    /// <summary>2바이트 정수.</summary>
    SmallInt,
    /// <summary>4바이트 정수.</summary>
    Int,
    /// <summary>8바이트 정수.</summary>
    BigInt,

    // 실수
    /// <summary>단정도 부동소수점.</summary>
    Float,
    /// <summary>배정도 부동소수점.</summary>
    Double,
    /// <summary>고정소수점 (DECIMAL / NUMBER).</summary>
    Decimal,

    // 날짜/시간
    /// <summary>날짜.</summary>
    Date,
    /// <summary>날짜+시간.</summary>
    DateTime,
    /// <summary>날짜+시간+시간대.</summary>
    DateTimeOffset,

    // 기타
    /// <summary>논리 값.</summary>
    Boolean,
    /// <summary>고유 식별자 (GUID).</summary>
    Guid,
    /// <summary>이진 데이터 (BLOB / VARBINARY).</summary>
    Binary,

    // InfluxDB 전용
    /// <summary>InfluxDB Tag (인덱싱 문자열).</summary>
    InfluxTag,
    /// <summary>InfluxDB Field (측정값).</summary>
    InfluxField,
    /// <summary>InfluxDB Timestamp.</summary>
    InfluxTimestamp,

    // SP 전용
    /// <summary>커서 (Oracle RefCursor / MSSQL 결과셋).</summary>
    Cursor,
}

// §2 ─ 파라미터 래퍼
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// DB 파라미터를 추상화하는 범용 값 타입.
/// Provider 구현체에서 각 DB 고유 파라미터 객체로 변환한다.
/// </summary>
/// <example><code>
/// // 기본 Input 파라미터
/// var p1 = DbParam.In("IN_DATA",    "SELECT '2024' FROM DUAL");
/// var p2 = DbParam.In("IN_SENSOR",  42,    DbParamType.Int);
/// var p3 = DbParam.In("IN_DATE",    DateTime.Now);
///
/// // Output 파라미터
/// var p4 = DbParam.Out("OUT_CODE",  DbParamType.VarChar, size: 10);
/// var p5 = DbParam.Out("OUT_MSG",   DbParamType.VarChar, size: 200);
/// var p6 = DbParam.Out("OUT_DATA",  DbParamType.Cursor);
///
/// // SP 호출
/// await repo.CallSpAsync("SP_NAME", p1, p4, p5, p6);
/// </code></example>
public readonly record struct DbParam
{
    // §2-1 ─ 프로퍼티

    /// <summary>파라미터 이름.</summary>
    public string Name { get; init; }

    /// <summary>파라미터 값 (Output 파라미터는 null 허용).</summary>
    public object? Value { get; init; }

    /// <summary>파라미터 방향.</summary>
    public ParameterDirection Direction { get; init; }

    /// <summary>파라미터 값 타입.</summary>
    public DbParamType ParamType { get; init; }

    /// <summary>최대 크기 (문자열/이진 타입에서 사용, 0 = 자동).</summary>
    public int Size { get; init; }

    // §2-2 ─ 팩토리 메서드

    /// <summary>Input 파라미터 생성.</summary>
    /// <param name="name">파라미터 이름.</param>
    /// <param name="value">파라미터 값.</param>
    /// <param name="type">값 타입 (기본값: Auto).</param>
    /// <param name="size">최대 크기 (기본값: 0 = 자동).</param>
    public static DbParam In(string name, object? value,
        DbParamType type = DbParamType.Auto, int size = 0) => new()
        {
            Name = name,
            Value = value ?? DBNull.Value,
            Direction = ParameterDirection.Input,
            ParamType = type,
            Size = size,
        };

    /// <summary>Output 파라미터 생성.</summary>
    /// <param name="name">파라미터 이름.</param>
    /// <param name="type">값 타입.</param>
    /// <param name="size">최대 크기 (기본값: 0 = 자동).</param>
    public static DbParam Out(string name,
        DbParamType type = DbParamType.Auto, int size = 0) => new()
        {
            Name = name,
            Value = DBNull.Value,
            Direction = ParameterDirection.Output,
            ParamType = type,
            Size = size,
        };

    /// <summary>InputOutput 파라미터 생성.</summary>
    /// <param name="name">파라미터 이름.</param>
    /// <param name="value">초기값.</param>
    /// <param name="type">값 타입.</param>
    /// <param name="size">최대 크기 (기본값: 0 = 자동).</param>
    public static DbParam InOut(string name, object? value,
        DbParamType type = DbParamType.Auto, int size = 0) => new()
        {
            Name = name,
            Value = value ?? DBNull.Value,
            Direction = ParameterDirection.InputOutput,
            ParamType = type,
            Size = size,
        };

    /// <summary>ReturnValue 파라미터 생성.</summary>
    /// <param name="name">파라미터 이름.</param>
    /// <param name="type">값 타입.</param>
    public static DbParam Return(string name,
        DbParamType type = DbParamType.Auto) => new()
        {
            Name = name,
            Value = DBNull.Value,
            Direction = ParameterDirection.ReturnValue,
            ParamType = type,
            Size = 0,
        };

    /// <summary>
    /// OracleDB 패턴 표준 SP 파라미터 배열 생성.
    /// (IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR)
    /// </summary>
    /// <param name="inData">IN_DATA 파라미터 값.</param>
    /// <returns>표준 4개 파라미터 배열.</returns>
    public static DbParam[] StandardSp(string inData) =>
    [
        In ("IN_DATA",        inData,         DbParamType.VarChar),
        Out("OUT_RETURNCODE", DbParamType.VarChar, size: 10),
        Out("OUT_RETURNMSG",  DbParamType.VarChar, size: 200),
        Out("OUT_CURSOR",     DbParamType.Cursor),
    ];

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{Direction}] {Name} = {Value} ({ParamType})";
}