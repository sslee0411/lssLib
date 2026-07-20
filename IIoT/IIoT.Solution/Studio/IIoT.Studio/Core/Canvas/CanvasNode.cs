// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/CanvasNode.cs
//  역할: NodeRed 스타일 캔버스 노드 모델
//  S-11: 초기 구현
//  S-12: 포트 위치 계산 프로퍼티 추가
//  S-12B: DeviceCanvasNode + TagInfo + DevicePaletteItem 추가
//  S-20 (N포트 노드): NodePort.Index/Label을 settable로 변경(포트 삭제 시
//               재인덱싱 + 사용자 이름변경 지원) · InputPorts/OutputPorts를
//               List→ObservableCollection으로 전환(포트 UI 자동 갱신) ·
//               AbstractCanvasNode에 AddInputPort/RemoveInputPort/
//               AddOutputPort/RemoveOutputPort(공개, 최소 1개 유지) +
//               CardHeight(포트 개수에 따른 카드 높이) 추가 · 신규 노드 2종:
//               SplitterNode(1입력 N출력 — 1:N 분기) / CompositeCalcNode
//               (N입력 1출력 + NCalc 식 — N:1 병합, 가상Tag와 동일 "[라벨]"
//               참조 문법)
//  생성: 2026-06-17 / 수정: 2026-07-20
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
    // ★ S-20: 사용자 이름변경 지원(Splitter/CompositeCalc 포트 라벨 편집) → settable
    public string        Label       { get; set;  } = string.Empty;
    public string        OwnerNodeId { get; set;  } = string.Empty;
    // ★ S-20: 포트 삭제 시 재인덱싱 필요 → settable
    public int           Index       { get; set;  }
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

    // ★ S-20: 노드 카드 높이 — 입/출력 포트 중 더 많은 쪽 개수에 맞춰 확장
    //   (포트가 카드 하단으로 삐져나가지 않도록 NodeCardTemplate.Height 바인딩용)
    public double CardHeight => Math.Max(
        NodeLayout.HeaderH,
        NodeLayout.PortOffsetY + Math.Max(InputPorts.Count, OutputPorts.Count) * 24 + 12);

    // §5-2 ─ 포트 컬렉션 ──────────────────────────────────────
    //   ★ S-20: List → ObservableCollection 전환 — Splitter/CompositeCalc
    //   포트 편집 UI(ItemsControl)가 Add/Remove 시 별도 Refresh() 없이 자동 갱신

    public ObservableCollection<NodePort> InputPorts  { get; } = new();
    public ObservableCollection<NodePort> OutputPorts { get; } = new();

    protected AbstractCanvasNode() => Label = DisplayLabel;

    protected void AddInput(string label = "in")
        => InputPorts.Add(new NodePort
            { Direction = PortDirection.Input, Label = label,
              OwnerNodeId = NodeId, Index = InputPorts.Count });

    protected void AddOutput(string label = "out")
        => OutputPorts.Add(new NodePort
            { Direction = PortDirection.Output, Label = label,
              OwnerNodeId = NodeId, Index = OutputPorts.Count });

    // §5-3 ─ ★ S-20: 공개 포트 CRUD (Splitter/CompositeCalc 등 N포트 노드용) ──
    //   최소 1개 포트는 항상 유지 — 연결이 끊긴 빈 노드가 되지 않도록 보호

    public NodePort AddInputPort(string? label = null)
    {
        var port = new NodePort
        {
            Direction   = PortDirection.Input,
            Label       = string.IsNullOrWhiteSpace(label) ? $"입력{InputPorts.Count + 1}" : label!,
            OwnerNodeId = NodeId,
            Index       = InputPorts.Count
        };
        InputPorts.Add(port);
        OnPropertyChanged(nameof(CardHeight));
        return port;
    }

    public NodePort AddOutputPort(string? label = null)
    {
        var port = new NodePort
        {
            Direction   = PortDirection.Output,
            Label       = string.IsNullOrWhiteSpace(label) ? $"출력{OutputPorts.Count + 1}" : label!,
            OwnerNodeId = NodeId,
            Index       = OutputPorts.Count
        };
        OutputPorts.Add(port);
        OnPropertyChanged(nameof(CardHeight));
        return port;
    }

    public bool RemoveInputPort(NodePort port)
    {
        if (InputPorts.Count <= 1) return false;
        if (!InputPorts.Remove(port)) return false;
        _Reindex(InputPorts);
        OnPropertyChanged(nameof(CardHeight));
        return true;
    }

    public bool RemoveOutputPort(NodePort port)
    {
        if (OutputPorts.Count <= 1) return false;
        if (!OutputPorts.Remove(port)) return false;
        _Reindex(OutputPorts);
        OnPropertyChanged(nameof(CardHeight));
        return true;
    }

    private static void _Reindex(ObservableCollection<NodePort> ports)
    {
        for (int i = 0; i < ports.Count; i++)
            ports[i].Index = i;
    }
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

