// ══════════════════════════════════════════════════════════
//  IIoT.Driver.RawFrame · RawFramePlugin.cs
//  역할: 커스텀 바이트 프레임(STX/LEN/CMD/DATA/CRC) 플러그인 진입점
//        Studio 프로토콜 라이브러리(S-프로토콜01)의 "커스텀 프레임" 블록
//        (CmdCode 가 채워진 ProtocolBlock)을 실제로 실행하는 전용 드라이버.
//        Tag 주소 단위 통신은 지원하지 않음(ReadTagsAsync/WriteTagAsync 는
//        Fail 반환) — 반드시 프로토콜 라이브러리의 블록을 통해서만 사용한다.
//  S-프로토콜01 Step B: 신규
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;

namespace IIoT.Driver.RawFrame;

/// <summary>
/// 커스텀 프레임(STX/LEN/CMD/DATA/CRC) 전용 플러그인.
/// PLC/장비가 표준 Modbus/미쓰비시 MC 프로토콜이 아닌 독자 바이트 프레임을
/// 사용할 때, Studio 프로토콜 라이브러리에서 "커스텀 프레임 사용"을 체크하고
/// 이 드라이버(raw-frame)를 PLC/장비에 연결해 사용한다.
/// </summary>
[DriverMetadata(
    displayName:  "Raw Frame (커스텀 프레임)",
    description:  "STX/LEN/CMD/DATA/CRC 커스텀 바이트 프레임 — 프로토콜 라이브러리 블록 전용",
    vendor:       PlcVendor.Free,
    supportWrite: true
)]
public sealed class RawFramePlugin : IProtocolPlugin
{
    public string    PluginName      => "raw-frame";
    public string    PluginVersion   => "1.0.0";
    public PlcVendor SupportedVendor => PlcVendor.Free;

    public IReadOnlyList<ParameterDefinition> GetParameterSchema() =>
    [
        new ParameterDefinition(
            Key:             "Host",
            DisplayName:     "IP 주소",
            Type:            ParameterType.String,
            DefaultValue:    "192.168.0.1",
            Description:     "장비 IP 주소 또는 호스트명",
            IsRequired:      true,
            ValidationRegex: @"^[\w\.\-]+$"
        ),
        new ParameterDefinition(
            Key:          "Port",
            DisplayName:  "포트",
            Type:         ParameterType.Int,
            DefaultValue: "9000",
            IsRequired:   true
        ),
        new ParameterDefinition(
            Key:          "TimeoutMs",
            DisplayName:  "타임아웃 (ms)",
            Type:         ParameterType.Int,
            DefaultValue: "3000"
        ),
    ];

    public IReadOnlyList<DriverCapability> GetCapabilities() =>
    [
        DriverCapability.Read,
        DriverCapability.Write,
        DriverCapability.BatchRead,
    ];

    public IProtocolDriver CreateDriver() => new RawFrameDriver();
}
