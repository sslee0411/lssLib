// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/CanvasNode.cs
//  역할: NodeRed 스타일 캔버스 노드 모델
//  S-11: 초기 구현
//  S-12: 포트 위치 계산 프로퍼티 추가
//  S-12B: DeviceCanvasNode + TagInfo + DevicePaletteItem 추가
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IIoT.Studio.Core.Canvas;

// §1 ─ 노드 크기 상수 ─────────────────────────────────────

public static class NodeLayout
{
    public const double Width       = 160;
    public const double HeaderH     = 60;
    public const double PortRadius  = 7;
    public const double PortOffsetY = 30;
    public const double TagRowH     = 22;
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
    public int           Index       { get; init; }
}

// §4 ─ TagInfo ────────────────────────────────────────────

public sealed record TagInfo(
    string TagId,
    string Name,
    string Address,
    string DataType,
    string Unit = "");

// §5 ─ 추상 노드 ──────────────────────────────────────────

public abstract partial class AbstractCanvasNode : ObservableObject
{
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

    [ObservableProperty] private bool _isSelected;

    // §5-1 ─ 포트 위치 ────────────────────────────────────────

    public double InputPortX        => X;
    public double OutputPortX       => X + NodeLayout.Width;
    public double InputPortCanvasY  => Y + NodeLayout.PortOffsetY;
    public double OutputPortCanvasY => Y + NodeLayout.PortOffsetY;
    public double GetPortCanvasY(int index) =>
        Y + NodeLayout.PortOffsetY + index * 24;

    // §5-2 ─ 포트 컬렉션 ──────────────────────────────────────

    public List<NodePort> InputPorts  { get; } = new();
    public List<NodePort> OutputPorts { get; } = new();

    protected AbstractCanvasNode() => Label = DisplayLabel;

    protected void AddInput(string label = "in")
        => InputPorts.Add(new NodePort
            { Direction = PortDirection.Input, Label = label,
              OwnerNodeId = NodeId, Index = InputPorts.Count });

    protected void AddOutput(string label = "out")
        => OutputPorts.Add(new NodePort
            { Direction = PortDirection.Output, Label = label,
              OwnerNodeId = NodeId, Index = OutputPorts.Count });
}

// §6 ─ DeviceCanvasNode (★ S-12B) ────────────────────────

public sealed partial class DeviceCanvasNode : AbstractCanvasNode
{
    [ObservableProperty] private string _linkedDeviceId   = string.Empty;
    [ObservableProperty] private string _linkedDeviceType = "PLC";
    [ObservableProperty] private string _linkedDeviceName = string.Empty;

    public ObservableCollection<TagInfo> Tags { get; } = new();
    public IEnumerable<TagInfo> PreviewTags   => Tags.Take(3);
    public string ExtraTagsText               =>
        Tags.Count > 3 ? $"외 {Tags.Count - 3}개..." : string.Empty;

    public override string NodeType      => "DeviceNode";
    public override string DisplayLabel  =>
        string.IsNullOrEmpty(LinkedDeviceName) ? "장비" : LinkedDeviceName;
    public override string IconGlyph     =>
        LinkedDeviceType == "PLC" ? "🖥" : "📟";
    public override string CategoryColor =>
        LinkedDeviceType == "PLC" ? "#2a7fd4" : "#0891b2";

    public DeviceCanvasNode() { AddOutput("데이터"); }

    public void SyncTags(IEnumerable<TagInfo> tags)
    {
        Tags.Clear();
        foreach (var t in tags) Tags.Add(t);
        OnPropertyChanged(nameof(PreviewTags));
        OnPropertyChanged(nameof(ExtraTagsText));
    }
}

// §7 ─ 처리 노드 6종 ─────────────────────────────────────

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

// §8 ─ 팩토리 ─────────────────────────────────────────────

public static class CanvasNodeFactory
{
    public static AbstractCanvasNode? Create(string nodeType) => nodeType switch
    {
        "DeviceNode"   => new DeviceCanvasNode(),
        "ModbusInput"  => new ModbusInputNode(),
        "TcpInput"     => new TcpInputNode(),
        "BufferParser" => new BufferParserNode(),
        "ScaleFilter"  => new ScaleFilterNode(),
        "DbOutput"     => new DbOutputNode(),
        "MqttOutput"   => new MqttOutputNode(),
        _              => null
    };

    /// <summary>처리 노드 팔레트 (장비 섹션은 CanvasViewModel이 동적 제공)</summary>
    public static IReadOnlyList<PaletteItem> ProcessItems =>
    [
        new("BufferParser", "Buffer Parser", "🔧", "#7c3aed"),
        new("ScaleFilter",  "Scale Filter",  "🔀", "#059669"),
        new("DbOutput",     "DB Output",     "🗄", "#d97706"),
        new("MqttOutput",   "MQTT Output",   "📤", "#dc2626"),
    ];
}

// §9 ─ 팔레트 타입 ────────────────────────────────────────

public sealed record PaletteItem(
    string NodeType,
    string Label,
    string Icon,
    string Color);

/// <summary>장비 팔레트 항목 — 장비 트리에서 동적 생성 (★ S-12B)</summary>
public sealed record DevicePaletteItem(
    string DeviceId,
    string DeviceType,
    string Name,
    IReadOnlyList<TagInfo> Tags);
