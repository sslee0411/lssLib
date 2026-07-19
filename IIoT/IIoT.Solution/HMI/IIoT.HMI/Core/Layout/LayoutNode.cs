// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Layout/LayoutNode.cs
//  역할: 레이아웃 편집 캔버스의 장비 아이콘 노드 모델
//        (IIoT.Studio Core/Canvas/CanvasNode.cs 중 "포트/연결선 제외" 부분만
//         이식 — HMI 캔버스는 NodeRed 식 흐름 연결이 아니라 프리폼 배치용이므로
//         NodePort/AbstractCanvasNode 의 Input/OutputPorts 는 가져오지 않는다)
//  HM-03: 신규 — AbstractLayoutNode(프리폼 배치 뼈대) + GenericIconNode(placeholder)
//  HM-04: 장비 노드 4종 추가 (MotorNode/ConveyorNode/TankNode/ValveNode) —
//         뷰 쪽은 Views/DeviceControls/DeviceControlBase 를 상속하는 대응 컨트롤
//         (MotorControl 등)이 담당하며, LayoutCanvasView.xaml 의 DataTemplate
//         (DataType 매핑)이 모델↔컨트롤을 자동 연결한다.
//         ★ 확장 방법(신규 장비 추가): 아래 3단계만 반복하면 된다.
//           1) 이 파일에 AbstractLayoutNode 파생 클래스 추가
//           2) Views/DeviceControls/ 에 DeviceControlBase 상속 컨트롤 추가
//           3) LayoutCanvasView.xaml Resources 에 DataTemplate 1개 추가
//           (LayoutNodeFactory.Create/PaletteItems 에도 등록 필요)
//  HM-05: AbstractLayoutNode 에 Tag 바인딩 필드(BoundCollectorId/BoundPlcId/
//         BoundTagId/BoundTagName)와 실시간 표시용 필드(ValueText/ValueQuality)
//         추가. 특정 장비 타입 전용이 아니라 모든 노드가 공통으로 가지는 값이므로
//         베이스 클래스에 둔다(HM-07 레이아웃 저장 시에도 그대로 직렬화 대상).
//  HM-06: EngValue(double?) 추가 — ValueText 는 사람이 읽는 문자열("23.4 ℃")이라
//         애니메이션 속도/게이지 계산에 그대로 쓰기 어려우므로, 숫자값을 별도로
//         보관한다. Views/DeviceControls 의 각 장비 컨트롤(Motor/Conveyor/Tank/
//         Valve)이 이 값의 변화를 구독해 회전/흐름/수위/개폐 애니메이션을 구동한다.
//  HM-07: ZIndex(int) 추가 — 카드 간 겹침 순서(Z-레벨) 우선순위. 특정 장비 타입
//         전용이 아니라 모든 노드가 공통으로 가지는 값이므로 베이스에 둔다.
//         LayoutCanvasView.xaml 의 NodesLayer ItemContainerStyle 에서
//         Panel.ZIndex 로 바인딩되며, LayoutCanvasViewModel 의 BringToFront/
//         SendToBack/BringForward/SendBackward 커맨드가 이 값을 조정한다.
//         레이아웃 저장(hmi-layout.json)에도 그대로 포함된다.
//  HM-08: 알람 상태 필드 추가(HasActiveAlarm/AlarmKey/AlarmLevel/AlarmStatusText/
//         AlarmMessage/AlarmTimeText) — Collector의 AlarmChanged 이벤트를 구독한
//         LayoutCanvasViewModel 이 갱신한다. 모든 장비 타입 공통이므로 베이스에
//         둔다. Views/DeviceControls/DeviceControlBase 의 알람 배지+팝업이 이
//         값을 바인딩해 표시하고, ACK 버튼은 AlarmKey 를 커맨드 파라미터로 사용한다.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.HMI.Core.Layout;

// §1 ─ 노드 크기 상수 ─────────────────────────────────────

public static class LayoutNodeLayout
{
    public const double Width  = 120;
    public const double Height = 90;
}

