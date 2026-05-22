// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/Tag.cs
//  역할: 수집 태그 정의 — 주소·타입·스케일·알람그룹 참조 포함
//  Phase 1 Update: alarmRuleId → alarmGroupId 변경
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// 수집 태그 정의 레코드.
/// ConfigNode(Tag 타입)의 Properties 에서 역직렬화되어 사용됩니다.
/// </summary>
public record Tag
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string TagId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    // §2 ─ 통신 주소 ──────────────────────────────────────────
    /// <summary>레지스터 주소 (예: "40001", "M0.0", "ns=2;i=3")</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>바이너리 타입명 (lssLib.Binary.BufType 열거형 문자열)</summary>
    public string BufTypeName { get; init; } = "FloatBE";

    // §3 ─ 수집 제어 ──────────────────────────────────────────
    /// <summary>데드밴드 — 이 범위 이내 변화는 수집 생략 (공학 단위 기준)</summary>
    public double DeadBand { get; init; } = 0.0;

    /// <summary>
    /// 태그 개별 폴링 주기 (밀리초).
    /// 0 이면 Device 의 CommConfig.PollMs 를 따릅니다.
    /// </summary>
    public int PollMs { get; init; } = 0;

    // §4 ─ 스케일·알람 참조 ───────────────────────────────────
    /// <summary>ScaleConfig.Id 참조 — null 이면 스케일 없음</summary>
    public string? ScaleConfigId { get; init; }

    /// <summary>
    /// AlarmGroup.Id 참조 — null 이면 알람 없음.
    /// AlarmGroup 이 AlarmRule 목록과 수신자 목록을 관리합니다.
    /// </summary>
    public string? AlarmGroupId { get; init; }

    // §5 ─ 팩토리 ─────────────────────────────────────────────
    public static Tag FromProperties(string nodeId, string nodeName,
                                     IReadOnlyDictionary<string, string> props)
        => new()
        {
            TagId = nodeId,
            Name = nodeName,
            Address = props.GetValueOrDefault("address", string.Empty),
            BufTypeName = props.GetValueOrDefault("bufType", "FloatBE"),
            DeadBand = double.TryParse(props.GetValueOrDefault("deadBand", "0"),
                               out var db) ? db : 0.0,
            PollMs = int.TryParse(props.GetValueOrDefault("pollMs", "0"),
                               out var pm) ? pm : 0,
            ScaleConfigId = props.GetValueOrDefault("scaleConfigId"),
            AlarmGroupId = props.GetValueOrDefault("alarmGroupId"),
        };

    public Dictionary<string, string> ToProperties() => new()
    {
        ["address"] = Address,
        ["bufType"] = BufTypeName,
        ["deadBand"] = DeadBand.ToString("G"),
        ["pollMs"] = PollMs.ToString(),
        ["scaleConfigId"] = ScaleConfigId ?? string.Empty,
        ["alarmGroupId"] = AlarmGroupId ?? string.Empty,   // ← alarmRuleId → alarmGroupId
    };
}