// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Models/TagRuntimeConfig.cs
//  역할: device.json Tag 노드 → 수집 런타임용 평탄화 모델
//        FlowEngine 폴링 시 IProtocolDriver.ReadTagsAsync(TagReadRequest[])
//        호출의 원천 데이터로 사용
//  C-01: 신규
//  C-18: IsVirtual / Expression 추가 (가상/계산 Tag)
//  S-Virtual02: UseRoslynScript / ScriptCode 추가 (Function 노드)
//  S-프로토콜01 Step B: IsProtocolBlockField 추가 — 프로토콜 블록 필드에서
//               합성된 Tag 표시(placeholder). true 인 Tag 는 FlowEngine 의
//               일반 Tag 주소 폴링에서 제외되고(Address 가 실주소가 아닐 수
//               있음), 프로토콜 블록 폴링 경로에서만 값이 채워진다.
//  생성: 2026-06-29 / 수정: 2026-07-20
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

    // ★ C-18 신규 — 가상 Tag / 계산 Tag
    /// <summary>
    /// 가상(계산) Tag 여부. true 면 드라이버 폴링 대상에서 제외되고,
    /// VirtualTagEngine 이 Expression 을 평가하여 값을 계산·발행한다.
    /// </summary>
    public bool IsVirtual { get; init; } = false;

    /// <summary>
    /// 계산식 (NCalc). 다른 Tag 값은 <c>[TagId]</c> 형태로 참조한다.
    /// 예: "[T001] + [T002] * 0.5"
    /// IsVirtual=false 인 Tag 에서는 사용하지 않음. UseRoslynScript=true 일 때도 미사용.
    /// </summary>
    public string? Expression { get; init; }

    // ★ S-Virtual02 신규 — Function 노드(Roslyn C# 고급 스크립트 모드)
    /// <summary>true 면 Expression(NCalc) 대신 ScriptCode(Roslyn C#)로 값을 계산한다.
    /// IsVirtual=false 인 Tag 에서는 사용하지 않음.</summary>
    public bool UseRoslynScript { get; init; } = false;

    /// <summary>Roslyn C# 스크립트 코드. UseRoslynScript=true 일 때만 사용.
    /// 스크립트 안에서 VirtualTagScriptContext 의 public 멤버(Values/Result/Suppress)를
    /// 한정자 없이 바로 참조할 수 있다.</summary>
    public string? ScriptCode { get; init; }

    // ★ S-프로토콜01 Step B 신규 — 프로토콜 블록 필드 합성 Tag
    /// <summary>
    /// true 면 이 Tag 는 PLC/장비에 연결된 프로토콜 라이브러리 블록의 필드에서
    /// CollectorConfigLoader 가 자동 합성한 placeholder 이다(Studio 에서 직접
    /// 만든 Tag 가 아님). Address 는 실제 폴링에 쓰이지 않으므로 비어있을 수
    /// 있고, FlowEngine 의 일반 Tag 주소 폴링(_PollOnceAsync 의 enabledTags)
    /// 에서 제외되며, 대신 프로토콜 블록 폴링 경로(_PollProtocolBlocksAsync)
    /// 에서만 값이 채워진다. DeviceInstance 트리에는 그대로 포함되어 Monitor/
    /// HMI 화면에서 일반 Tag 와 동일하게 조회·표시된다.
    /// </summary>
    public bool IsProtocolBlockField { get; init; } = false;
}