// §2 ─ 추상 노드 ──────────────────────────────────────────

/// <summary>
/// 레이아웃 캔버스에 배치되는 아이콘 노드의 공통 기반.
/// <para>
/// Studio AbstractCanvasNode 와 달리 포트(InputPorts/OutputPorts)가 없다 —
/// HMI 캔버스는 노드 간 흐름을 연결하는 용도가 아니라, 화면 위에 장비를
/// 자유롭게 배치하고 실시간 Tag 값을 표시하는 용도이기 때문이다.
/// </para>
/// </summary>
public abstract partial class AbstractLayoutNode : ObservableObject
{
    public string NodeId { get; } = Guid.NewGuid().ToString();

    public abstract string NodeType      { get; }
    public abstract string DisplayLabel  { get; }
    public abstract string IconGlyph     { get; }
    public abstract string CategoryColor { get; }

    [ObservableProperty] private string _label = string.Empty;

    [ObservableProperty] private double _x = 100;
    [ObservableProperty] private double _y = 100;

    /// <summary>★ HM-07: Z-레벨(겹침 순서) — 값이 클수록 위에 그려짐. 기본값 0.</summary>
    [ObservableProperty] private int _zIndex;

    [ObservableProperty] private bool _isSelected;

    // §2-1 ─ HM-05: Tag 바인딩 ────────────────────────────

    /// <summary>바인딩된 Collector ID (미바인딩 시 빈 문자열)</summary>
    [NotifyPropertyChangedFor(nameof(IsBound))]
    [ObservableProperty] private string _boundCollectorId = string.Empty;

    /// <summary>바인딩된 PLC/Device ID (미바인딩 시 빈 문자열)</summary>
    [NotifyPropertyChangedFor(nameof(IsBound))]
    [ObservableProperty] private string _boundPlcId = string.Empty;

    /// <summary>바인딩된 Tag ID (미바인딩 시 빈 문자열)</summary>
    [NotifyPropertyChangedFor(nameof(IsBound))]
    [ObservableProperty] private string _boundTagId = string.Empty;

    /// <summary>바인딩된 Tag 표시 이름 (속성 패널·툴팁 표시용)</summary>
    [ObservableProperty] private string _boundTagName = string.Empty;

    /// <summary>카드에 표시되는 실시간 값 텍스트 (미바인딩 시 "-")</summary>
    [ObservableProperty] private string _valueText = "-";

    /// <summary>최근 수신한 Tag Quality 문자열 ("Good"/"Bad"/"Timeout"/"Disconnected", 미바인딩 시 "")</summary>
    [ObservableProperty] private string _valueQuality = string.Empty;

    /// <summary>★ HM-06: 애니메이션 계산용 숫자값(EngValue 우선, 없으면 RawValue). 미바인딩/값없음 시 null</summary>
    [ObservableProperty] private double? _engValue;

    /// <summary>Tag 가 바인딩되어 있는지 여부 — 카드에 값 표시줄을 보여줄지 결정</summary>
    public bool IsBound => !string.IsNullOrEmpty(BoundTagId);

    // §2-2 ─ HM-08: 알람 오버레이 ─────────────────────────

    /// <summary>현재 이 노드에 활성 알람(Active/Acked)이 있는지 — 배지 표시 여부</summary>
    [ObservableProperty] private bool _hasActiveAlarm;

    /// <summary>Collector 측 알람 고유 키 — ACK 요청 시 그대로 전달</summary>
    [ObservableProperty] private string _alarmKey = string.Empty;

    /// <summary>알람 레벨("HH"/"H"/"L"/"LL", 알람 없으면 "")</summary>
    [ObservableProperty] private string _alarmLevel = string.Empty;

    /// <summary>알람 상태("Active"=미확인/"Acked"=확인됨, 알람 없으면 "")</summary>
    [ObservableProperty] private string _alarmStatusText = string.Empty;

    /// <summary>알람 메시지 (팝업 표시용)</summary>
    [ObservableProperty] private string _alarmMessage = string.Empty;

