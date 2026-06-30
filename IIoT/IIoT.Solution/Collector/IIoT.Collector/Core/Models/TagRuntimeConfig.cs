// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Models/TagRuntimeConfig.cs
//  역할: device.json Tag 노드 → 수집 런타임용 평탄화 모델
//        FlowEngine 폴링 시 IProtocolDriver.ReadTagsAsync(TagReadRequest[])
//        호출의 원천 데이터로 사용
//  C-01: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Core.Models;

/// <summary>
/// 수집 대상 Tag 런타임 설정.
/// <para>
/// Studio의 <c>DeviceNodeDto</c>(NodeType="Tag") 1개를 평탄화한 결과.
/// 상위 PLC/Device 의 <see cref="PlcRuntimeConfig.PlcId"/> 에 종속된다.
/// </para>
/// </summary>
public sealed class TagRuntimeConfig
{
    /// <summary>Tag 고유 ID (device.json DeviceNodeDto.Id)</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Tag 표시 이름</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 레지스터 주소 문자열.
    /// IProtocolDriver.ReadTagsAsync() 의 TagReadRequest.Address 로 그대로 전달.
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// 데이터 타입 문자열 (예: "UInt16", "Float32", "Bool").
    /// IProtocolDriver.ReadTagsAsync() 의 TagReadRequest.DataType 으로 그대로 전달.
    /// </summary>
    public string DataType { get; init; } = string.Empty;

    /// <summary>공학 단위 (스케일 적용 후 표시 — C-05 ScaleEngine 에서 사용)</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>스케일 라이브러리 참조 ID (null = 원시값 그대로 사용)</summary>
    public string? ScaleEntryId { get; init; }

    /// <summary>알람 라이브러리 참조 ID (null = 알람 감지 안 함)</summary>
    public string? AlarmEntryId { get; init; }

    /// <summary>Tag 메모 (참고용)</summary>
    public string Memo { get; init; } = string.Empty;

    /// <summary>
    /// 수집 활성 여부.
    /// false 인 Tag 는 FlowEngine 폴링 대상에서 제외 (C-03 이후 적용).
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>이 Tag 가 속한 상위 PLC/Device ID (역참조 — 진단·UI 표시용)</summary>
    public string ParentPlcId { get; init; } = string.Empty;
}
