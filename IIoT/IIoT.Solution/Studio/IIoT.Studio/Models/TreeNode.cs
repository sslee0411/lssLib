// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/TreeNode.cs
//  역할: 장비 트리 노드 추상 기반 + 4종 구체 노드
//        Group / Device / Plc / Tag
//  Enum 은 Models/Enums.cs 로 분리됨 (NodeCommType 등)
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IIoT.Studio.Models;

// §1 ─ 추상 기반 노드 ─────────────────────────────────────

/// <summary>
/// 트리 노드 공통 추상 클래스.
/// 모든 노드 타입은 이 클래스를 상속한다.
/// </summary>
public abstract partial class AbstractTreeNode : ObservableObject
{
    // §1-1 ─ 공통 프로퍼티 ───────────────────────────────────

    /// <summary>노드 표시 이름</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>노드 설명</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>트리 아이콘 글리프 (이모지)</summary>
    public abstract string IconGlyph { get; }

    /// <summary>노드 타입 레이블 (우측 패널 표시용)</summary>
    public abstract string NodeTypeLabel { get; }

    // §1-2 ─ 자식 노드 ───────────────────────────────────────

    /// <summary>자식 노드 컬렉션 (TreeView ItemsSource)</summary>
    public ObservableCollection<AbstractTreeNode> Children { get; } = new();

    // §1-3 ─ 헬퍼 ───────────────────────────────────────────

    /// <summary>고유 ID (내부 관리용)</summary>
    public Guid Id { get; } = Guid.NewGuid();
}

// §2 ─ 그룹 노드 ──────────────────────────────────────────

/// <summary>
/// 그룹 노드 — 공장·라인·사이트 등 논리적 묶음.
/// 하위에 Device 또는 Group 을 포함할 수 있다.
/// </summary>
public partial class GroupTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "📁";
    public override string NodeTypeLabel => "그룹";

    public GroupTreeNode(string name = "새 그룹")
    {
        Name = name;
    }
}

// §3 ─ 장비 노드 ──────────────────────────────────────────

/// <summary>
/// 장비 노드 — 압출기·사출기 등 실제 설비.
/// 하위에 PLC 또는 Tag 를 직접 포함할 수 있다.
/// ★ 장비 자체도 통신 가능 (Serial 바코드리더, MQTT 센서 등)
///    통신 설정이 필요 없는 경우 CommType = "없음" 유지
/// </summary>
public partial class DeviceTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "🏭";
    public override string NodeTypeLabel => "장비";

    // ── 기본 정보 ────────────────────────────────────────────

    /// <summary>장비 모델명</summary>
    [ObservableProperty]
    private string _model = string.Empty;

    /// <summary>제조사</summary>
    [ObservableProperty]
    private string _manufacturer = string.Empty;

    /// <summary>설치 위치</summary>
    [ObservableProperty]
    private string _location = string.Empty;

    // ── 통신 설정 ────────────────────────────────────────────

    /// <summary>
    /// 통신 방식.
    /// ★ NodeCommType.None = 통신 없음 (기본값)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommEnabled))]
    [NotifyPropertyChangedFor(nameof(IsModbusTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsMqtt))]
    [NotifyPropertyChangedFor(nameof(IsOpcUa))]
    private NodeCommType _commType = NodeCommType.None;

    /// <summary>IP 주소 / COM 포트 / 브로커 주소 / 엔드포인트 URL</summary>
    [ObservableProperty]
    private string _host = string.Empty;

    /// <summary>포트 번호 / 보드레이트</summary>
    [ObservableProperty]
    private int _port = 502;

    /// <summary>폴링 주기 (ms)</summary>
    [ObservableProperty]
    private int _pollMs = 1000;

    // 통신 방식 판별 프로퍼티
    public bool IsCommEnabled => CommType != NodeCommType.None;
    public bool IsModbusTcp   => CommType == NodeCommType.ModbusTcp;
    public bool IsSerial      => CommType == NodeCommType.Serial;
    public bool IsMqtt        => CommType == NodeCommType.Mqtt;
    public bool IsOpcUa       => CommType == NodeCommType.OpcUa;

    public DeviceTreeNode(string name = "새 장비")
    {
        Name = name;
    }
}

// §4 ─ PLC 노드 ───────────────────────────────────────────

/// <summary>
/// PLC 노드 — Modbus TCP / Serial / OPC-UA / MQTT 등 통신 설정.
/// 하위에 Tag 를 포함한다.
/// </summary>
public partial class PlcTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "🔧";
    public override string NodeTypeLabel => "PLC";

    /// <summary>
    /// 통신 방식.
    /// ★ PLC 는 반드시 통신 필요 → None 없음, 기본값 ModbusTcp
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModbusTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsMqtt))]
    [NotifyPropertyChangedFor(nameof(IsOpcUa))]
    private NodeCommType _commType = NodeCommType.ModbusTcp;

    /// <summary>IP 주소 / COM 포트 / 브로커 주소 / 엔드포인트 URL</summary>
    [ObservableProperty]
    private string _host = "192.168.0.1";

    /// <summary>포트 번호 / 보드레이트</summary>
    [ObservableProperty]
    private int _port = 502;

    /// <summary>폴링 주기 (ms)</summary>
    [ObservableProperty]
    private int _pollMs = 1000;

    // 통신 방식 판별 프로퍼티
    public bool IsModbusTcp => CommType == NodeCommType.ModbusTcp;
    public bool IsSerial    => CommType == NodeCommType.Serial;
    public bool IsMqtt      => CommType == NodeCommType.Mqtt;
    public bool IsOpcUa     => CommType == NodeCommType.OpcUa;

    public PlcTreeNode(string name = "새 PLC")
    {
        Name = name;
    }
}

// §5 ─ Tag 노드 ───────────────────────────────────────────

/// <summary>
/// Tag 노드 — PLC 레지스터 단일 수집 포인트.
/// 스케일·알람 라이브러리를 Id 로 느슨하게 참조 (Loose Coupling).
/// </summary>
public partial class TagTreeNode : AbstractTreeNode
{
    public override string IconGlyph    => "🏷";
    public override string NodeTypeLabel => "Tag";

    // ── 수집 설정 ────────────────────────────────────────────

    /// <summary>레지스터 주소 (예: 40001)</summary>
    [ObservableProperty]
    private string _address = "40001";

    /// <summary>데이터 타입 (Float / Int16 / Bool 등)</summary>
    [ObservableProperty]
    private string _dataType = "Float";

    /// <summary>단위 (예: bar / °C)</summary>
    [ObservableProperty]
    private string _unit = string.Empty;

    // ── 라이브러리 참조 (Id 기반 느슨한 연결) ────────────────

    /// <summary>
    /// 연결된 스케일 항목 Id.
    /// null = 스케일 미적용.
    /// S-10 저장 시 JSON 에 기록, 로드 시 ScaleEntry 역참조.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScale))]
    private Guid? _scaleEntryId;

    /// <summary>
    /// 연결된 알람 항목 Id.
    /// null = 알람 미적용.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlarm))]
    private Guid? _alarmEntryId;

    /// <summary>스케일 연결 여부 (편집기 표시용)</summary>
    public bool HasScale => ScaleEntryId.HasValue;

    /// <summary>알람 연결 여부 (편집기 표시용)</summary>
    public bool HasAlarm => AlarmEntryId.HasValue;

    public TagTreeNode(string name = "새 Tag")
    {
        Name = name;
    }
}
