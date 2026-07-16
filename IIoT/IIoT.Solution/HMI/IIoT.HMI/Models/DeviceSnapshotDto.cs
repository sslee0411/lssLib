// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Models/DeviceSnapshotDto.cs
//  역할: Collector GET /api/devices 응답 역직렬화용 DTO
//        Collector 측 DeviceInstance/TagInstance 의 부분집합.
//        (프로젝트 간 참조 없이 HMI 측에서 독립적으로 정의 —
//         IIoT.Monitor Models/DeviceSnapshotDto.cs, MN-01B 이식)
//  HM-01: 신규 — CollectorId 자동 동기화 + 개수 확인용으로만 사용.
//  HM-05: 필드 확장 + 버그 수정 (아이콘↔Tag 바인딩에 실제 사용 시작)
//         ★ 버그 수정: Collector 는 MapGet("/api/devices")에서 DeviceInstance를
//           JsonNamingPolicy.CamelCase 로 그대로 직렬화한다 — Tag 하위 식별자
//           JSON 필드명은 "tagId" 인데, 기존 TagSnapshotDto 는 "Id" 로 선언되어
//           있어 실제로는 한 번도 매핑되지 못했다(항상 빈 문자열). "TagId" 로
//           수정하여 실제 GET /api/devices 응답과 일치시킴.
//         ★ 신규 필드: RawValue/EngValue/Unit 추가 — Tag 바인딩 시 최초 값
//           프리뷰(다음 SignalR TagValue Push 수신 전 초기 표시)에 사용.
//           ※ Quality 는 Collector 응답에서 enum 이 정수로 직렬화되어(문자열
//           변환 컨버터 미적용) 여기서는 매핑하지 않는다 — 실시간 Quality 는
//           SignalR "TagValue" Push payload(quality: 문자열)로만 반영한다.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

namespace IIoT.HMI.Models;

/// <summary>
/// Collector GET /api/devices 응답 1항목(PLC/Device 1개) DTO.
/// </summary>
public sealed class DeviceSnapshotDto
{
    /// <summary>이 스냅샷을 발행한 Collector 의 고유 ID (C-EX-10)</summary>
    public string CollectorId { get; set; } = string.Empty;

    public string PlcId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsConnected { get; set; }

    public bool IsPaused { get; set; }

    public List<TagSnapshotDto> Tags { get; set; } = new();
}

/// <summary>Collector 스냅샷 내 Tag 1개 DTO (바인딩 선택 + 초기값 프리뷰용 필드).</summary>
public sealed class TagSnapshotDto
{
    /// <summary>★ HM-05: Collector JSON 필드 "tagId" 와 일치하도록 수정(기존 "Id"는 매핑 안 됨)</summary>
    public string TagId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>★ HM-05 신규 — 초기값 프리뷰용(실시간 갱신은 SignalR TagValue Push로 처리)</summary>
    public double? RawValue { get; set; }

    /// <summary>★ HM-05 신규</summary>
    public double? EngValue { get; set; }

    /// <summary>★ HM-05 신규</summary>
    public string Unit { get; set; } = string.Empty;
}
