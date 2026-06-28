// ══════════════════════════════════════════════════════════
//  IIoT.Driver.ModbusTcp · ModbusTcpPlugin.cs
//  역할: Modbus TCP 플러그인 진입점 (IProtocolPlugin 구현)
//        PluginLoader 가 이 타입을 탐색하여 자동 등록
//
//  ★ 새 드라이버 추가 시 이 파일을 복사해서 수정하세요.
//    1. PluginName  → 새 driverId 로 변경
//    2. PlcVendor   → 해당 제조사로 변경
//    3. GetParameterSchema() → 연결에 필요한 파라미터 정의
//    4. CreateDriver() → 새 Driver 클래스 반환
//
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;

namespace IIoT.Driver.ModbusTcp;

/// <summary>
/// Modbus TCP 플러그인.
/// Modbus TCP 프로토콜로 통신하는 PLC/장비에 범용으로 사용합니다.
/// </summary>
[DriverMetadata(
    displayName:  "Modbus TCP",
    description:  "Modbus TCP/IP 프로토콜 (범용 — 제조사 무관)",
    vendor:       PlcVendor.Modbus,
    supportWrite: true
)]
public sealed class ModbusTcpPlugin : IProtocolPlugin
{
    // §1 ─ 메타데이터 ──────────────────────────────────────

    /// <summary>
    /// ★ driverId 로 사용됩니다.
    /// device.json 의 driverId 필드 값과 반드시 일치해야 합니다.
    /// 소문자 + 하이픈 형식 권장: "modbus-tcp"
    /// </summary>
    public string    PluginName      => "modbus-tcp";
    public string    PluginVersion   => "1.0.0";
    public PlcVendor SupportedVendor => PlcVendor.Modbus;

    // §2 ─ 파라미터 스키마 ─────────────────────────────────

    /// <summary>
    /// Studio PLC 편집기에 자동으로 렌더링될 입력 폼을 정의합니다.
    ///
    /// ★ ParameterType 별 생성 UI:
    ///   String   → TextBox
    ///   Int      → NumericBox
    ///   Bool     → CheckBox
    ///   Enum     → ComboBox (EnumValues 배열 지정 필수)
    ///   Password → PasswordBox (마스킹)
    /// </summary>
    public IReadOnlyList<ParameterDefinition> GetParameterSchema() =>
    [
        // ── 연결 설정 ────────────────────────────────────
        new ParameterDefinition(
            Key:             "Host",
            DisplayName:     "IP 주소",
            Type:            ParameterType.String,
            DefaultValue:    "192.168.0.1",
            Description:     "PLC IP 주소 또는 호스트명",
            IsRequired:      true,
            ValidationRegex: @"^[\w\.\-]+$"
        ),
        new ParameterDefinition(
            Key:          "Port",
            DisplayName:  "포트",
            Type:         ParameterType.Int,
            DefaultValue: "502",
            Description:  "Modbus TCP 기본 포트: 502",
            IsRequired:   true
        ),
        new ParameterDefinition(
            Key:          "SlaveId",
            DisplayName:  "슬레이브 ID",
            Type:         ParameterType.Int,
            DefaultValue: "1",
            Description:  "Modbus 장치 주소 (1~247)"
        ),

        // ── 타임아웃·재연결 ───────────────────────────────
        new ParameterDefinition(
            Key:          "TimeoutMs",
            DisplayName:  "타임아웃 (ms)",
            Type:         ParameterType.Int,
            DefaultValue: "3000"
        ),
        new ParameterDefinition(
            Key:          "RetryCount",
            DisplayName:  "재시도 횟수",
            Type:         ParameterType.Int,
            DefaultValue: "3"
        ),

        // ── 배치 읽기 최적화 ──────────────────────────────
        new ParameterDefinition(
            Key:          "MaxBatchSize",
            DisplayName:  "최대 배치 크기 (레지스터 수)",
            Type:         ParameterType.Int,
            DefaultValue: "120",
            Description:  "한 번 요청으로 읽을 최대 레지스터 수 (Modbus 제한: 125)"
        ),
    ];

    // §3 ─ 지원 기능 ───────────────────────────────────────

    public IReadOnlyList<DriverCapability> GetCapabilities() =>
    [
        DriverCapability.Read,
        DriverCapability.Write,
        DriverCapability.BatchRead,   // 레지스터 범위 묶음 읽기 지원
    ];

    // §4 ─ 드라이버 생성 ───────────────────────────────────

    /// <summary>
    /// PLC 노드 하나당 드라이버 인스턴스 1개 생성.
    /// Collector 가 호출합니다.
    /// </summary>
    public IProtocolDriver CreateDriver() => new ModbusTcpDriver();
}
