// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/CanvasViewModel.cs
//  역할: NodeRed 캔버스 ViewModel
//  S-11: 초기 구현
//  S-12: 연결 드래그 상태 + RefreshConnections
//  S-12B: DeviceTreeViewModel 주입
//         DevicePaletteItems (장비 섹션 동적 제공)
//         AddDeviceNodeCommand (장비 팔레트 더블클릭)
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Canvas;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    // §1 ─ 주입 (★ S-12B) ────────────────────────────────────

    private readonly DeviceTreeViewModel _deviceTreeVm;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public CanvasViewModel(DeviceTreeViewModel deviceTreeVm)
    {
        _deviceTreeVm = deviceTreeVm;
        // 장비 트리 변경 시 팔레트 자동 갱신
        _deviceTreeVm.RootNodes.CollectionChanged += (_, _)
            => OnPropertyChanged(nameof(DevicePaletteItems));
    }

    // §3 ─ 컬렉션 ─────────────────────────────────────────────

    public ObservableCollection<AbstractCanvasNode> Nodes       { get; } = new();
    public ObservableCollection<CanvasConnection>   Connections { get; } = new();

    // §4 ─ 팔레트 ─────────────────────────────────────────────

    /// <summary>처리 노드 팔레트 (고정)</summary>
    public IReadOnlyList<PaletteItem> ProcessItems
        => CanvasNodeFactory.ProcessItems;

    /// <summary>
    /// 장비 팔레트 (★ S-12B) — 장비 트리의 PLC/장비를 동적으로 열거.
    /// 트리 변경 시 자동 갱신.
    /// </summary>
    public IReadOnlyList<DevicePaletteItem> DevicePaletteItems
        => _CollectDevices(_deviceTreeVm.RootNodes).ToList();

    // §5 ─ 선택 노드 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private AbstractCanvasNode? _selectedNode;

    public bool HasSelection => SelectedNode is not null;

    // §6 ─ 줌·패닝 ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePercent))]
    private double _scale = 1.0;

    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;

    public string ScalePercent => $"{Scale * 100:F0}%";

    // §7 ─ 드래그 연결선 미리보기 ────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraggingConnection))]
    private string _dragConnectionPath = string.Empty;

    public bool IsDraggingConnection
        => !string.IsNullOrEmpty(DragConnectionPath);

    public NodePort?              DragSourcePort { get; private set; }
    public AbstractCanvasNode?    DragSourceNode { get; private set; }

    // §8 ─ 노드 추가 커맨드 ───────────────────────────────────

    /// <summary>처리 노드 팔레트 버튼 클릭</summary>
    [RelayCommand]
    private void AddNode(string nodeType)
    {
        var node = CanvasNodeFactory.Create(nodeType);
        if (node is null) return;
        _PlaceNode(node);
    }

    /// <summary>
    /// 장비 팔레트 항목 더블클릭 (★ S-12B).
    /// DeviceId 기준으로 DeviceCanvasNode 생성 후 캔버스에 추가.
    /// </summary>
    [RelayCommand]
    private void AddDeviceNode(DevicePaletteItem item)
    {
        // 이미 캔버스에 있으면 중복 추가 방지
        if (Nodes.OfType<DeviceCanvasNode>()
                 .Any(n => n.LinkedDeviceId == item.DeviceId)) return;

        var node = new DeviceCanvasNode
        {
            LinkedDeviceId   = item.DeviceId,
            LinkedDeviceType = item.DeviceType,
            LinkedDeviceName = item.Name,
        };
        node.Label = item.Name;
        node.SyncTags(item.Tags);
        _PlaceNode(node);
    }

    // §9 ─ 선택·삭제 ──────────────────────────────────────────

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

    // §10 ─ 줌 커맨드 ─────────────────────────────────────────

    [RelayCommand] private void ZoomIn()  => Scale = Math.Min(3.0, Scale + 0.1);
    [RelayCommand] private void ZoomOut() => Scale = Math.Max(0.3, Scale - 0.1);

    [RelayCommand]
    private void ZoomReset() { Scale = 1.0; OffsetX = 0; OffsetY = 0; }

    public void ApplyWheelZoom(double delta)
    {
        Scale = Math.Clamp(Scale * (delta > 0 ? 1.1 : 0.9), 0.3, 3.0);
        OnPropertyChanged(nameof(ScalePercent));
    }

    // §11 ─ 연결선 관리 ───────────────────────────────────────

    public void AddConnection(
        string sourceNodeId, string sourcePortId,
        string targetNodeId, string targetPortId)
    {
        if (sourceNodeId == targetNodeId) return;
        if (Connections.Any(c =>
                c.SourcePortId == sourcePortId &&
                c.TargetPortId == targetPortId)) return;

        var conn = new CanvasConnection
        {
            SourceNodeId = sourceNodeId, SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId, TargetPortId = targetPortId
        };
        _RefreshOnePath(conn);
        Connections.Add(conn);
    }

    public void RefreshConnections(string nodeId)
    {
        foreach (var conn in Connections)
        {
            if (conn.SourceNodeId == nodeId || conn.TargetNodeId == nodeId)
                _RefreshOnePath(conn);
        }
    }

    // §12 ─ 드래그 미리보기 ───────────────────────────────────

    public void BeginConnectionDrag(AbstractCanvasNode node, NodePort port)
    {
        DragSourceNode = node;
        DragSourcePort = port;
    }

    public void UpdateConnectionDrag(double mouseX, double mouseY)
    {
        if (DragSourceNode is null || DragSourcePort is null) return;
        double x1 = DragSourceNode.OutputPortX;
        double y1 = DragSourceNode.GetPortCanvasY(DragSourcePort.Index);
        double cx = (x1 + mouseX) / 2;
        DragConnectionPath =
            $"M {x1:F1},{y1:F1} C {cx:F1},{y1:F1} {cx:F1},{mouseY:F1} {mouseX:F1},{mouseY:F1}";
    }

    public void EndConnectionDrag()
    {
        DragSourceNode     = null;
        DragSourcePort     = null;
        DragConnectionPath = string.Empty;
    }

    // §13 ─ 내부 헬퍼 ─────────────────────────────────────────

    private void _PlaceNode(AbstractCanvasNode node)
    {
        node.X = 200 + (Nodes.Count % 5) * 30;
        node.Y = 120 + (Nodes.Count / 5) * 120;
        Nodes.Add(node);
        SelectNode(node);
    }

    private void _RefreshOnePath(CanvasConnection conn)
    {
        var src = Nodes.FirstOrDefault(n => n.NodeId == conn.SourceNodeId);
        var tgt = Nodes.FirstOrDefault(n => n.NodeId == conn.TargetNodeId);
        if (src is null || tgt is null) return;

        var srcPort = src.OutputPorts.FirstOrDefault(p => p.PortId == conn.SourcePortId);
        var tgtPort = tgt.InputPorts.FirstOrDefault(p => p.PortId == conn.TargetPortId);

        conn.UpdatePath(
            src.OutputPortX, src.GetPortCanvasY(srcPort?.Index ?? 0),
            tgt.InputPortX,  tgt.GetPortCanvasY(tgtPort?.Index ?? 0));
    }

    /// <summary>장비 트리를 재귀 탐색해 PLC/장비 → DevicePaletteItem 변환</summary>
    private static IEnumerable<DevicePaletteItem> _CollectDevices(
        IEnumerable<AbstractTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is PlcTreeNode plc)
            {
                var tags = plc.Children
                    .OfType<TagTreeNode>()
                    .Select(t => new TagInfo(
                        t.Id.ToString(), t.Name, t.Address,
                        t.DataType, t.Unit ?? ""))
                    .ToList();
                yield return new DevicePaletteItem(
                    plc.Id.ToString(), "PLC", plc.Name, tags);
            }
            else if (node is DeviceTreeNode dev)
            {
                var tags = dev.Children
                    .OfType<TagTreeNode>()
                    .Select(t => new TagInfo(
                        t.Id.ToString(), t.Name, t.Address,
                        t.DataType, t.Unit ?? ""))
                    .ToList();
                yield return new DevicePaletteItem(
                    dev.Id.ToString(), "Device", dev.Name, tags);
            }

            foreach (var child in _CollectDevices(node.Children))
                yield return child;
        }
    }
}
