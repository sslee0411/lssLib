// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Models/DriverConfig.cs
//  역할: 드라이버 연결 설정 — ConnectAsync() 에 전달되는 런타임 설정
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// 드라이버 연결 설정.
/// <para>
/// device.json의 PLC 노드 데이터를 런타임에 드라이버로 전달할 때 사용한다.
/// DriverParams 는 ParameterDefinition.Key 와 1:1 대응한다.
/// </para>
/// <example>
/// <code>
/// var config = new DriverConfig(
///     DriverId:   "mitsubishi-mc",
///     PlcId:      "plc-001",
///     PollMs:     1000,
///     TimeoutMs:  3000,
///     Params:     new() { ["Host"] = "192.168.0.1", ["Port"] = "5007" }
/// );
/// await driver.ConnectAsync(config, ct);
/// </code>
/// </example>
/// </summary>
/// <param name="DriverId">드라이버 플러그인 ID (예: "modbus-tcp")</param>
/// <param name="PlcId">장비 트리 PLC 노드 ID</param>
/// <param name="PollMs">폴링 주기 (ms)</param>
/// <param name="TimeoutMs">통신 타임아웃 (ms)</param>
/// <param name="Params">드라이버별 파라미터 (Key=ParameterDefinition.Key)</param>
public sealed record DriverConfig(
    string                     DriverId,
    string                     PlcId,
    int                        PollMs    = 1000,
    int                        TimeoutMs = 3000,
    Dictionary<string, string>? Params   = null
);
