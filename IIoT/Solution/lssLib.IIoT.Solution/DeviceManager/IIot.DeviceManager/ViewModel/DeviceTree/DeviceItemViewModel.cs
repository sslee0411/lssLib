// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceItemViewModel.cs
//  역할: 실제 장비(Device) 노드 ViewModel
//  생성: 2025-05-22
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.DeviceManager.Core.DataModel;
using System.Windows.Documents;
using System.Xml.Linq;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 실제 장비 노드 — CommConfig 참조, PLC / Tag 자식 허용.
/// </summary>
public partial class DeviceItemViewModel : DeviceNodeViewModel
{
    // §1 ─ 속성 ───────────────────────────────────────────────

    [ObservableProperty]
    private string? _commConfigId;

    [ObservableProperty]
    private string _manufacturer = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _serialNo = string.Empty;

    [ObservableProperty]
    private string? _locationId;

    [ObservableProperty]
    private bool _isOnline;

    // §2 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Device;

    /// <summary>🖥️ (온라인) / ⬛ (오프라인)</summary>
    public override string IconGlyph => IsOnline ? "🖥️" : "📟";

    public override IReadOnlyList<NodeKind> AllowedChildKinds =>
        [NodeKind.Plc, NodeKind.Tag];

    public override string? Badge => CommConfigId is not null ? "COM" : null;

    public override string BadgeBrushKey =>
        IsOnline ? "SuccessBrush" : "MutedBrush";

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public DeviceItemViewModel(string name = "새 장비")
    {
        Name = name;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsOnline) or nameof(CommConfigId))
            {
                OnPropertyChanged(nameof(Badge));
                OnPropertyChanged(nameof(BadgeBrushKey));
                OnPropertyChanged(nameof(IconGlyph));
            }
        };
    }
}