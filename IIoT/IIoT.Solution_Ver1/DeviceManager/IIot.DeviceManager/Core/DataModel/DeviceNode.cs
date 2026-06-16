// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/DeviceNode.cs
//  역할: lssLib.Config.ConfigNode 래퍼 — 장비 트리 계층 표현
//
//  계층 규칙 (Phase 1 Update):
//   - Group 은 N단계 자유 중첩 (폴더처럼 무제한)
//   - Device 는 Root / Group 어느 레벨에도 위치 가능
//   - Tag 는 반드시 Device 하위
//   - Group · Device 모두 LocationId 참조 가능
//   - Device 는 CommConfigId 참조 (통신 라이브러리 공유)
//
//  Phase 1 Update: N단계 중첩, LocationId, CommConfigId 추가
// ══════════════════════════════════════════════════════════

using lssLib.Config.Tree;

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// 장비 트리 노드 — lssLib.Config.ConfigNode 의 도메인 래퍼.
/// Group 은 하위에 Group 또는 Device 를 가질 수 있으며,
/// Device 는 트리 어느 레벨에도 위치할 수 있습니다.
/// </summary>
public record DeviceNode
{
    // §1 ─ 공통 식별 ──────────────────────────────────────────
    /// <summary>ConfigNode.Id 와 동일한 전역 고유 식별자</summary>
    public string NodeId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>표시명</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>설명</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>노드 종류 (lssLib.Config.Tree.NodeType)</summary>
    public NodeType Type { get; init; } = NodeType.Group;

    // §2 ─ 공통 참조 (Group · Device 모두 사용 가능) ──────────
    /// <summary>
    /// 위치 정보 참조 ID (location-library.json 의 Location.Id).
    /// Group(구역 단위) 또는 Device(설치 위치) 에 지정합니다.
    /// </summary>
    public string? LocationId { get; init; }

    // §3 ─ Device 전용 속성 ───────────────────────────────────
    /// <summary>
    /// 통신 설정 참조 ID (comm-library.json 의 CommConfig.Id).
    /// 같은 PLC 에 여러 Device 가 연결된 경우 동일한 CommConfigId 를 공유합니다.
    /// </summary>
    public string? CommConfigId { get; init; }

    /// <summary>제조사 (예: "Siemens", "Mitsubishi")</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>모델명 (예: "S7-300", "Q06UDEH")</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>시리얼 번호</summary>
    public string SerialNo { get; init; } = string.Empty;

    /// <summary>설치일 (ISO 8601, 예: "2024-03-15")</summary>
    public string InstallDate { get; init; } = string.Empty;

    // §4 ─ 타입 판별 헬퍼 ────────────────────────────────────
    public bool IsGroup => Type == NodeType.Group || Type == NodeType.Root;
    public bool IsDevice => Type == NodeType.Device;
    public bool IsTag => Type == NodeType.Tag || Type == NodeType.Sensor;

    // §5 ─ ConfigNode 변환 ────────────────────────────────────
    /// <summary>ConfigNode → DeviceNode 변환</summary>
    public static DeviceNode FromConfigNode(ConfigNode n) => new()
    {
        NodeId = n.Id,
        Name = n.Name,
        Type = n.Type,
        Description = n.GetProperty("description") ?? string.Empty,
        LocationId = n.GetProperty("locationId"),
        CommConfigId = n.GetProperty("commConfigId"),
        Manufacturer = n.GetProperty("manufacturer") ?? string.Empty,
        Model = n.GetProperty("model") ?? string.Empty,
        SerialNo = n.GetProperty("serialNo") ?? string.Empty,
        InstallDate = n.GetProperty("installDate") ?? string.Empty,
    };

    /// <summary>DeviceNode 속성을 ConfigNode.Properties 에 기록합니다.</summary>
    public void ApplyToConfigNode(ConfigNode n)
    {
        _SetIfNotEmpty(n, "description", Description);
        _SetIfNotEmpty(n, "locationId", LocationId);
        _SetIfNotEmpty(n, "commConfigId", CommConfigId);
        _SetIfNotEmpty(n, "manufacturer", Manufacturer);
        _SetIfNotEmpty(n, "model", Model);
        _SetIfNotEmpty(n, "serialNo", SerialNo);
        _SetIfNotEmpty(n, "installDate", InstallDate);
    }

    // §6 ─ 내부 헬퍼 ──────────────────────────────────────────
    private static void _SetIfNotEmpty(ConfigNode n, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            n.SetProperty(key, value);
    }
}