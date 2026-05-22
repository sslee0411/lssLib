// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · TagNodeViewModel.cs
//  역할: 수집 태그(Tag) 리프 노드 ViewModel
//  생성: 2025-05-22
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// Tag 노드 — 리프 노드, 자식 없음.
/// 주소·BufType·PollMs·DeadBand 포함.
/// </summary>
public partial class TagNodeViewModel : DeviceNodeViewModel
{
    // §1 ─ 속성 ───────────────────────────────────────────────

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _bufType = "FloatBE";

    [ObservableProperty]
    private int _pollMs = 1000;

    [ObservableProperty]
    private double _deadBand;

    [ObservableProperty]
    private string? _scaleConfigId;

    [ObservableProperty]
    private string _unit = string.Empty;

    // §2 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Tag;

    /// <summary>🏷️</summary>
    public override string IconGlyph => "🏷️";

    /// <summary>Tag 는 자식 추가 불가</summary>
    public override IReadOnlyList<NodeKind> AllowedChildKinds => [];

    /// <summary>BufType 배지</summary>
    public override string? Badge => BufType;

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public TagNodeViewModel(string name = "새 태그")
    {
        Name = name;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BufType))
                OnPropertyChanged(nameof(Badge));
        };
    }
}