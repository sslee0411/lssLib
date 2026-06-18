// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/CanvasNode.cs
//  역할: NodeRed 스타일 캔버스 노드 모델
//        AbstractCanvasNode + 6종 구체 노드
//  S-11: 초기 구현
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Core.Canvas;

// §1 ─ 노드 방향 열거형 ───────────────────────────────────

public enum PortDirection { Input, Output }

// §2 ─ 포트 ──────────────────────────────────────────────

public sealed class NodePort
{
    public string        PortId    { get; init; } = Guid.NewGuid().ToString();
    public PortDirection Direction { get; init; }
    public string        Label     { get; init; } = string.Empty;
    public string        OwnerNodeId { get; set; } = string.Empty;
}

// §3 ─ 추상 노드 ──────────────────────────────────────────

/// <summary>캔버스 위 모든 노드의 추상 기반 클래스</summary>
public abstract partial class AbstractCanvasNode : ObservableObject
{
    // §3-1 ─ 공통 프로퍼티 ───────────────────────────────────

    public string NodeId   { get; } = Guid.NewGuid().ToString();
    public abstract string NodeType     { get; }   // "ModbusInput" 등
    public abstract string DisplayLabel { get; }   // 팔레트 표시 이름
    public abstract string IconGlyph    { get; }   // 이모지
    public abstract string CategoryColor { get; }  // 카드 좌측 색상 (hex)

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private double _x = 100;

    [ObservableProperty]
    private double _y = 100;

    [ObservableProperty]
    private bool _isSelected;

    // §3-2 ─ 포트 ────────────────────────────────────────────

    public List<NodePort> InputPorts  { get; } = new();
    public List<NodePort> OutputPorts { get; } = new();

    // §3-3 ─ 생성자 ──────────────────────────────────────────

    protected AbstractCanvasNode()
    {
        Label = DisplayLabel;
    }

    // §3-4 ─ 포트 초기화 헬퍼 ───────────────────────────────

    protected void AddInput(string label = "in")
        => InputPorts.Add(new NodePort
            { Direction = PortDirection.Input, Label = label, OwnerNodeId = NodeId });

    protected void AddOutput(string label = "out")
        => OutputPorts.Add(new NodePort
            { Direction = PortDirection.Output, Label = label, OwnerNodeId = NodeId });
}

// §4 ─ 구체 노드 6종 ─────────────────────────────────────

/// <summary>Modbus TCP 레지스터 읽기 노드</summary>
public sealed partial class ModbusInputNode : AbstractCanvasNode
{
    public override string NodeType      => "ModbusInput";
    public override string DisplayLabel  => "Modbus Input";
    public override string IconGlyph     => "🔌";
    public override string CategoryColor => "#2a7fd4";

    [ObservableProperty] private string _host    = "192.168.0.1";
    [ObservableProperty] private int    _port    = 502;
    [ObservableProperty] private int    _slaveId = 1;
    [ObservableProperty] private string _register = "40001";
    [ObservableProperty] private int    _pollMs   = 1000;

    public ModbusInputNode() { AddOutput("데이터"); }
}

/// <summary>원시 TCP 수신 노드</summary>
public sealed partial class TcpInputNode : AbstractCanvasNode
{
    public override string NodeType      => "TcpInput";
    public override string DisplayLabel  => "TCP Input";
    public override string IconGlyph     => "📡";
    public override string CategoryColor => "#0891b2";

    [ObservableProperty] private string _host = "0.0.0.0";
    [ObservableProperty] private int    _port = 5000;

    public TcpInputNode() { AddOutput("프레임"); }
}

/// <summary>바이너리 파싱 노드 (BufSchema)</summary>
public sealed partial class BufferParserNode : AbstractCanvasNode
{
    public override string NodeType      => "BufferParser";
    public override string DisplayLabel  => "Buffer Parser";
    public override string IconGlyph     => "🔧";
    public override string CategoryColor => "#7c3aed";

    [ObservableProperty] private string _schema = string.Empty;

    public BufferParserNode() { AddInput("프레임"); AddOutput("값"); }
}

/// <summary>스케일 변환 노드 (Raw → 공학단위)</summary>
public sealed partial class ScaleFilterNode : AbstractCanvasNode
{
    public override string NodeType      => "ScaleFilter";
    public override string DisplayLabel  => "Scale Filter";
    public override string IconGlyph     => "🔀";
    public override string CategoryColor => "#059669";

    [ObservableProperty] private double _rawMin = 0;
    [ObservableProperty] private double _rawMax = 4095;
    [ObservableProperty] private double _engMin = 0;
    [ObservableProperty] private double _engMax = 100;
    [ObservableProperty] private string _unit   = string.Empty;

    public ScaleFilterNode() { AddInput("raw"); AddOutput("공학값"); }
}

/// <summary>SQLite DB 저장 노드</summary>
public sealed partial class DbOutputNode : AbstractCanvasNode
{
    public override string NodeType      => "DbOutput";
    public override string DisplayLabel  => "DB Output";
    public override string IconGlyph     => "🗄";
    public override string CategoryColor => "#d97706";

    [ObservableProperty] private string _tableName = "TagHistory";

    public DbOutputNode() { AddInput("값"); }
}

/// <summary>MQTT 브로커 발행 노드</summary>
public sealed partial class MqttOutputNode : AbstractCanvasNode
{
    public override string NodeType      => "MqttOutput";
    public override string DisplayLabel  => "MQTT Output";
    public override string IconGlyph     => "📤";
    public override string CategoryColor => "#dc2626";

    [ObservableProperty] private string _broker = "localhost";
    [ObservableProperty] private int    _port   = 1883;
    [ObservableProperty] private string _topic  = "iiot/data";

    public MqttOutputNode() { AddInput("값"); }
}

// §5 ─ 노드 팩토리 ────────────────────────────────────────

public static class CanvasNodeFactory
{
    /// <summary>nodeType 문자열로 노드 인스턴스 생성</summary>
    public static AbstractCanvasNode? Create(string nodeType) => nodeType switch
    {
        "ModbusInput"  => new ModbusInputNode(),
        "TcpInput"     => new TcpInputNode(),
        "BufferParser" => new BufferParserNode(),
        "ScaleFilter"  => new ScaleFilterNode(),
        "DbOutput"     => new DbOutputNode(),
        "MqttOutput"   => new MqttOutputNode(),
        _              => null
    };

    /// <summary>팔레트용 전체 노드 타입 목록 (순서 고정)</summary>
    public static IReadOnlyList<PaletteItem> AllItems =>
    [
        new("ModbusInput",  "Modbus Input",  "🔌", "#2a7fd4"),
        new("TcpInput",     "TCP Input",     "📡", "#0891b2"),
        new("BufferParser", "Buffer Parser", "🔧", "#7c3aed"),
        new("ScaleFilter",  "Scale Filter",  "🔀", "#059669"),
        new("DbOutput",     "DB Output",     "🗄", "#d97706"),
        new("MqttOutput",   "MQTT Output",   "📤", "#dc2626"),
    ];
}

// §6 ─ 팔레트 항목 ────────────────────────────────────────

public sealed record PaletteItem(
    string NodeType,
    string Label,
    string Icon,
    string Color);
