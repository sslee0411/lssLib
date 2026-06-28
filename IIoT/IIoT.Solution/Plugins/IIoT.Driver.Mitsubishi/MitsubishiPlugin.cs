// ══════════════════════════════════════════════════════════
//  IIoT.Driver.Mitsubishi · MitsubishiPlugin.cs
//  역할: 미쓰비시 MELSEC MC 프로토콜 플러그인 진입점
//        대상: Q/L/iQ-R/iQ-F 시리즈 (TCP 3E/4E 프레임)
//
//  ★ 제조사 전용 프로토콜 드라이버 예제
//    Modbus 와 달리 제조사 고유 프레임 구조 사용
//    NetworkNo / PCNo / UnitNo 등 제조사 특유 파라미터 존재
//
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;

namespace IIoT.Driver.Mitsubishi;

/// <summary>
/// 미쓰비시 MELSEC MC 프로토콜 (3E 프레임) 플러그인.
/// Q/L/iQ-R/iQ-F 시리즈 이더넷 모듈과 통신합니다.
/// </summary>
[DriverMetadata(
    displayName:  "미쓰비시 MELSEC MC",
    description:  "MELSEC Q/L/iQ-R/iQ-F 시리즈 (MC 프로토콜 3E/4E 프레임)",
    vendor:       PlcVendor.Mitsubishi,
    supportWrite: true
)]
public sealed class MitsubishiPlugin : IProtocolPlugin
{
    // §1 ─ 메타데이터 ──────────────────────────────────────

    public string    PluginName      => "mitsubishi-mc";
    public string    PluginVersion   => "1.0.0";
    public PlcVendor SupportedVendor => PlcVendor.Mitsubishi;

    // §2 ─ 파라미터 스키마 ─────────────────────────────────

    /// <summary>
    /// 미쓰비시 MC 프로토콜 연결 파라미터.
    ///
    /// ★ 모듈(네트워크 카드) 설정과 일치해야 합니다:
    ///   GX Works → 파라미터 → 이더넷 포트 → 오픈 설정에서 확인
    /// </summary>
    public IReadOnlyList<ParameterDefinition> GetParameterSchema() =>
    [
        // ── 네트워크 설정 ────────────────────────────────
        new ParameterDefinition(
            Key:             "Host",
            DisplayName:     "IP 주소",
            Type:            ParameterType.String,
            DefaultValue:    "192.168.0.1",
            Description:     "MELSEC 이더넷 모듈 IP 주소",
            IsRequired:      true,
            ValidationRegex: @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"
        ),
        new ParameterDefinition(
            Key:          "Port",
            DisplayName:  "포트",
            Type:         ParameterType.Int,
            DefaultValue: "5007",
            Description:  "기본값 5007 (GX Works 이더넷 설정과 일치)",
            IsRequired:   true
        ),

        // ── MELSEC 고유 파라미터 ─────────────────────────
        new ParameterDefinition(
            Key:          "NetworkNo",
            DisplayName:  "네트워크 번호",
            Type:         ParameterType.Int,
            DefaultValue: "0",
            Description:  "MNET/H 네트워크 번호 (직결: 0)"
        ),
        new ParameterDefinition(
            Key:          "PCNo",
            DisplayName:  "PC 번호 (국번)",
            Type:         ParameterType.Int,
            DefaultValue: "255",
            Description:  "국번 (이더넷 직결 시 255 / MNET 경유 시 실제 국번)"
        ),
        new ParameterDefinition(
            Key:          "UnitIoNo",
            DisplayName:  "유닛 I/O 번호",
            Type:         ParameterType.String,
            DefaultValue: "03FF",
            Description:  "이더넷 유닛 선두 I/O 번호 (HEX, 기본 03FF)"
        ),
        new ParameterDefinition(
            Key:          "StationNo",
            DisplayName:  "멀티 드롭 국번",
            Type:         ParameterType.Int,
            DefaultValue: "0",
            Description:  "멀티 드롭 연결 시 사용 (직결: 0)"
        ),

        // ── 프레임 선택 ───────────────────────────────────
        new ParameterDefinition(
            Key:          "FrameType",
            DisplayName:  "프레임 타입",
            Type:         ParameterType.Enum,
            DefaultValue: "3E",
            Description:  "3E: 시리얼 번호 없음 / 4E: 시리얼 번호 포함",
            EnumValues:   ["3E", "4E"]
        ),

        // ── 타임아웃 ─────────────────────────────────────
        new ParameterDefinition(
            Key:          "TimeoutMs",
            DisplayName:  "타임아웃 (ms)",
            Type:         ParameterType.Int,
            DefaultValue: "5000"
        ),
    ];

    // §3 ─ 지원 기능 ───────────────────────────────────────

    public IReadOnlyList<DriverCapability> GetCapabilities() =>
    [
        DriverCapability.Read,
        DriverCapability.Write,
        DriverCapability.BatchRead,
    ];

    // §4 ─ 드라이버 생성 ───────────────────────────────────

    public IProtocolDriver CreateDriver() => new MitsubishiDriver();
}
