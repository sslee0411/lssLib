// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/CanvasViewModel.cs
//  역할: NodeRed 캔버스 ViewModel
//  S-11: 초기 구현
//  S-12: 연결 드래그 상태 + RefreshConnections
//  S-12B: DeviceTreeViewModel 주입 + DevicePaletteItems + AddDeviceNodeCommand
//  S-13B: ApplyTemplate() 추가 (템플릿 → Tag + BufferParser 자동생성)
//  S-20 (N포트 노드): IsSplitterSelected/IsCompositeCalcSelected 계산 프로퍼티 +
//               AddSelectedNodeInputPort/OutputPort·RemoveSelectedNodeInputPort/
//               OutputPort 커맨드(포트 삭제 시 연결된 Connections 정리 포함) +
//               NodePortsChanged 이벤트(코드비하인드가 PortsLayer 재구성 트리거용)
//  생성: 2026-06-17 / 수정: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Canvas;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    // §1 ─ 주입 ───────────────────────────────────────────────

    private readonly DeviceTreeViewModel _deviceTreeVm;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public CanvasViewModel(DeviceTreeViewModel deviceTreeVm)
    {
        _deviceTreeVm = deviceTreeVm;
        // ★ RootNodes.CollectionChanged 만으로는 하위 노드 추가 감지 불가
        //   → MainViewModel.SwitchTab(1) 시 RefreshDevicePalette() 명시 호출
    }

    // §3 ─ 컬렉션 ─────────────────────────────────────────────

    public ObservableCollection<AbstractCanvasNode> Nodes       { get; } = new();
    public ObservableCollection<CanvasConnection>   Connections { get; } = new();

    // §4 ─ 팔레트 ─────────────────────────────────────────────

    /// <summary>처리 노드 팔레트 (고정)</summary>
    public IReadOnlyList<PaletteItem> ProcessItems
        => CanvasNodeFactory.ProcessItems;

    /// <summary>장비 팔레트 — 장비 트리의 PLC/장비를 동적 열거</summary>
    public IReadOnlyList<DevicePaletteItem> DevicePaletteItems
        => _CollectDevices(_deviceTreeVm.RootNodes).ToList();

    /// <summary>
    /// 장비 팔레트 강제 갱신.
    /// ★ MainViewModel.SwitchTab(1) 진입 시 호출.
    /// </summary>
    public void RefreshDevicePalette()
        => OnPropertyChanged(nameof(DevicePaletteItems));

    // §5 ─ 선택 노드 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(IsSplitterSelected))]
    [NotifyPropertyChangedFor(nameof(IsCompositeCalcSelected))]
    private AbstractCanvasNode? _selectedNode;

    public bool HasSelection => SelectedNode is not null;

    // ★ S-20: N포트 노드 속성 패널 표시 여부
    public bool IsSplitterSelected      => SelectedNode is SplitterNode;
    public bool IsCompositeCalcSelected => SelectedNode is CompositeCalcNode;

    /// <summary>포트 개수 변경(추가/삭제) 시 발생 — CanvasView 코드비하인드가
    /// PortsLayer(수동 Ellipse 배치)를 재구성하도록 알림.</summary>
    public event Action? NodePortsChanged;

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

    public NodePort?           DragSourcePort { get; private set; }
    public AbstractCanvasNode? DragSourceNode { get; private set; }

    // §8 ─ 노드 추가 커맨드 ───────────────────────────────────

    [RelayCommand]
    private void AddNode(string nodeType)
    {
        var node = CanvasNodeFactory.Create(nodeType);
        if (node is null) return;
        _PlaceNode(node);
    }

    [RelayCommand]
    private void AddDeviceNode(DevicePaletteItem item)
    {
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

    // §9-1 ─ ★ S-20: N포트 노드 포트 추가/삭제 ────────────────

    [RelayCommand]
    private void AddSelectedNodeInputPort()
    {
        if (SelectedNode is null) return;
        SelectedNode.AddInputPort();
        NodePortsChanged?.Invoke();
    }

    [RelayCommand]
    private void AddSelectedNodeOutputPort()
    {
        if (SelectedNode is null) return;
        SelectedNode.AddOutputPort();
        NodePortsChanged?.Invoke();
    }

    [RelayCommand]
    private void RemoveSelectedNodeInputPort(NodePort port)
    {
        if (SelectedNode is null || port is null) return;
        if (!SelectedNode.RemoveInputPort(port)) return;
        _RemoveConnectionsForPort(port.PortId);
        NodePortsChanged?.Invoke();
    }

    [RelayCommand]
    private void RemoveSelectedNodeOutputPort(NodePort port)
    {
        if (SelectedNode is null || port is null) return;
        if (!SelectedNode.RemoveOutputPort(port)) return;
        _RemoveConnectionsForPort(port.PortId);
        NodePortsChanged?.Invoke();
    }

    private void _RemoveConnectionsForPort(string portId)
    {
        var dangling = Connections
            .Where(c => c.SourcePortId == portId || c.TargetPortId == portId)
            .ToList();
        foreach (var c in dangling) Connections.Remove(c);
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

    // §13 ─ 태그 템플릿 적용 (★ S-13B) ──────────────────────

    /// <summary>
    /// DeviceNode에 템플릿 적용:
    /// ① 장비 트리에 Tag 자동 추가
    /// ② DeviceNode Tag 미리보기 갱신
    /// ③ BufferParser 노드 자동 생성
    /// ④ DeviceNode → BufferParser 연결선 자동 생성
    /// </summary>
    public void ApplyTemplate(
        DeviceCanvasNode    targetNode,
        TagTemplate         template,
        int                 startAddress,
        DeviceTreeViewModel deviceTree)
    {
        // ① 장비 트리에 Tag 추가
        var treeNode = _FindTreeNode(deviceTree.RootNodes, targetNode.LinkedDeviceId);
        if (treeNode is not null)
        {
            foreach (var item in template.Items)
            {
                var tag = new TagTreeNode(item.Name)
                {
                    Address  = item.CalcAddress(startAddress).ToString(),
                    DataType = item.BufType,
                    Unit     = item.Unit
                };
                treeNode.Children.Add(tag);
            }
        }

        // ② DeviceNode Tag 미리보기 갱신
        var tagInfos = template.Items.Select(i => new TagInfo(
            Guid.NewGuid().ToString(),
            i.Name,
            i.CalcAddress(startAddress).ToString(),
            i.BufType,
            i.Unit));
        targetNode.SyncTags(tagInfos);

        // ③ BufferParser 노드 자동 생성
        var parser = new BufferParserNode
        {
            Label  = $"{targetNode.Label} Parser",
            Schema = _BuildSchema(template)
        };
        parser.X = targetNode.X + 200;
        parser.Y = targetNode.Y;
        Nodes.Add(parser);

        // ④ 연결선 자동 생성
        var srcPort = targetNode.OutputPorts.FirstOrDefault();
        var tgtPort = parser.InputPorts.FirstOrDefault();
        if (srcPort is not null && tgtPort is not null)
        {
            AddConnection(
                targetNode.NodeId, srcPort.PortId,
                parser.NodeId,     tgtPort.PortId);
        }
    }

    // §14 ─ 내부 헬퍼 ─────────────────────────────────────────

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

    private static string _BuildSchema(TagTemplate template)
        => string.Join(", ", template.Items.Select(i =>
            $"{i.ByteOffset}:{i.BufType}:{i.Name}"));

    private static AbstractTreeNode? _FindTreeNode(
        IEnumerable<AbstractTreeNode> nodes, string id)
    {
        foreach (var n in nodes)
        {
            if (n.Id.ToString() == id) return n;
            var found = _FindTreeNode(n.Children, id);
            if (found is not null) return found;
        }
        return null;
    }

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
