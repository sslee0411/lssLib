// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Interfaces/IProtocolPlugin.cs
//  역할: 플러그인 진입점 인터페이스
//        PluginLoader가 dll에서 이 타입을 탐색하여 로드한다
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// 프로토콜 플러그인 진입점.
/// <para>
/// 각 드라이버 dll이 반드시 구현해야 하는 최상위 인터페이스.
/// PluginLoader는 dll에서 이 인터페이스를 구현한 비추상 타입을 탐색하여 인스턴스화한다.
/// </para>
///
/// <b>플러그인 개발 절차:</b>
/// <list type="number">
///   <item><description>IIoT.Contracts 만 참조하는 새 프로젝트 생성</description></item>
///   <item><description>IProtocolPlugin + IProtocolDriver 구현</description></item>
///   <item><description>빌드 → Plugins/ 폴더에 dll 복사</description></item>
///   <item><description>Studio/Collector 재시작 → 자동 등록 완료</description></item>
/// </list>
///
/// <example>
/// <code>
/// [DriverMetadata("미쓰비시 MC", "MELSEC Q/L/FX", PlcVendor.Mitsubishi, SupportWrite: true)]
/// public sealed class MitsubishiMcPlugin : IProtocolPlugin
/// {
///     public string    PluginName     => "미쓰비시 MC";
///     public string    PluginVersion  => "1.0.0";
///     public PlcVendor SupportedVendor => PlcVendor.Mitsubishi;
///
///     public IReadOnlyList&lt;ParameterDefinition&gt; GetParameterSchema() => new[]
///     {
///         new ParameterDefinition("Host", "IP 주소", ParameterType.String,
///             DefaultValue: "192.168.0.1", IsRequired: true),
///         new ParameterDefinition("Port", "포트",    ParameterType.Int,
///             DefaultValue: "5007",        IsRequired: true),
///     };
///
///     public IReadOnlyList&lt;DriverCapability&gt; GetCapabilities()
///         => [DriverCapability.Read, DriverCapability.Write, DriverCapability.BatchRead];
///
///     public IProtocolDriver CreateDriver() => new MitsubishiMcDriver();
/// }
/// </code>
/// </example>
/// </summary>
public interface IProtocolPlugin
{
    // §1 ─ 메타데이터 ──────────────────────────────────────

    /// <summary>플러그인 표시 이름 (예: "미쓰비시 MC")</summary>
    string    PluginName      { get; }

    /// <summary>플러그인 버전 (예: "1.0.0")</summary>
    string    PluginVersion   { get; }

    /// <summary>이 플러그인이 지원하는 PLC 제조사</summary>
    PlcVendor SupportedVendor { get; }

    // §2 ─ 스키마·기능 정보 ────────────────────────────────

    /// <summary>
    /// 파라미터 스키마를 반환합니다.
    /// <para>Studio가 이 목록을 읽어 PlcEditorView에 입력 폼을 자동 렌더링합니다.</para>
    /// </summary>
    IReadOnlyList<ParameterDefinition> GetParameterSchema();

    /// <summary>
    /// 이 드라이버가 지원하는 기능 목록을 반환합니다.
    /// </summary>
    IReadOnlyList<DriverCapability> GetCapabilities();

    // §3 ─ 드라이버 생성 ───────────────────────────────────

    /// <summary>
    /// 새 드라이버 인스턴스를 생성합니다.
    /// <para>
    /// Collector는 PLC 노드 하나당 드라이버 인스턴스를 1개씩 생성한다.
    /// 스레드 안전 보장이 필요하다면 내부에서 처리할 것.
    /// </para>
    /// </summary>
    IProtocolDriver CreateDriver();
}
