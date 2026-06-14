// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/AbstractNode.cs
//  역할: NodeRed 스타일 캔버스 노드 추상 기반 클래스
//        상속 → 팔레트 노드(Input·Parser·Filter·Output) 구현
//  Phase 11: 신규
//
//  확장 방법:
//    public class ModbusInputNode : AbstractNode {
//        public override string NodeType    => "Input";
//        public override string Category   => "Input";
//        public override string IconGlyph  => "📡";
//        public override string DisplayColor => "#4F7CFF";
//    }
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Canvas;

// §1 ─ 포트 방향 ──────────────────────────────────────────
public enum PortDirection { Input, Output }

// §2 ─ 포트 모델 ──────────────────────────────────────────
/// <summary>
/// 노드 연결 포트 (입력 / 출력).
/// 연결선(NodeConnection)의 Source/Target 으로 참조됩니다.
/// </summary>
public sealed class NodePort
{
    public string        PortId    { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string        Label     { get; init; } = string.Empty;
    public PortDirection Direction { get; init; }
    public string        DataType  { get; init; } = "any"; // double, bool, string, any
    public string        OwnerNodeId { get; set; } = string.Empty;

    /// <summary>캔버스 상 포트 위치 (노드 상대 좌표)</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
}

// §3 ─ 연결선 모델 ────────────────────────────────────────
/// <summary>두 포트를 연결하는 선</summary>
public sealed class NodeConnection
{
    public string ConnectionId  { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string SourceNodeId  { get; set;  } = string.Empty;
    public string SourcePortId  { get; set;  } = string.Empty;
    public string TargetNodeId  { get; set;  } = string.Empty;
    public string TargetPortId  { get; set;  } = string.Empty;
}

// §4 ─ 추상 노드 기반 ─────────────────────────────────────
/// <summary>
/// NodeRed 스타일 캔버스 노드 추상 기반.
///
/// 하위 클래스가 구현해야 할 항목:
///   NodeType    — 노드 종류 ("ModbusInput", "ScaleMap" 등)
///   Category    — 팔레트 카테고리 ("Input", "Parser", "Filter", "Output")
///   IconGlyph   — 아이콘 (Unicode)
///   DisplayColor — 노드 헤더 색상 (#RRGGBB)
///
/// 선택 오버라이드:
///   InputPorts  — 입력 포트 목록 (기본: 1개 "in")
///   OutputPorts — 출력 포트 목록 (기본: 1개 "out")
///   Properties  — 노드 설정 프로퍼티 딕셔너리
/// </summary>
public abstract partial class AbstractNode : ObservableObject
{
    // §4-1 ─ 추상 프로퍼티 ────────────────────────────────
    public abstract string NodeType     { get; }
    public abstract string Category     { get; }
    public abstract string IconGlyph    { get; }
    public abstract string DisplayColor { get; }

    // §4-2 ─ 공통 식별 프로퍼티 ──────────────────────────
    public string NodeId { get; init; } = Guid.NewGuid().ToString("N")[..12];

    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private bool   _isSelected;
    [ObservableProperty] private bool   _isRunning;  // 실시간 수집 중 표시
    [ObservableProperty] private string _statusText  = string.Empty;

    // §4-3 ─ 캔버스 위치 ──────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CenterX))]
    private double _x = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CenterY))]
    private double _y = 100;

    [ObservableProperty] private double _width  = 160;
    [ObservableProperty] private double _height = 60;

    public double CenterX => X + Width  / 2;
    public double CenterY => Y + Height / 2;

    // §4-4 ─ 포트 목록 ────────────────────────────────────
    /// <summary>입력 포트 목록 (오버라이드 가능)</summary>
    public virtual IReadOnlyList<NodePort> InputPorts { get; } =
        [new NodePort { Label = "in", Direction = PortDirection.Input }];

    /// <summary>출력 포트 목록 (오버라이드 가능)</summary>
    public virtual IReadOnlyList<NodePort> OutputPorts { get; } =
        [new NodePort { Label = "out", Direction = PortDirection.Output }];

    // §4-5 ─ 설정 프로퍼티 ────────────────────────────────
    /// <summary>노드 설정 키-값 (직렬화 대상)</summary>
    public Dictionary<string, string> Properties { get; init; } = [];

    public string? GetProp(string key)
        => Properties.TryGetValue(key, out var v) ? v : null;

    public void SetProp(string key, string value)
        => Properties[key] = value;

    // §4-6 ─ 직렬화 지원 ──────────────────────────────────
    /// <summary>collect.json 저장용 직렬화</summary>
    public virtual Dictionary<string, object> ToSerializable() => new()
    {
        ["nodeId"]    = NodeId,
        ["nodeType"]  = NodeType,
        ["label"]     = Label,
        ["x"]         = X,
        ["y"]         = Y,
        ["properties"] = Properties,
    };
}

