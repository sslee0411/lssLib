// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceItemViewModel.cs
//  역할: 실제 장비(Device) 노드 ViewModel
//  생성: 2025-05-22
//  수정: 2025-05-22 — XDG0008 수정
//        PropertyChanged 수동 구독 제거 (nameof(IsOnline) = 소스 제너레이터 의존)
//        → [NotifyPropertyChangedFor] 어트리뷰트로 교체 (공식 CommunityToolkit 패턴)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 실제 장비 노드 — CommConfig 참조, PLC / Tag 자식 허용.
/// </summary>
public partial class DeviceItemViewModel : DeviceNodeViewModel
{
    // §1 ─ 속성 ───────────────────────────────────────────────

    /// <summary>
    /// 통신 설정 ID 참조 (comm-library.json).
    /// 변경 시 Badge / BadgeBrushKey 자동 알림.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    [NotifyPropertyChangedFor(nameof(BadgeBrushKey))]
    private string? _commConfigId;

    [ObservableProperty]
    private string _manufacturer = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _serialNo = string.Empty;

    [ObservableProperty]
    private string? _locationId;

    /// <summary>
    /// 온라인 상태.
    /// 변경 시 Badge / BadgeBrushKey / IconGlyph 자동 알림.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    [NotifyPropertyChangedFor(nameof(BadgeBrushKey))]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    private bool _isOnline;

    // §2 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Device;

    /// <summary>온라인 = 🖥️, 오프라인 = 📟</summary>
    public override string IconGlyph => IsOnline ? "🖥️" : "📟";

    public override IReadOnlyList<NodeKind> AllowedChildKinds =>
        [NodeKind.Plc, NodeKind.Tag];

    /// <summary>CommConfig 연결 시 "COM" 배지 표시</summary>
    public override string? Badge => CommConfigId is not null ? "COM" : null;

    public override string BadgeBrushKey =>
        IsOnline ? "GreenBrush" : "Text3Brush";

    // §3 ─ 생성자 ─────────────────────────────────────────────

    /// <summary>
    /// ★ PropertyChanged 수동 구독 제거.
    ///   IsOnline · CommConfigId 는 소스 제너레이터([ObservableProperty])가 만드는 속성.
    ///   빌드 전에는 nameof(IsOnline) 이 컴파일 실패 → XDG0008 연쇄 발생.
    ///   → [NotifyPropertyChangedFor] 어트리뷰트로 연쇄 알림을 선언적으로 처리.
    /// </summary>
    public DeviceItemViewModel(string name = "새 장비")
    {
        Name = name;
    }
}
