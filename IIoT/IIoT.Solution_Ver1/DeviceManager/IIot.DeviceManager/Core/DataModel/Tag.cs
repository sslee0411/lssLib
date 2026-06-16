// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/Tag.cs
//  역할: 수집 태그 정의 — 주소·타입·수집주체 참조 포함
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — alarmRuleId → alarmGroupId 변경
//  수정: 2025-05-23 v3 — OwnerDeviceId 추가
//        ScaleConfigId, AlarmGroupId → Sensor 로 이동
//        Tag는 수집 주소 + 수집 주체만 보유
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// 수집 태그 정의 레코드.
/// ConfigNode(Tag 타입)의 Properties 에서 역직렬화되어 사용됩니다.
///
/// ── 수집 주체 결정 규칙 (OwnerDeviceId 상향 탐색) ──────────
///
///   CASE 1: OwnerDeviceId == null  →  트리 상향 탐색
///     Tag 부모가 Plc  → Plc.CommConfig 로 수집
///     Tag 부모가 Device → 상위로 올라가며 CommConfigId 있는
///                         첫 번째 노드(Device/Plc)가 수집 주체
///
///   CASE 2: OwnerDeviceId != null  →  명시 지정 우선
///     해당 Device/Plc ID가 수집 주체
///     트리 구조가 바뀌어도 수집 주체가 변하지 않아야 할 때 사용
///
/// ── 예시 구조 ────────────────────────────────────────────
///
///   ⚙️ PLC-001 (CommConfig: ModbusTCP / 192.168.1.10)
///     📋 temp_raw     OwnerDeviceId=null  → PLC-001 이 수집
///     📦 온도변환기-001 (Device, CommConfig: HART / CH1)
///         📋 측정값   OwnerDeviceId=null  → 온도변환기-001 이 수집
///                                           (CommConfigId 있으므로)
///         📋 설정값   OwnerDeviceId="plc-001-id" → 강제로 PLC-001 이 수집
///         🌡️ 베어링온도1 (Sensor, TagRef: 측정값)
///
/// ── 모니터링 매칭 흐름 ──────────────────────────────────
///
///   수집 프로그램: Tag.RawValue 수집 (OwnerDevice 기준 통신)
///       ↓
///   Sensor.TagRefs 역참조: TagId → Tag.RawValue
///       ↓ (Scale 적용)
///   Sensor.ScaledValue → EventBus.Publish(SensorValueEvent)
///       ↓
///   모니터링: SensorId 기준으로 실 데이터 표시
///             Tag 주소 변경 / PLC 교체 시 Sensor 설정 변경 없음
/// </summary>
public record Tag
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string TagId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    // §2 ─ 통신 주소 ──────────────────────────────────────────
    /// <summary>
    /// 레지스터 주소.
    /// Modbus: "40001", "M0.0"
    /// OPC-UA: "ns=2;i=3"
    /// Siemens: "MW100", "DB1.DBW0"
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// 바이너리 타입명 (lssLib.Binary.BufType 열거형 문자열).
    /// 예: "FloatBE", "Int16BE", "UInt16BE", "Bool"
    /// </summary>
    public string BufTypeName { get; init; } = "FloatBE";

    // §3 ─ 수집 제어 ──────────────────────────────────────────
    /// <summary>
    /// 데드밴드 — 이 범위 이내 변화는 수집 생략 (원시값 기준).
    /// 0이면 모든 변화 수집.
    /// </summary>
    public double DeadBand { get; init; } = 0.0;

    /// <summary>
    /// 태그 개별 폴링 주기 (밀리초).
    /// 0이면 수집 주체 Device/Plc의 CommConfig.PollMs 를 따름.
    /// </summary>
    public int PollMs { get; init; } = 0;

    // §4 ─ 수집 주체 ★ 신규 ────────────────────────────────────
    /// <summary>
    /// 수집 주체 Device/Plc의 NodeId (명시 지정).
    ///
    /// null (기본값) → 트리 상향 탐색으로 수집 주체 자동 결정:
    ///   1. 부모 노드가 CommConfigId를 가지면 → 해당 부모가 수집
    ///   2. 없으면 → 상위로 올라가며 CommConfigId 있는 첫 번째 노드
    ///   3. 루트까지 없으면 → 수집 불가 (설정 오류)
    ///
    /// not null → 지정된 NodeId의 Device/Plc가 수집 주체.
    ///   · 트리 재구성 후에도 수집 주체 고정
    ///   · 필드 장비의 Tag를 상위 PLC가 대신 수집하는 경우 사용
    /// </summary>
    public string? OwnerDeviceId { get; init; }

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
            OwnerDeviceId = props.GetValueOrDefault("ownerDeviceId"),  // null 허용
        };

    public Dictionary<string, string> ToProperties() => new()
    {
        ["address"] = Address,
        ["bufType"] = BufTypeName,
        ["deadBand"] = DeadBand.ToString("G"),
        ["pollMs"] = PollMs.ToString(),
        ["ownerDeviceId"] = OwnerDeviceId ?? string.Empty,
    };
}