// ══════════════════════════════════════════════════════════
//  §5 ─ Input 카테고리 노드
// ══════════════════════════════════════════════════════════

/// <summary>Modbus TCP/RTU 입력 노드 — device.json Device 참조</summary>
public sealed partial class ModbusInputNode : AbstractNode
{
    public override string NodeType     => "ModbusInput";
    public override string Category     => "Input";
    public override string IconGlyph    => "📡";
    public override string DisplayColor => "#4F7CFF";

    [ObservableProperty] private string _deviceId  = string.Empty;
    [ObservableProperty] private string _address   = "40001";
    [ObservableProperty] private string _dataType  = "FloatBE";
    [ObservableProperty] private int    _pollMs    = 1000;

    public ModbusInputNode() => Label = "Modbus Input";

    public override IReadOnlyList<NodePort> InputPorts  { get; } = [];
    public override IReadOnlyList<NodePort> OutputPorts { get; } =
        [new NodePort { Label = "raw", Direction = PortDirection.Output, DataType = "double" }];
}

/// <summary>OPC-UA 입력 노드</summary>
public sealed partial class OpcUaInputNode : AbstractNode
{
    public override string NodeType     => "OpcUaInput";
    public override string Category     => "Input";
    public override string IconGlyph    => "🌐";
    public override string DisplayColor => "#0284C7";

    [ObservableProperty] private string _endpointUrl = "opc.tcp://localhost:4840";
    [ObservableProperty] private string _nodeId      = "ns=2;i=1";
    [ObservableProperty] private int    _pollMs      = 1000;

    public OpcUaInputNode() => Label = "OPC-UA Input";

    public override IReadOnlyList<NodePort> InputPorts  { get; } = [];
    public override IReadOnlyList<NodePort> OutputPorts { get; } =
        [new NodePort { Label = "raw", Direction = PortDirection.Output, DataType = "double" }];
}

/// <summary>가상(시뮬레이터) 입력 노드</summary>
public sealed partial class VirtualInputNode : AbstractNode
{
    public override string NodeType     => "VirtualInput";
    public override string Category     => "Input";
    public override string IconGlyph    => "🔬";
    public override string DisplayColor => "#22D3A0";

    [ObservableProperty] private string _simType  = "SIN";  // SIN / RAMP / RAND / CONST
    [ObservableProperty] private double _amplitude = 10.0;
    [ObservableProperty] private double _period    = 60.0;
    [ObservableProperty] private int    _pollMs    = 1000;

    public VirtualInputNode() => Label = "Virtual Input";

    public override IReadOnlyList<NodePort> InputPorts  { get; } = [];
    public override IReadOnlyList<NodePort> OutputPorts { get; } =
        [new NodePort { Label = "raw", Direction = PortDirection.Output, DataType = "double" }];
}

// ══════════════════════════════════════════════════════════
//  §6 ─ Parser 카테고리 노드
// ══════════════════════════════════════════════════════════

/// <summary>BufSchema 기반 바이너리 파서 노드</summary>
public sealed partial class BufParserNode : AbstractNode
{
    public override string NodeType     => "BufParser";
    public override string Category     => "Parser";
    public override string IconGlyph    => "📦";
    public override string DisplayColor => "#7C5CFF";

    [ObservableProperty] private string _schema     = string.Empty;  // BufSchema JSON
    [ObservableProperty] private string _fieldName  = "value";

