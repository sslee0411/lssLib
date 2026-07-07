// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Models/DeviceSnapshotDto.cs
//  역할: Collector GET /api/devices 응답 역직렬화용 DTO
//        Collector 측 DeviceInstance/TagInstance 의 부분집합.
//        (프로젝트 간 참조 없이 Monitor 측에서 독립적으로 정의)
//  MN-01B: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

namespace IIoT.Monitor.Models;

/// <summary>
/// Collector GET /api/devices 응답 1항목(PLC/Device 1개) DTO.
/// <para>
/// MN-01B 단계에서는 CollectorId 자동 동기화 + 개수 확인용으로만 사용한다.
/// PLC/Tag 상세 표시는 MN-02 에서 필드를 확장한다.
/// </para>
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

/// <summary>Collector 스냅샷 내 Tag 1개 DTO (MN-01B: 개수 확인용 최소 필드)</summary>
public sealed class TagSnapshotDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
