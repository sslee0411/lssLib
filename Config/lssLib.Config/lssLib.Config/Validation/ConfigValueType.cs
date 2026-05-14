// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Validation/ConfigValueType.cs
//  역할: 설정값 검증에 사용되는 값 유형 열거형
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config.Validation;

/// <summary>
/// 설정값 검증에 사용되는 값 유형.
/// </summary>
/// <remarks>
/// <see cref="ConfigFieldRule"/> 의 <c>ValueType</c> 으로 지정되며,
/// <see cref="ConfigValidator"/> 가 이 유형을 기준으로 파싱·범위·형식 검사를 수행합니다.
/// <example><code>
/// var schema = new ConfigSchema()
///     .Require("Network", "Port",    ConfigValueType.Int,       range: (1, 65535))
///     .Require("Network", "Host",    ConfigValueType.IpAddress)
///     .Require("App",     "Enabled", ConfigValueType.Bool)
///     .Require("App",     "Rate",    ConfigValueType.Double,    range: (0.0, 1.0))
///     .Optional("App",    "Version", ConfigValueType.SemVer,    defaultValue: "1.0.0");
/// </code></example>
/// </remarks>
public enum ConfigValueType
{
    /// <summary>임의 문자열. 빈 문자열도 허용.</summary>
    String = 0,

    /// <summary>비어있지 않은 문자열 (<c>NotEmpty</c> 검증 포함).</summary>
    NonEmptyString = 1,

    /// <summary>정수 (<see cref="int"/>). 범위 검증 가능.</summary>
    Int = 2,

    /// <summary>정수 (<see cref="long"/>). 범위 검증 가능.</summary>
    Long = 3,

    /// <summary>부동소수점 (<see cref="double"/>). 범위 검증 가능.</summary>
    Double = 4,

    /// <summary>불리언 — "true"/"1"/"yes"/"on" 또는 "false"/"0"/"no"/"off".</summary>
    Bool = 5,

    /// <summary>IPv4 주소 형식 (예: <c>192.168.1.100</c>).</summary>
    IpAddress = 6,

    /// <summary>포트 번호 (1~65535).</summary>
    Port = 7,

    /// <summary>시맨틱 버전 형식 (예: <c>1.2.3</c>).</summary>
    SemVer = 8,

    /// <summary>디렉터리 경로 — 존재 여부는 검증하지 않음 (형식만 확인).</summary>
    DirectoryPath = 9,

    /// <summary>파일 경로 — 존재 여부는 검증하지 않음 (형식만 확인).</summary>
    FilePath = 10,

    /// <summary>정규식 패턴으로 검증. <see cref="ConfigFieldRule.Pattern"/> 을 반드시 설정.</summary>
    Regex = 11,

    /// <summary>열거형 목록 중 하나 — <see cref="ConfigFieldRule.AllowedValues"/> 를 반드시 설정.</summary>
    Enum = 12,

    /// <summary>GUID 문자열 (하이픈 포함·제외 모두 허용).</summary>
    Guid = 13,

    /// <summary>Cron 표현식 형식 (5필드 또는 6필드).</summary>
    Cron = 14
}