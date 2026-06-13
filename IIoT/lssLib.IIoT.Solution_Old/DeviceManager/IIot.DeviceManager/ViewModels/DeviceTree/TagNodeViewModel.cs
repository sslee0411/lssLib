// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · TagNodeViewModel.cs
//  역할: PLC 레지스터 주소 노드 ViewModel (수집 레이어)
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — Tag/Sensor 이중 레이어 구조 반영
//        ScaleConfigId, AlarmGroupId → Sensor 로 이동
//  수정: 2025-05-23 v3 — OwnerDeviceId 추가
//        수집 주체 명시 지정 (null = 트리 상향 탐색 자동 결정)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// PLC 레지스터 주소 노드 — 수집 레이어.
///
/// Tag는 "어디서 읽느냐"만 알고, "무엇을 보여주느냐"는 모릅니다.
/// ScaleConfig, AlarmConfig는 Sensor(물리 레이어)가 소유합니다.
///
/// ── 수집 주체 결정 (OwnerDeviceId) ──────────────────────
///
///   OwnerDeviceId == null (기본):
///     트리 상향 탐색으로 자동 결정
///     부모 → 조부모 순으로 CommConfigId 있는 첫 번째 노드
///
///   OwnerDeviceId != null (명시):
///     지정된 Device/Plc ID가 수집 주체 (트리 변경에도 고정)
///
/// ── 배치 위치 ────────────────────────────────────────────
///
///   ⚙️ PLC-001
///     📋 temp_raw     ← Plc 직속 (OwnerDeviceId=null → PLC-001)
///     📦 온도변환기   ← 필드 장비 (CommConfig=HART)
///         📋 측정값  ← Device 직속 (null → 온도변환기 / 또는 명시)
/// </summary>
public partial class TagNodeViewModel : DeviceNodeViewModel
{
    // §1 ─ 수집 주소 속성 ─────────────────────────────────────

    /// <summary>
    /// PLC 레지스터 주소.
    /// Modbus: "40001", "D100"
    /// OPC-UA: "ns=2;i=1003"
    /// Siemens: "MW100", "M0.0", "DB1.DBW0"
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    private string _address = "";

    /// <summary>
    /// 데이터 타입 (BufType).
    /// lssLib.Binary.BufType 열거형 문자열.
    /// 예: "FloatBE", "Int16BE", "UInt16BE", "Bool"
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    private string _bufType = "FloatBE";

    /// <summary>
    /// 폴링 주기 (밀리초).
    /// 0이면 수집 주체 Device/Plc의 CommConfig.PollMs 를 따름.
    /// </summary>
    [ObservableProperty] private int _pollMs = 1000;

    /// <summary>
    /// DeadBand — 이 범위 이내 변화는 수집 생략 (원시값 기준).
    /// 0이면 모든 변화 수집.
    /// </summary>
    [ObservableProperty] private double _deadBand;

    // §2 ─ 수집 주체 ★ 신규 ─────────────────────────────────

    /// <summary>
    /// 수집 주체 Device/Plc의 NodeId (명시 지정).
    ///
    /// null (기본값):
    ///   트리 상향 탐색으로 수집 주체 자동 결정.
    ///   부모 → 상위 순으로 CommConfigId 있는 첫 번째 노드가 수집.
    ///
    /// not null:
    ///   지정된 NodeId의 Device/Plc가 수집 주체.
    ///   트리 재구성 후에도 수집 주체 고정됨.
    ///   예) PLC 하위 필드 장비의 Tag를 상위 PLC가 대신 수집
    ///
    /// 변경 시 Badge 갱신 → "자동" / "고정:DeviceName" 표시.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    [NotifyPropertyChangedFor(nameof(OwnerLabel))]
    private string? _ownerDeviceId;

    // §3 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Tag;

    public override string IconGlyph => "📋";

    /// <summary>Tag는 리프 노드 — 자식 없음</summary>
    public override IReadOnlyList<NodeKind> AllowedChildKinds => [];

    /// <summary>
    /// 배지:
    ///   OwnerDeviceId 지정 시 → "🔒 고정" (수집 주체 명시)
    ///   미지정 시             → BufType (예: FloatBE)
    /// </summary>
    public override string? Badge => OwnerDeviceId is not null
        ? "🔒 고정"
        : (string.IsNullOrEmpty(BufType) ? null : BufType);

    /// <summary>
    /// 수집 주체 레이블 (UI 표시용).
    ///   null     → "자동 (트리 상향)"
    ///   not null → "고정: {OwnerDeviceId[..8]}"
    /// </summary>
    public string OwnerLabel => OwnerDeviceId is null
        ? "자동 (트리 상향)"
        : $"고정: {OwnerDeviceId[..Math.Min(8, OwnerDeviceId.Length)]}...";

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public TagNodeViewModel(string name = "새 태그")
    {
        Name = name;
    }

    // §5 ─ 헬퍼 ──────────────────────────────────────────────

    /// <summary>
    /// 트리 상향 탐색으로 수집 주체 DeviceNodeViewModel 을 찾습니다.
    ///
    /// OwnerDeviceId 명시 시: 트리 전체에서 해당 Id 탐색
    /// null 시: 부모 → 상위 순으로 CommConfigId 있는 첫 번째 노드 반환
    /// </summary>
    public DeviceNodeViewModel? ResolveOwner()
    {
        // CASE 1: 명시 지정
        if (OwnerDeviceId is not null)
            return _FindNodeById(Parent, OwnerDeviceId)
                   ?? _FindNodeById_Up(Parent, OwnerDeviceId);

        // CASE 2: 트리 상향 탐색 — CommConfigId 있는 첫 번째 노드
        var cursor = Parent;
        while (cursor is not null)
        {
            if (cursor is DeviceItemViewModel dev && dev.CommConfigId is not null)
                return dev;
            if (cursor is PlcNodeViewModel)
                return cursor;   // Plc 자체가 CommConfig 보유
            cursor = cursor.Parent;
        }
        return null; // 루트까지 없음 → 수집 불가
    }

    // §6 ─ 내부 탐색 헬퍼 ────────────────────────────────────

    /// <summary>현재 노드의 Children 재귀 탐색</summary>
    private static DeviceNodeViewModel? _FindNodeById(
        DeviceNodeViewModel? root, string id)
    {
        if (root is null) return null;
        foreach (var n in root.Flatten())
            if (n.Id == id) return n;
        return null;
    }

    /// <summary>부모 방향으로 올라가며 Id 탐색 (형제·삼촌 포함)</summary>
    private static DeviceNodeViewModel? _FindNodeById_Up(
        DeviceNodeViewModel? node, string id)
    {
        var cursor = node;
        while (cursor is not null)
        {
            if (cursor.Id == id) return cursor;
            cursor = cursor.Parent;
        }
        return null;
    }
}