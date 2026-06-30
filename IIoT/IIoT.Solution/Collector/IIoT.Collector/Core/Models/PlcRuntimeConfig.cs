// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Models/PlcRuntimeConfig.cs
//  역할: device.json PLC/Device 노드 → 수집 런타임용 평탄화 모델
//        DriverId/DriverParams → IProtocolDriver.ConnectAsync(DriverConfig) 변환 원천
//  C-01: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;

namespace IIoT.Collector.Core.Models;

/// <summary>
/// 수집 대상 PLC(또는 단독 통신 Device) 런타임 설정.
/// <para>
/// Studio 의 <c>DeviceNodeDto</c>(NodeType="PLC" 또는 "Device") 1개를 평탄화한 결과.
/// CommTypeMigrator.Resolve() 를 거쳐 <see cref="DriverId"/> 가 항상 확정된 상태로 보관된다.
/// </para>
/// </summary>
public sealed class PlcRuntimeConfig
{
    /// <summary>PLC/Device 고유 ID (device.json DeviceNodeDto.Id)</summary>
    public string PlcId { get; init; } = string.Empty;

    /// <summary>표시 이름</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 원본 노드 타입 ("PLC" 또는 "Device").
    /// 둘 다 동일하게 DriverId/DriverParams 기반으로 폴링하므로
    /// FlowEngine 입장에서는 차이 없음 (UI 표시 구분 용도).
    /// </summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>
    /// 플러그인 드라이버 ID.
    /// CommTypeMigrator.Resolve(dto.DriverId, dto.CommType) 결과 — 항상 값이 채워짐.
    /// CollectorPluginService.IsKnownDriver(DriverId) 로 등록 여부 확인 가능.
    /// </summary>
    public string DriverId { get; init; } = string.Empty;

    /// <summary>
    /// 드라이버 연결 파라미터.
    /// DriverConfig.Params 로 그대로 전달됨 (Key 는 ParameterDefinition.Key 와 일치해야 함).
    /// </summary>
    public Dictionary<string, string> DriverParams { get; init; } = new();

    /// <summary>폴링 주기 (ms) — DriverConfig.PollMs 로 전달</summary>
    public int PollMs { get; init; } = 1000;

    /// <summary>통신 타임아웃 (ms) — DriverConfig.TimeoutMs 로 전달</summary>
    public int TimeoutMs { get; init; } = 3000;

    /// <summary>이 PLC 하위 수집 대상 Tag 목록</summary>
    public List<TagRuntimeConfig> Tags { get; init; } = new();

    /// <summary>
    /// DriverConfig 로 변환합니다.
    /// C-03 FlowEngine 에서 driver.ConnectAsync(ToDriverConfig()) 호출 시 사용.
    /// </summary>
    public DriverConfig ToDriverConfig() => new(
        DriverId:  DriverId,
        PlcId:     PlcId,
        PollMs:    PollMs,
        TimeoutMs: TimeoutMs,
        Params:    DriverParams.Count > 0 ? new(DriverParams) : null
    );
}
