// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceItemViewModel.cs
//  역할: 실제 장비(Device/PLC) 노드 ViewModel
//  생성: 2025-05-22
//  수정: 2025-05-22 — XDG0008 수정 ([NotifyPropertyChangedFor] 패턴)
//  수정: 2025-05-23 v2 — 트리 구조 유연화
//        AllowedChildKinds 에 Device / Plc 추가
//        → 장비(PLC) 하위에 장비(PLC)를 연결할 수 있음
//  수정: 2025-05-23 v3 — Tag/Sensor 이중 레이어 구조 반영
//  수정: 2025-05-23 v4 — Device 하위 Tag 허용 (PLC 하위 필드 장비 지원)
//        AllowedChildKinds: Tag 제거, Sensor 추가
//        Device 하위: [Device, Plc(수집), Sensor(물리)]
//        Tag는 Plc 하위에만 위치 → Device 직접 하위 불가
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;


/// <summary>
/// 실 장비 노드 — 수집 레이어(PLC/Tag)와 물리 레이어(Sensor) 모두 포함.
///
/// 트리 구조:
///   📦 압연기-001 (DeviceItemViewModel)
///     ├── [수집 레이어]
///     │   🔌 PLC-SIEMENS (Plc)
///     │       📋 temp_raw (Tag) ← Tag는 Plc 하위에만
///     └── [물리 레이어]
///         🌡️ 베어링온도1 (Sensor) ← Sensor는 Device 하위 직접
///         💧 차압센서1   (Sensor)
///
/// AllowedChildKinds: [Device, Plc, Sensor]  (Tag 직접 추가 불가)
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

    [ObservableProperty] private string _manufacturer = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _serialNo = "";
    [ObservableProperty] private string? _locationId;

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

    /// <summary>
    /// ★ v4 수정: Tag 추가 (Device 독립/PLC하위 모두 허용)
    ///
    /// Device는 두 가지 사용 방식을 지원:
    ///   ① 독립 장비 (Group/루트 하위):
    ///       Device → [Device, Plc, Sensor]
    ///       장비 내부 PLC 통신 + 물리 센서 구성
    ///
    ///   ② PLC 하위 필드 장비 (PLC에 연결된 세부 장비):
    ///       PLC → Device → [Tag, Device, Sensor]
    ///       HART/Profibus 등 자체 통신으로 태그 직접 연결
    ///       예) PLC → 온도변환기(Device) → 측정값(Tag), 설정값(Tag)
    ///
    /// Tag를 AllowedChildKinds에 포함하여 두 방식 모두 지원.
    /// 실제 배치 위치(루트/그룹/PLC 하위)에 따라 사용자가 의미 구분.
    /// </summary>
    public override IReadOnlyList<NodeKind> AllowedChildKinds =>
        [NodeKind.Device, NodeKind.Plc, NodeKind.Tag, NodeKind.Sensor];

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