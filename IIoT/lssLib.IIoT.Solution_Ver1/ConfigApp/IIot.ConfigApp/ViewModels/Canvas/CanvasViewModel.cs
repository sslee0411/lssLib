// ══════════════════════════════════════════════════════════
//  IIoT.ConfigApp · ViewModels/Canvas/CanvasViewModel.cs
//  역할: NodeRed 스타일 캔버스 편집 ViewModel
//        노드 CRUD·연결선 관리·팔레트·collect.json 저장
//  Phase 11: 신규
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.ConfigApp.Core.Canvas;
using lssLib.Log;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace IIoT.ConfigApp.ViewModels.Canvas;

public partial class CanvasViewModel : ObservableObject
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "CanvasViewModel";

    // §2 ─ 캔버스 컬렉션 ──────────────────────────────────────
    public ObservableCollection<AbstractNode>     Nodes       { get; } = [];
    public ObservableCollection<NodeConnection>   Connections { get; } = [];

    // §3 ─ 팔레트 ─────────────────────────────────────────────
    public IReadOnlyList<PaletteItem> Palette { get; } = _BuildPalette();

    // §4 ─ 선택 상태 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedNode))]
    [NotifyPropertyChangedFor(nameof(HasSelectedConnection))]
    private AbstractNode? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedConnection))]
    private NodeConnection? _selectedConnection;

    public bool HasSelectedNode       => SelectedNode       is not null;
    public bool HasSelectedConnection => SelectedConnection is not null;

    // §5 ─ 연결 드래그 상태 ───────────────────────────────────
    [ObservableProperty] private bool      _isDraggingConnection;
    [ObservableProperty] private NodePort? _dragSourcePort;
    [ObservableProperty] private double    _dragEndX;
    [ObservableProperty] private double    _dragEndY;

    // §6 ─ 캔버스 뷰 변환 ─────────────────────────────────────
    [ObservableProperty] private double _scale      = 1.0;
    [ObservableProperty] private double _offsetX    = 0;
    [ObservableProperty] private double _offsetY    = 0;

    // §7 ─ 저장 상태 ──────────────────────────────────────────
    [ObservableProperty] private bool   _hasUnsavedChanges;
    [ObservableProperty] private string _statusText = "준비";

    // §8 ─ 노드 추가 커맨드 ───────────────────────────────────

    [RelayCommand]
    private void AddNode(string nodeType)
    {
        var node = _CreateNode(nodeType);
        if (node is null) return;

        // 캔버스 중앙에 배치 (기존 노드 없으면 (100,100), 있으면 오프셋)
        node.X = 100 + (Nodes.Count % 5) * 180.0;
        node.Y = 100 + (Nodes.Count / 5) * 90.0;

        // 포트 Owner 설정
        foreach (var p in node.InputPorts.Concat(node.OutputPorts))
            p.OwnerNodeId = node.NodeId;

        Nodes.Add(node);
        SelectedNode       = node;
        HasUnsavedChanges  = true;
        StatusText         = $"'{node.Label}' 추가됨";

        LogManager.Instance.Info(LogSrc, $"노드 추가: {node.NodeType} ({node.NodeId})");
    }

    [RelayCommand]
    private void DeleteSelectedNode()
    {
        if (SelectedNode is null) return;

        // 연결선도 함께 제거
        var toRemove = Connections
            .Where(c => c.SourceNodeId == SelectedNode.NodeId
                     || c.TargetNodeId == SelectedNode.NodeId)
            .ToList();
        foreach (var c in toRemove) Connections.Remove(c);

        Nodes.Remove(SelectedNode);
        SelectedNode      = null;
        HasUnsavedChanges = true;
        StatusText        = "노드 삭제됨";
    }

    [RelayCommand]
    private void DeleteSelectedConnection()
    {
        if (SelectedConnection is null) return;
        Connections.Remove(SelectedConnection);
        SelectedConnection = null;
        HasUnsavedChanges  = true;
        StatusText         = "연결 삭제됨";
    }

    // §9 ─ 연결선 추가 ────────────────────────────────────────

    /// <summary>두 포트를 연결합니다 (코드비하인드에서 드래그 완료 시 호출)</summary>
    public void ConnectPorts(NodePort source, NodePort target)
    {
        // 같은 방향 연결 불가
        if (source.Direction == target.Direction) return;

        // 이미 연결된 포트 중복 방지
        bool exists = Connections.Any(c =>
            (c.SourcePortId == source.PortId && c.TargetPortId == target.PortId) ||
            (c.SourcePortId == target.PortId && c.TargetPortId == source.PortId));
        if (exists) return;

        // 출력 → 입력 방향 정렬
        var (src, tgt) = source.Direction == PortDirection.Output
            ? (source, target)
            : (target, source);

        Connections.Add(new NodeConnection
        {
            SourceNodeId = src.OwnerNodeId,
            SourcePortId = src.PortId,
            TargetNodeId = tgt.OwnerNodeId,
            TargetPortId = tgt.PortId,
        });

        HasUnsavedChanges = true;
        StatusText        = "연결 추가됨";
    }

    // §10 ─ 뷰 제어 커맨드 ───────────────────────────────────

    [RelayCommand]
    private void ZoomIn()  => Scale = Math.Min(Scale + 0.1, 3.0);

    [RelayCommand]
    private void ZoomOut() => Scale = Math.Max(Scale - 0.1, 0.3);

    [RelayCommand]
    private void ResetView()
    {
        Scale   = 1.0;
        OffsetX = 0;
        OffsetY = 0;
    }

    [RelayCommand]
    private void AutoLayout()
    {
        // 카테고리별 열 배치 (Input → Parser → Filter → Output)
        var categories = new[] { "Input", "Parser", "Filter", "Output" };
        var grouped    = Nodes.GroupBy(n => n.Category).ToDictionary(g => g.Key);

        double colX = 60;
        foreach (var cat in categories)
        {
            if (!grouped.TryGetValue(cat, out var group)) continue;
            double rowY = 60;
            foreach (var node in group)
            {
                node.X  = colX;
                node.Y  = rowY;
                rowY   += node.Height + 30;
            }
            colX += 200;
        }

        // 미분류 노드
        var uncategorized = Nodes.Where(n => !categories.Contains(n.Category)).ToList();
        double ux = colX;
        double uy = 60;
        foreach (var node in uncategorized)
        {
            node.X  = ux;
            node.Y  = uy;
            uy     += node.Height + 30;
        }

        StatusText = "자동 배치 완료";
    }

    [RelayCommand]
    private void ClearAll()
    {
        Nodes.Clear();
        Connections.Clear();
        SelectedNode       = null;
        SelectedConnection = null;
        HasUnsavedChanges  = true;
        StatusText         = "캔버스 초기화";
    }

    // §11 ─ 샘플 플로우 로드 ──────────────────────────────────

    [RelayCommand]
    private void LoadSampleFlow()
    {
        ClearAll();

        // Input → ScaleMap → SDTFilter → DbOutput 기본 플로우
        var input    = new ModbusInputNode  { X = 60,  Y = 100 };
        var scaleMap = new ScaleMapNode     { X = 280, Y = 100 };
        var sdt      = new SdtFilterNode    { X = 500, Y = 100 };
        var dbOut    = new DbOutputNode     { X = 720, Y = 100 };
        var mqttOut  = new MqttOutputNode   { X = 720, Y = 220 };

        input.SetProp("address", "40001");
        input.SetProp("deviceId", "PLC-001");
        scaleMap.SetProp("rawMin", "0");
        scaleMap.SetProp("rawMax", "4095");
        scaleMap.SetProp("engMax", "100");
        scaleMap.SetProp("unit", "°C");

        foreach (var n in new AbstractNode[] { input, scaleMap, sdt, dbOut, mqttOut })
        {
            foreach (var p in n.InputPorts.Concat(n.OutputPorts))
                p.OwnerNodeId = n.NodeId;
            Nodes.Add(n);
        }

        // 자동 연결: Input→ScaleMap→SDT→DbOutput / SDT→MqttOutput
        _TryAutoConnect(input,    scaleMap);
        _TryAutoConnect(scaleMap, sdt);
        _TryAutoConnect(sdt,      dbOut);
        _TryAutoConnect(sdt,      mqttOut);

        HasUnsavedChanges = false;
        StatusText        = "샘플 플로우 로드 완료";
        LogManager.Instance.Info(LogSrc, "샘플 플로우 로드");
    }

    // §12 ─ collect.json 직렬화 ───────────────────────────────

    /// <summary>현재 캔버스를 collect.json 직렬화 문자열로 반환합니다.</summary>
    public string SerializeToJson()
    {
        var data = new
        {
            Meta = new { Version = "1.0", UpdatedAt = DateTime.UtcNow.ToString("O") },
            Nodes = Nodes.Select(n => n.ToSerializable()).ToList(),
            Connections = Connections.Select(c => new
            {
                c.ConnectionId,
                c.SourceNodeId, c.SourcePortId,
                c.TargetNodeId, c.TargetPortId,
            }).ToList(),
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    /// <summary>collect.json 에서 캔버스를 복원합니다.</summary>
    public void DeserializeFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            ClearAll();

            // 노드 복원
            if (root.TryGetProperty("Nodes", out var nodesEl))
            {
                foreach (var nodeEl in nodesEl.EnumerateArray())
                {
                    var nodeType = nodeEl.GetProperty("nodeType").GetString() ?? string.Empty;
                    var node     = _CreateNode(nodeType);
                    if (node is null) continue;

                    // 기본 필드 복원
                    if (nodeEl.TryGetProperty("nodeId",    out var id))
                        // NodeId는 init-only이므로 레코드 재생성 불가 → Label만 복원
                        node.Label = nodeEl.TryGetProperty("label", out var lbl)
                            ? lbl.GetString() ?? string.Empty : string.Empty;

                    if (nodeEl.TryGetProperty("x", out var x)) node.X = x.GetDouble();
                    if (nodeEl.TryGetProperty("y", out var y)) node.Y = y.GetDouble();

                    // Properties 복원
                    if (nodeEl.TryGetProperty("properties", out var propsEl))
                        foreach (var prop in propsEl.EnumerateObject())
                            node.SetProp(prop.Name, prop.Value.GetString() ?? string.Empty);

                    foreach (var p in node.InputPorts.Concat(node.OutputPorts))
                        p.OwnerNodeId = node.NodeId;

                    Nodes.Add(node);
                }
            }

            HasUnsavedChanges = false;
            StatusText        = $"collect.json 로드 — {Nodes.Count}개 노드";
            LogManager.Instance.Info(LogSrc, StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"로드 오류: {ex.Message}";
            LogManager.Instance.Error(LogSrc, StatusText);
        }
    }

    // §13 ─ 내부 헬퍼 ─────────────────────────────────────────

    private static AbstractNode? _CreateNode(string nodeType) => nodeType switch
    {
        "ModbusInput"     => new ModbusInputNode(),
        "OpcUaInput"      => new OpcUaInputNode(),
        "VirtualInput"    => new VirtualInputNode(),
        "BufParser"       => new BufParserNode(),
        "ScaleMap"        => new ScaleMapNode(),
        "ThresholdFilter" => new ThresholdFilterNode(),
        "SDTFilter"       => new SdtFilterNode(),
        "DbOutput"        => new DbOutputNode(),
        "MqttOutput"      => new MqttOutputNode(),
        "DebugOutput"     => new DebugOutputNode(),
        _ => null,
    };

    private void _TryAutoConnect(AbstractNode from, AbstractNode to)
    {
        var src = from.OutputPorts.FirstOrDefault();
        var tgt = to.InputPorts.FirstOrDefault();
        if (src is null || tgt is null) return;
        ConnectPorts(src, tgt);
    }

    private static IReadOnlyList<PaletteItem> _BuildPalette() =>
    [
        new("Input",  "📡", "ModbusInput",     "Modbus Input",     "#4F7CFF"),
        new("Input",  "🌐", "OpcUaInput",       "OPC-UA Input",     "#0284C7"),
        new("Input",  "🔬", "VirtualInput",     "Virtual Input",    "#22D3A0"),
        new("Parser", "📦", "BufParser",        "Buf Parser",       "#7C5CFF"),
        new("Parser", "📐", "ScaleMap",         "Scale Map",        "#F59E0B"),
        new("Filter", "🚦", "ThresholdFilter",  "Threshold Filter", "#FF8C42"),
        new("Filter", "🗜️",  "SDTFilter",        "SDT Filter",       "#059669"),
        new("Output", "💾", "DbOutput",         "DB Output",        "#DC2626"),
        new("Output", "📤", "MqttOutput",       "MQTT Output",      "#7C3AED"),
        new("Output", "🐛", "DebugOutput",      "Debug",            "#4A5568"),
    ];
}

// §14 ─ 팔레트 항목 모델 ──────────────────────────────────
public sealed record PaletteItem(
    string Category,
    string Icon,
    string NodeType,
    string DisplayName,
    string Color);