    public BufParserNode() => Label = "Buf Parser";
}

/// <summary>스케일 변환 노드 — scale-library.json 참조</summary>
public sealed partial class ScaleMapNode : AbstractNode
{
    public override string NodeType     => "ScaleMap";
    public override string Category     => "Parser";
    public override string IconGlyph    => "📐";
    public override string DisplayColor => "#F59E0B";

    [ObservableProperty] private string _scaleConfigId = string.Empty;
    [ObservableProperty] private double _rawMin  = 0;
    [ObservableProperty] private double _rawMax  = 4095;
    [ObservableProperty] private double _engMin  = 0;
    [ObservableProperty] private double _engMax  = 100;
    [ObservableProperty] private string _unit    = string.Empty;

    public ScaleMapNode() => Label = "Scale Map";
}

// ══════════════════════════════════════════════════════════
//  §7 ─ Filter 카테고리 노드
// ══════════════════════════════════════════════════════════

/// <summary>임계값 필터 노드 — 범위 밖 데이터 차단</summary>
public sealed partial class ThresholdFilterNode : AbstractNode
{
    public override string NodeType     => "ThresholdFilter";
    public override string Category     => "Filter";
    public override string IconGlyph    => "🚦";
    public override string DisplayColor => "#FF8C42";

    [ObservableProperty] private double? _minValue;
    [ObservableProperty] private double? _maxValue;
    [ObservableProperty] private bool    _passOnFail = false;  // 실패 시 통과 여부

    public ThresholdFilterNode() => Label = "Threshold";

    public override IReadOnlyList<NodePort> OutputPorts { get; } =
    [
        new NodePort { Label = "pass", Direction = PortDirection.Output, DataType = "double" },
        new NodePort { Label = "fail", Direction = PortDirection.Output, DataType = "double" },
    ];
}

/// <summary>SDT 압축 필터 노드 — 의미 있는 변화점만 통과</summary>
public sealed partial class SdtFilterNode : AbstractNode
{
    public override string NodeType     => "SDTFilter";
    public override string Category     => "Filter";
    public override string IconGlyph    => "🗜️";
    public override string DisplayColor => "#059669";

    [ObservableProperty] private double _devBand    = 0.5;
    [ObservableProperty] private int    _maxTimeSec = 300;

    public SdtFilterNode() => Label = "SDT Filter";
}

// ══════════════════════════════════════════════════════════
//  §8 ─ Output 카테고리 노드
// ══════════════════════════════════════════════════════════

/// <summary>SQLite DB 저장 노드</summary>
public sealed partial class DbOutputNode : AbstractNode
{
    public override string NodeType     => "DbOutput";
    public override string Category     => "Output";
    public override string IconGlyph    => "💾";
    public override string DisplayColor => "#DC2626";

    [ObservableProperty] private string _tableName = "tag_history";
    [ObservableProperty] private string _tagId     = string.Empty;

    public DbOutputNode() => Label = "DB Output";

    public override IReadOnlyList<NodePort> OutputPorts { get; } = [];
}

/// <summary>MQTT 발행 노드</summary>
public sealed partial class MqttOutputNode : AbstractNode
{
    public override string NodeType     => "MqttOutput";
    public override string Category     => "Output";
    public override string IconGlyph    => "📤";
    public override string DisplayColor => "#7C3AED";

    [ObservableProperty] private string _broker  = "localhost";
    [ObservableProperty] private int    _port    = 1883;
    [ObservableProperty] private string _topic   = "iiot/data";
    [ObservableProperty] private string _qos     = "0";

    public MqttOutputNode() => Label = "MQTT Output";

    public override IReadOnlyList<NodePort> OutputPorts { get; } = [];
}

/// <summary>디버그 출력 노드 (개발용)</summary>
public sealed partial class DebugOutputNode : AbstractNode
{
    public override string NodeType     => "DebugOutput";
    public override string Category     => "Output";
    public override string IconGlyph    => "🐛";
    public override string DisplayColor => "#4A5568";

    public DebugOutputNode() => Label = "Debug";

    public override IReadOnlyList<NodePort> OutputPorts { get; } = [];
}