    /// <summary>알람 발생 시각 텍스트 (팝업 표시용, "HH:mm:ss")</summary>
    [ObservableProperty] private string _alarmTimeText = string.Empty;

    protected AbstractLayoutNode() => Label = DisplayLabel;
}

// §3 ─ Generic 아이콘 노드 (HM-03 placeholder) ───────────

/// <summary>
/// ★ HM-03 단계의 placeholder 노드 — 캔버스 배치·드래그·줌/팬 메커니즘을
/// 검증하기 위한 범용 아이콘. HM-04 이후에도 팔레트의 기본 아이콘으로 유지한다.
/// </summary>
public sealed partial class GenericIconNode : AbstractLayoutNode
{
    public override string NodeType      => "GenericIcon";
    public override string DisplayLabel  => "아이콘";
    public override string IconGlyph     => "🔷";
    public override string CategoryColor => "#4a7fd4";
}

// §3-1 ─ HM-04: 장비 노드 4종 ─────────────────────────────

/// <summary>모터 노드 — Views/DeviceControls/MotorControl 과 1:1 대응.</summary>
public sealed partial class MotorNode : AbstractLayoutNode
{
    public override string NodeType      => "Motor";
    public override string DisplayLabel  => "모터";
    public override string IconGlyph     => "⚙";
    public override string CategoryColor => "#e08a3c";
}

/// <summary>컨베이어 노드 — Views/DeviceControls/ConveyorControl 과 1:1 대응.</summary>
public sealed partial class ConveyorNode : AbstractLayoutNode
{
    public override string NodeType      => "Conveyor";
    public override string DisplayLabel  => "컨베이어";
    public override string IconGlyph     => "➡";
    public override string CategoryColor => "#2ea8a8";
}

/// <summary>탱크 노드 — Views/DeviceControls/TankControl 과 1:1 대응.</summary>
public sealed partial class TankNode : AbstractLayoutNode
{
    public override string NodeType      => "Tank";
    public override string DisplayLabel  => "탱크";
    public override string IconGlyph     => "🛢";
    public override string CategoryColor => "#4caf6d";
}

/// <summary>밸브 노드 — Views/DeviceControls/ValveControl 과 1:1 대응.</summary>
public sealed partial class ValveNode : AbstractLayoutNode
{
    public override string NodeType      => "Valve";
    public override string DisplayLabel  => "밸브";
    public override string IconGlyph     => "🚰";
    public override string CategoryColor => "#9b59d0";
}

// §4 ─ 팩토리 ─────────────────────────────────────────────

public static class LayoutNodeFactory
{
    /// <summary>
    /// ★ 확장 지점: 신규 장비 노드 추가 시 이 switch 에 케이스 1줄만 추가하면 된다
    /// (모델 클래스는 §3-1 참조, 대응 뷰 컨트롤은 Views/DeviceControls/ 참조).
    /// </summary>
    public static AbstractLayoutNode? Create(string nodeType) => nodeType switch
    {
        "GenericIcon" => new GenericIconNode(),
        "Motor"       => new MotorNode(),
        "Conveyor"    => new ConveyorNode(),
        "Tank"        => new TankNode(),
        "Valve"       => new ValveNode(),
        _             => null
    };

    /// <summary>팔레트 항목 — HM-04: 모터/컨베이어/탱크/밸브 추가</summary>
    public static IReadOnlyList<LayoutPaletteItem> PaletteItems =>
    [
        new("GenericIcon", "아이콘",    "🔷", "#4a7fd4"),
        new("Motor",       "모터",      "⚙", "#e08a3c"),
        new("Conveyor",    "컨베이어",  "➡", "#2ea8a8"),
        new("Tank",        "탱크",      "🛢", "#4caf6d"),
        new("Valve",       "밸브",      "🚰", "#9b59d0"),
    ];
}

public sealed record LayoutPaletteItem(
    string NodeType,
    string Label,
    string Icon,
    string Color);
