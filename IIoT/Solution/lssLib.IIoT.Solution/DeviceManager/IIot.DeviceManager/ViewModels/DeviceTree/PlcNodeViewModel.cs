// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · PlcNodeViewModel.cs
//  역할: PLC 슬롯/채널 노드 ViewModel
//  생성: 2025-05-22
//  수정: 2025-05-22 — XDG0008 수정
//        PropertyChanged 수동 구독 제거 (nameof(SlotNo) = 소스 제너레이터 의존)
//        → [NotifyPropertyChangedFor] 어트리뷰트로 교체
// ══════════════════════════════════════════════════════════

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// PLC 노드 — 슬롯번호·Unit ID 포함, Tag 자식만 허용.
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

    public override IReadOnlyList<NodeKind> AllowedChildKinds => [NodeKind.Tag];

    /// <summary>슬롯번호 배지 (예: #0, #1)</summary>
    public override string? Badge => $"#{SlotNo}";

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public PlcNodeViewModel(string name = "새 PLC", int slotNo = 0)
    {
        Name = name;
        SlotNo = slotNo;
    }
}