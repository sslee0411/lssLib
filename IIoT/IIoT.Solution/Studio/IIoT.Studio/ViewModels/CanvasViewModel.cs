// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/CanvasViewModel.cs
//  역할: NodeRed 캔버스 ViewModel
//  S-11: 초기 구현
//  S-12: 연결 드래그 상태 추가
//        RefreshConnections() — 노드 이동 시 연결선 PathData 갱신
//        DragConnection — 드래그 중 미리보기 선
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Canvas;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    // §1 ─ 컬렉션 ─────────────────────────────────────────────

    public ObservableCollection<AbstractCanvasNode> Nodes       { get; } = new();
    public ObservableCollection<CanvasConnection>   Connections { get; } = new();

    // §2 ─ 팔레트 ─────────────────────────────────────────────

    public IReadOnlyList<PaletteItem> PaletteItems => CanvasNodeFactory.AllItems;

    // §3 ─ 선택 노드 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private AbstractCanvasNode? _selectedNode;

    public bool HasSelection => SelectedNode is not null;

    // §4 ─ 줌·패닝 ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePercent))]
    private double _scale = 1.0;

    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;

    public string ScalePercent => $"{Scale * 100:F0}%";

    // §5 ─ 드래그 연결선 미리보기 (★ S-12) ───────────────────

    /// <summary>포트 드래그 중 미리보기 선 PathData</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraggingConnection))]
    private string _dragConnectionPath = string.Empty;

    public bool IsDraggingConnection => !string.IsNullOrEmpty(DragConnectionPath);

    /// <summary>드래그 시작 포트 (출발점 고정)</summary>
    public NodePort? DragSourcePort { get; private set; }

    /// <summary>드래그 시작 노드</summary>
    public AbstractCanvasNode? DragSourceNode { get; private set; }

    // §6 ─ 노드 추가 커맨드 ───────────────────────────────────

    [RelayCommand]
    private void AddNode(string nodeType)
    {
        var node = CanvasNodeFactory.Create(nodeType);
        if (node is null) return;

        node.X = 200 + (Nodes.Count % 5) * 30;
        node.Y = 120 + (Nodes.Count / 5) * 100;

        Nodes.Add(node);
        SelectNode(node);
    }

    // §7 ─ 선택·삭제 ──────────────────────────────────────────

    public void SelectNode(AbstractCanvasNode? node)
    {
        if (SelectedNode is not null) SelectedNode.IsSelected = false;
        SelectedNode = node;
        if (SelectedNode is not null) SelectedNode.IsSelected = true;
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode is null) return;

        var toRemove = Connections
            .Where(c => c.SourceNodeId == SelectedNode.NodeId
                     || c.TargetNodeId == SelectedNode.NodeId)
            .ToList();
        foreach (var c in toRemove) Connections.Remove(c);

        Nodes.Remove(SelectedNode);
        SelectedNode = null;
    }

    // §8 ─ 줌 커맨드 ─────────────────────────────────────────

    [RelayCommand] private void ZoomIn()  => Scale = Math.Min(3.0, Scale + 0.1);
    [RelayCommand] private void ZoomOut() => Scale = Math.Max(0.3, Scale - 0.1);

    [RelayCommand]
    private void ZoomReset() { Scale = 1.0; OffsetX = 0; OffsetY = 0; }

    public void ApplyWheelZoom(double delta)
    {
        Scale = Math.Clamp(Scale * (delta > 0 ? 1.1 : 0.9), 0.3, 3.0);
        OnPropertyChanged(nameof(ScalePercent));
    }

    // §9 ─ 연결선 관리 (★ S-12) ──────────────────────────────

    /// <summary>연결선 추가 — 중복 방지</summary>
    public void AddConnection(
        string sourceNodeId, string sourcePortId,
        string targetNodeId, string targetPortId)
    {
        // 같은 포트 간 중복 연결 방지
        if (Connections.Any(c =>
                c.SourcePortId == sourcePortId &&
                c.TargetPortId == targetPortId)) return;

        // 자기 자신 연결 방지
        if (sourceNodeId == targetNodeId) return;

        var conn = new CanvasConnection
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };

        // 초기 PathData 계산
        var src = Nodes.FirstOrDefault(n => n.NodeId == sourceNodeId);
        var tgt = Nodes.FirstOrDefault(n => n.NodeId == targetNodeId);
        if (src is not null && tgt is not null)
        {
            var srcPort = src.OutputPorts.FirstOrDefault(p => p.PortId == sourcePortId);
            var tgtPort = tgt.InputPorts.FirstOrDefault(p => p.PortId == targetPortId);
            conn.UpdatePath(
                src.OutputPortX, src.GetPortCanvasY(srcPort?.Index ?? 0),
                tgt.InputPortX,  tgt.GetPortCanvasY(tgtPort?.Index ?? 0));
        }

        Connections.Add(conn);
    }

    /// <summary>
    /// 노드 이동 후 해당 노드에 연결된 모든 연결선 PathData 갱신.
    /// 코드비하인드 드래그 완료 시 호출.
    /// </summary>
    public void RefreshConnections(string nodeId)
    {
        var node = Nodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node is null) return;

        foreach (var conn in Connections)
        {
            if (conn.SourceNodeId == nodeId)
            {
                var tgt = Nodes.FirstOrDefault(n => n.NodeId == conn.TargetNodeId);
                if (tgt is null) continue;
                var srcPort = node.OutputPorts.FirstOrDefault(p => p.PortId == conn.SourcePortId);
                var tgtPort = tgt.InputPorts.FirstOrDefault(p => p.PortId == conn.TargetPortId);
                conn.UpdatePath(
                    node.OutputPortX, node.GetPortCanvasY(srcPort?.Index ?? 0),
                    tgt.InputPortX,   tgt.GetPortCanvasY(tgtPort?.Index ?? 0));
            }
            else if (conn.TargetNodeId == nodeId)
            {
                var src = Nodes.FirstOrDefault(n => n.NodeId == conn.SourceNodeId);
                if (src is null) continue;
                var srcPort = src.OutputPorts.FirstOrDefault(p => p.PortId == conn.SourcePortId);
                var tgtPort = node.InputPorts.FirstOrDefault(p => p.PortId == conn.TargetPortId);
                conn.UpdatePath(
                    src.OutputPortX, src.GetPortCanvasY(srcPort?.Index ?? 0),
                    node.InputPortX, node.GetPortCanvasY(tgtPort?.Index ?? 0));
            }
        }
    }

    // §10 ─ 드래그 연결선 미리보기 (★ S-12) ─────────────────

    /// <summary>포트 드래그 시작</summary>
    public void BeginConnectionDrag(AbstractCanvasNode node, NodePort port)
    {
        DragSourceNode = node;
        DragSourcePort = port;
    }

    /// <summary>드래그 중 미리보기 선 업데이트</summary>
    public void UpdateConnectionDrag(double mouseX, double mouseY)
    {
        if (DragSourceNode is null || DragSourcePort is null) return;

        double x1 = DragSourceNode.OutputPortX;
        double y1 = DragSourceNode.GetPortCanvasY(DragSourcePort.Index);
        double cx = (x1 + mouseX) / 2;
        DragConnectionPath =
            $"M {x1:F1},{y1:F1} C {cx:F1},{y1:F1} {cx:F1},{mouseY:F1} {mouseX:F1},{mouseY:F1}";
    }

    /// <summary>드래그 취소 / 완료 후 정리</summary>
    public void EndConnectionDrag()
    {
        DragSourceNode     = null;
        DragSourcePort     = null;
        DragConnectionPath = string.Empty;
    }
}
