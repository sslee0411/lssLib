// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · PlcNodeViewModel.cs
//  역할: PLC 슬롯/채널 노드 ViewModel
//  생성: 2025-05-22
//  수정: 2025-05-22 — XDG0008 수정 ([NotifyPropertyChangedFor] 패턴)
//  수정: 2025-05-23 v2 — 트리 구조 유연화
//        AllowedChildKinds 에 Device / Plc 추가
//        → PLC 하위에 PLC / 장비를 중첩 연결할 수 있음
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// PLC 노드 — 슬롯번호·Unit ID 포함.
/// 하위 노드: Device · PLC (중첩 가능) · Tag
/// </summary>
public partial class PlcNodeViewModel : DeviceNodeViewModel
{
    // §1 ─ 속성 ───────────────────────────────────────────────

    /// <summary>슬롯번호. 변경 시 Badge 자동 알림.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    private int _slotNo;

    [ObservableProperty]
    private byte _unitId = 1;

    [ObservableProperty]
    private string _protocolType = "Modbus";

    // §2 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Plc;

    public override string IconGlyph => "⚙️";

    /// <summary>
    /// ★ v2 수정: Device · Plc · Tag 모두 허용
    ///   → PLC 하위에 PLC 또는 장비를 중첩 연결할 수 있음
    ///   예) PLC-001 → PLC-001-CH1 (채널) → Tag
    /// </summary>
    public override IReadOnlyList<NodeKind> AllowedChildKinds =>
        [NodeKind.Device, NodeKind.Plc, NodeKind.Tag];

    /// <summary>슬롯번호 배지 (예: #0, #1)</summary>
    public override string? Badge => $"#{SlotNo}";

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public PlcNodeViewModel(string name = "새 PLC", int slotNo = 0)
    {
        Name = name;
        SlotNo = slotNo;
    }
}