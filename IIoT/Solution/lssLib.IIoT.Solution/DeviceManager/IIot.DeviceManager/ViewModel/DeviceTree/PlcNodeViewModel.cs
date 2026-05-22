// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · PlcNodeViewModel.cs
//  역할: PLC 슬롯/채널 노드 ViewModel
//  생성: 2025-05-22
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Xml.Linq;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// PLC 노드 — 슬롯번호·Unit ID 포함, Tag 자식만 허용.
/// </summary>
public partial class PlcNodeViewModel : DeviceNodeViewModel
{
    // §1 ─ 속성 ───────────────────────────────────────────────

    [ObservableProperty]
    private int _slotNo;

    [ObservableProperty]
    private byte _unitId = 1;

    [ObservableProperty]
    private string _protocolType = "Modbus";

    // §2 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Plc;

    /// <summary>⚙️</summary>
    public override string IconGlyph => "⚙️";

    public override IReadOnlyList<NodeKind> AllowedChildKinds => [NodeKind.Tag];

    /// <summary>슬롯번호 배지</summary>
    public override string? Badge => $"#{SlotNo}";

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public PlcNodeViewModel(string name = "새 PLC", int slotNo = 0)
    {
        Name = name;
        SlotNo = slotNo;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SlotNo))
                OnPropertyChanged(nameof(Badge));
        };
    }
}