// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/CanvasNode.cs
//  역할: NodeRed 스타일 캔버스 노드 모델
//  S-11: 초기 구현
//  S-12: 포트 위치 계산 프로퍼티 추가
//        NodeWidth / NodeHeight 상수 추가
//        GetInputPortY / GetOutputPortY 헬퍼 추가
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Core.Canvas;

// §1 ─ 노드 크기 상수 ─────────────────────────────────────

public static class NodeLayout
{
    public const double Width      = 160;
    public const double HeaderH    = 60;   // 노드 카드 최소 높이
    public const double PortRadius = 7;    // 포트 원 반지름
    public const double PortOffsetY = 30;  // 헤더 중앙 (포트 Y 기본)
}

// §2 ─ 포트 방향 ──────────────────────────────────────────

public enum PortDirection { Input, Output }

// §3 ─ 포트 ──────────────────────────────────────────────

public sealed class NodePort
{
    public string        PortId      { get; init; } = Guid.NewGuid().ToString();
    public PortDirection Direction   { get; init; }
    public string        Label       { get; init; } = string.Empty;
    public string        OwnerNodeId { get; set;  } = string.Empty;
    /// <summary>포트 인덱스 (같은 방향 내 순서, 0-based)</summary>
    public int           Index       { get; init; }
}

// §4 ─ 추상 노드 ──────────────────────────────────────────

public abstract partial class AbstractCanvasNode : ObservableObject
{
    // §4-1 ─ 공통 프로퍼티 ───────────────────────────────────

    public string NodeId { get; } = Guid.NewGuid().ToString();
    public abstract string NodeType      { get; }
    public abstract string DisplayLabel  { get; }
    public abstract string IconGlyph     { get; }
    public abstract string CategoryColor { get; }

    [ObservableProperty] private string _label = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputPortX))]
    [NotifyPropertyChangedFor(nameof(InputPortX))]
    private double _x = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputPortCanvasY))]
    [NotifyPropertyChangedFor(nameof(OutputPortCanvasY))]
    private double _y = 100;

    [ObservableProperty]
    private bool _isSelected;

    // §4-2 ─ 포트 위치 계산 프로퍼티 (캔버스 절대 좌표) ───────

    /// <summary>입력 포트 X (노드 좌측 중앙)</summary>
    public double InputPortX  => X;

    /// <summary>출력 포트 X (노드 우측 중앙)</summary>
    public double OutputPortX => X + NodeLayout.Width;

    /// <summary>입력 포트 캔버스 Y (첫 번째 포트 기준)</summary>
    public double InputPortCanvasY  => Y + NodeLayout.PortOffsetY;

    /// <summary>출력 포트 캔버스 Y (첫 번째 포트 기준)</summary>
    public double OutputPortCanvasY => Y + NodeLayout.PortOffsetY;

    /// <summary>포트 인덱스별 Y 계산 (포트가 여러 개일 때)</summary>
    public double GetPortCanvasY(int index) =>
        Y + NodeLayout.PortOffsetY + index * 24;

    // §4-3 ─ 포트 ────────────────────────────────────────────

    public List<NodePort> InputPorts  { get; } = new();
    public List<NodePort> OutputPorts { get; } = new();

    // §4-4 ─ 생성자 ──────────────────────────────────────────

    protected AbstractCanvasNode() => Label = DisplayLabel;

    // §4-5 ─ 포트 초기화 헬퍼 ───────────────────────────────

    protected void AddInput(string label = "in")
    {
        var port = new NodePort
        {
            Direction   = PortDirection.Input,
            Label       = label,
            OwnerNodeId = NodeId,
            Index       = InputPorts.Count
        };
        InputPorts.Add(port);
    }

    protected void AddOutput(string label = "out")
    {
        var port = new NodePort
        {
            Direction   = PortDirection.Output,
            Label       = label,
            OwnerNodeId = NodeId,
            Index       = OutputPorts.Count
        };
        OutputPorts.Add(port);
    }
}

// §5 ─ 구체 노드 6종 ─────────────────────────────────────

public sealed partial class ModbusInputNode : AbstractCanvasNode
{
    public override string NodeType      => "ModbusInput";
    public override string DisplayLabel  => "Modbus Input";
    public override string IconGlyph     => "🔌";
    public override string CategoryColor => "#2a7fd4";

    [ObservableProperty] private string _host     = "192.168.0.1";
    [ObservableProperty] private int    _port     = 502;
    [ObservableProperty] private int    _slaveId  = 1;
    [ObservableProperty] private string _register = "40001";
    [ObservableProperty] private int    _pollMs   = 1000;

    public ModbusInputNode() { AddOutput("데이터"); }
}

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

public sealed partial class BufferParserNode : AbstractCanvasNode
{
    public override string NodeType      => "BufferParser";
    public override string DisplayLabel  => "Buffer Parser";
    public override string IconGlyph     => "🔧";
    public override string CategoryColor => "#7c3aed";

    [ObservableProperty] private string _schema = string.Empty;

    public BufferParserNode() { AddInput("프레임"); AddOutput("값"); }
}

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

public sealed partial class DbOutputNode : AbstractCanvasNode
{
    public override string NodeType      => "DbOutput";
    public override string DisplayLabel  => "DB Output";
    public override string IconGlyph     => "🗄";
    public override string CategoryColor => "#d97706";

    [ObservableProperty] private string _tableName = "TagHistory";

    public DbOutputNode() { AddInput("값"); }
}

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

// §6 ─ 노드 팩토리 ────────────────────────────────────────

public static class CanvasNodeFactory
{
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

// §7 ─ 팔레트 항목 ────────────────────────────────────────

public sealed record PaletteItem(
    string NodeType,
    string Label,
    string Icon,
    string Color);
