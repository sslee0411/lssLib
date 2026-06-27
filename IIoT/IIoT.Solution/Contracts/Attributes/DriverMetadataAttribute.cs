// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Attributes/DriverMetadataAttribute.cs
//  역할: 드라이버 클래스에 메타데이터를 선언적으로 부착
//        PluginLoader가 리플렉션으로 읽어 빠른 탐색에 사용
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// 드라이버 플러그인 메타데이터 어트리뷰트.
/// <para>
/// IProtocolPlugin 구현 클래스에 부착하면 PluginLoader가
/// 리플렉션 없이 빠르게 드라이버 정보를 읽을 수 있다.
/// </para>
/// <example>
/// <code>
/// [DriverMetadata(
///     DisplayName:  "미쓰비시 MC",
///     Description:  "MELSEC Q/L/FX 시리즈 MC 프로토콜",
///     Vendor:       PlcVendor.Mitsubishi,
///     SupportWrite: true
/// )]
/// public sealed class MitsubishiMcPlugin : IProtocolPlugin { ... }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DriverMetadataAttribute : Attribute
{
    /// <summary>드라이버 표시 이름</summary>
    public string    DisplayName  { get; }

    /// <summary>드라이버 설명</summary>
    public string    Description  { get; }

    /// <summary>지원 PLC 제조사</summary>
    public PlcVendor Vendor       { get; }

    /// <summary>쓰기 지원 여부</summary>
    public bool      SupportWrite { get; }

    /// <param name="displayName">드라이버 표시 이름 (예: "미쓰비시 MC")</param>
    /// <param name="description">드라이버 설명 (예: "MELSEC Q/L/FX 시리즈")</param>
    /// <param name="vendor">지원 PLC 제조사</param>
    /// <param name="supportWrite">쓰기 지원 여부 (기본: false)</param>
    public DriverMetadataAttribute(
        string    displayName,
        string    description,
        PlcVendor vendor,
        bool      supportWrite = false)
    {
        DisplayName  = displayName;
        Description  = description;
        Vendor       = vendor;
        SupportWrite = supportWrite;
    }
}
