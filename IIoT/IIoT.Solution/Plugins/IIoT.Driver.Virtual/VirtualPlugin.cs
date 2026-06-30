// ══════════════════════════════════════════════════════════
//  IIoT.Driver.Virtual · VirtualPlugin.cs
//  역할: 가상(시뮬레이터) 드라이버 플러그인 진입점
//        실제 PLC 없이 Studio/Collector 기능을 테스트하기 위한 내장 드라이버
//        ModbusTcpPlugin.cs 패턴 그대로 복사 + 파라미터만 교체
//  C-02: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;

namespace IIoT.Driver.Virtual;

/// <summary>
/// 가상 드라이버 플러그인.
/// <para>
/// 실 PLC 연결 없이 Sine(사인파) 또는 Fixed(고정값) 모드로
/// 가짜 Tag 값을 생성한다. Collector 파이프라인(C-03 FlowEngine 이후)
/// 전체를 실 장비 없이 검증하는 용도.
/// </para>
/// </summary>
[DriverMetadata(
    displayName:  "가상 드라이버 (시뮬레이터)",
    description:  "실 PLC 없이 테스트용 값을 생성 — Sine/Fixed 모드 지원",
    vendor:       PlcVendor.Free,
    supportWrite: false
)]
public sealed class VirtualPlugin : IProtocolPlugin
{
    // §1 ─ 메타데이터 ──────────────────────────────────────

    /// <summary>
    /// ★ driverId 로 사용됩니다.
    /// device.json 의 driverId 필드 값과 반드시 일치해야 합니다.
    /// </summary>
    public string    PluginName      => "virtual";
    public string    PluginVersion   => "1.0.0";
    public PlcVendor SupportedVendor => PlcVendor.Free;

    // §2 ─ 파라미터 스키마 ─────────────────────────────────

    /// <summary>
    /// Studio PLC 편집기에 자동으로 렌더링될 입력 폼을 정의합니다.
    /// HANDOFF C-02 설계 기준: SimMode / FixedValue / Min / Max / PeriodSec
    /// </summary>
    public IReadOnlyList<ParameterDefinition> GetParameterSchema() =>
    [
        new ParameterDefinition(
            Key:          "SimMode",
            DisplayName:  "시뮬레이션 모드",
            Type:         ParameterType.Enum,
            DefaultValue: "Sine",
            Description:  "Sine: 사인파 변동 / Fixed: 고정값",
            EnumValues:   ["Sine", "Fixed"]
        ),
        new ParameterDefinition(
            Key:          "FixedValue",
            DisplayName:  "고정값",
            Type:         ParameterType.String,
            DefaultValue: "0",
            Description:  "SimMode=Fixed 일 때 반환할 값"
        ),
        new ParameterDefinition(
            Key:          "Min",
            DisplayName:  "최소값",
            Type:         ParameterType.Int,
            DefaultValue: "0",
            Description:  "SimMode=Sine 일 때 사인파 최소값"
        ),
        new ParameterDefinition(
            Key:          "Max",
            DisplayName:  "최대값",
            Type:         ParameterType.Int,
            DefaultValue: "100",
            Description:  "SimMode=Sine 일 때 사인파 최대값"
        ),
        new ParameterDefinition(
            Key:          "PeriodSec",
            DisplayName:  "주기 (초)",
            Type:         ParameterType.Int,
            DefaultValue: "10",
            Description:  "사인파 1주기 시간 (초)"
        ),
    ];

    // §3 ─ 지원 기능 ───────────────────────────────────────

    public IReadOnlyList<DriverCapability> GetCapabilities() =>
    [
        DriverCapability.Read,
        DriverCapability.BatchRead,
        // ★ Write 미지원: 가상 드라이버는 읽기 전용 (DriverMetadata supportWrite: false 와 일치)
    ];

    // §4 ─ 드라이버 생성 ───────────────────────────────────

    public IProtocolDriver CreateDriver() => new VirtualDriver();
}