// §7-1 ─ ★ S-20: N포트 노드 2종 ──────────────────────────
//   설계: 입력N/출력N 포트 라우팅 (스킬 "N포트 노드 아키텍처(S-20)" 참조)
//   [1:N 분기] SplitterNode — 입력 1개를 사용자가 추가한 N개 출력으로 그대로 전달
//   [N:1 병합] CompositeCalcNode — 입력 N개를 NCalc 식으로 계산해 출력 1개 발행
//   ※ Studio 캔버스(collect.json)는 Collector가 소비하지 않는 별도 트랙이라
//     실제 수집 동작에는 영향 없음 — 화면 정의 전용(이전 후속·보류 항목 공지와 동일)

public sealed partial class SplitterNode : AbstractCanvasNode
{
    public override string NodeType      => "Splitter";
    public override string DisplayLabel  => "분배기(N출력)";
    public override string IconGlyph     => "🔱";
    public override string CategoryColor => "#f59e0b";

    // 기본 2개 출력으로 시작 — 캔버스에서 우측 속성 패널로 자유롭게 추가/삭제
    public SplitterNode() { AddInput("입력"); AddOutput("출력1"); AddOutput("출력2"); }
}

public sealed partial class CompositeCalcNode : AbstractCanvasNode
{
    public override string NodeType      => "CompositeCalc";
    public override string DisplayLabel  => "복합계산(N입력)";
    public override string IconGlyph     => "🧮";
    public override string CategoryColor => "#0d9488";

    /// <summary>NCalc 식 — 입력 포트 라벨을 [라벨] 형태로 참조 (가상Tag와 동일 문법).
    /// 예: "[압력1] - [압력2]"</summary>
    [ObservableProperty] private string _expression = string.Empty;

    // 기본 2개 입력으로 시작
    public CompositeCalcNode() { AddInput("입력1"); AddInput("입력2"); AddOutput("결과"); }
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
        "Splitter"     => new SplitterNode(),      // ★ S-20
        "CompositeCalc"=> new CompositeCalcNode(), // ★ S-20
        _              => null
    };

    /// <summary>처리 노드 팔레트 (장비 섹션은 CanvasViewModel이 동적 제공)</summary>
    public static IReadOnlyList<PaletteItem> ProcessItems =>
    [
        new("BufferParser",  "Buffer Parser",    "🔧", "#7c3aed"),
        new("ScaleFilter",   "Scale Filter",     "🔀", "#059669"),
        new("DbOutput",      "DB Output",        "🗄", "#d97706"),
        new("MqttOutput",    "MQTT Output",      "📤", "#dc2626"),
        new("Splitter",      "분배기(N출력)",     "🔱", "#f59e0b"),   // ★ S-20
        new("CompositeCalc", "복합계산(N입력)",   "🧮", "#0d9488"),   // ★ S-20
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
