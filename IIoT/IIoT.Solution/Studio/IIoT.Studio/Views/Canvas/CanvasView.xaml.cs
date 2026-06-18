// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/Canvas/CanvasView.xaml.cs
//  역할: 캔버스 인터랙션 코드비하인드
//  S-11: 노드 드래그·줌·패닝
//  S-12: 포트 드래그 → 연결선 생성
//  S-12B: PortsLayer 절대좌표 갱신 (HitTest 완전 해결)
//         DeviceTypeIconConverter 추가
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Canvas;
using IIoT.Studio.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IIoT.Studio.Views.Canvas;

// §2 ─ CanvasView ─────────────────────────────────────────

public partial class CanvasView : UserControl
{
    // §2-1 ─ 필드 ─────────────────────────────────────────────

    private CanvasViewModel? _vm;

    // 노드 드래그
    private AbstractCanvasNode? _draggingNode;
    private Point                _dragStart;
    private double               _nodeStartX;
    private double               _nodeStartY;

    // 패닝
    private bool   _isPanning;
    private Point  _panStart;
    private double _panStartOffsetX;
    private double _panStartOffsetY;
    private bool   _spaceDown;

    // 포트 연결 드래그
    private bool              _isDraggingPort;
    private NodePort?         _dragSourcePort;
    private AbstractCanvasNode? _dragSourceNode;

    // §2-2 ─ 생성자 ───────────────────────────────────────────

    public CanvasView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        CanvasBorder.PreviewMouseDown  += OnCanvasMouseDown;
        CanvasBorder.PreviewMouseMove  += OnCanvasMouseMove;
        CanvasBorder.PreviewMouseUp    += OnCanvasMouseUp;
        CanvasBorder.PreviewMouseWheel += OnCanvasWheel;

        Loaded  += (_, _) => Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Space)  { _spaceDown = true;  e.Handled = true; }
            if (e.Key == Key.Delete) { _vm?.DeleteSelectedCommand.Execute(null); }
        };
        KeyUp += (_, e) =>
        {
            if (e.Key == Key.Space) { _spaceDown = false; e.Handled = true; }
        };
        Focusable = true;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _vm = DataContext as CanvasViewModel;
        if (_vm is not null)
        {
            // 노드 추가/삭제 시 PortsLayer 갱신
            _vm.Nodes.CollectionChanged += (_, _) => _RebuildPortsLayer();
        }
    }

    // §3 ─ 마우스 다운 ────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;

        // 패닝
        if (e.ChangedButton == MouseButton.Middle
            || (e.ChangedButton == MouseButton.Left && _spaceDown))
        {
            _isPanning       = true;
            _panStart        = e.GetPosition(CanvasBorder);
            _panStartOffsetX = _vm.OffsetX;
            _panStartOffsetY = _vm.OffsetY;
            CanvasBorder.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left) return;

        var posOnCanvas = e.GetPosition(RootCanvas);

        // ── ★ S-12B: PortsLayer에서 포트 먼저 감지 ───────────
        var portHit = VisualTreeHelper.HitTest(PortsLayer, posOnCanvas)?.VisualHit;
        var (portNode, port) = _FindPortFromVisual(portHit);

        if (portNode is not null && port is not null
            && port.Direction == PortDirection.Output)
        {
            _isDraggingPort = true;
            _dragSourcePort = port;
            _dragSourceNode = portNode;
            _vm.BeginConnectionDrag(portNode, port);
            CanvasBorder.CaptureMouse();
            e.Handled = true;
            return;
        }

        // ── 노드 카드 클릭 ────────────────────────────────────
        var nodeHit = VisualTreeHelper.HitTest(NodesLayer, posOnCanvas)?.VisualHit;
        var node    = _FindNodeFromVisual(nodeHit);

        if (node is not null)
        {
            _vm.SelectNode(node);
            _draggingNode = node;
            _dragStart    = posOnCanvas;
            _nodeStartX   = node.X;
            _nodeStartY   = node.Y;
            NodesLayer.CaptureMouse();
            e.Handled = true;
        }
        else
        {
            _vm.SelectNode(null);
        }
    }

    // §4 ─ 마우스 이동 ────────────────────────────────────────

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_vm is null) return;

        if (_isPanning)
        {
            var cur = e.GetPosition(CanvasBorder);
            _vm.OffsetX = _panStartOffsetX + (cur.X - _panStart.X);
            _vm.OffsetY = _panStartOffsetY + (cur.Y - _panStart.Y);
            return;
        }

        if (_isDraggingPort && e.LeftButton == MouseButtonState.Pressed)
        {
            var posOnBorder = e.GetPosition(CanvasBorder);
            var cx = (posOnBorder.X - _vm.OffsetX) / _vm.Scale;
            var cy = (posOnBorder.Y - _vm.OffsetY) / _vm.Scale;
            _vm.UpdateConnectionDrag(cx, cy);
            return;
        }

        if (_draggingNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var cur = e.GetPosition(RootCanvas);
            _draggingNode.X = _nodeStartX + (cur.X - _dragStart.X);
            _draggingNode.Y = _nodeStartY + (cur.Y - _dragStart.Y);
            _vm.RefreshConnections(_draggingNode.NodeId);
            // ★ 노드 이동 시 포트 위치도 갱신
            _UpdatePortPositions(_draggingNode);
        }
    }

    // §5 ─ 마우스 업 ──────────────────────────────────────────

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;

        if (_isPanning
            && (e.ChangedButton == MouseButton.Middle
                || e.ChangedButton == MouseButton.Left))
        {
            _isPanning = false;
            CanvasBorder.ReleaseMouseCapture();
            return;
        }

        if (_isDraggingPort && e.ChangedButton == MouseButton.Left)
        {
            var posOnCanvas = e.GetPosition(RootCanvas);
            var portHit = VisualTreeHelper.HitTest(PortsLayer, posOnCanvas)?.VisualHit;
            var (targetNode, targetPort) = _FindPortFromVisual(portHit);

            if (targetNode is not null
                && targetPort is not null
                && targetPort.Direction == PortDirection.Input
                && _dragSourceNode is not null
                && _dragSourcePort is not null)
            {
                _vm.AddConnection(
                    _dragSourceNode.NodeId, _dragSourcePort.PortId,
                    targetNode.NodeId,      targetPort.PortId);
            }

            _isDraggingPort = false;
            _dragSourcePort = null;
            _dragSourceNode = null;
            _vm.EndConnectionDrag();
            CanvasBorder.ReleaseMouseCapture();
            return;
        }

        if (_draggingNode is not null && e.ChangedButton == MouseButton.Left)
        {
            _vm.RefreshConnections(_draggingNode.NodeId);
            _draggingNode = null;
            NodesLayer.ReleaseMouseCapture();
        }
    }

    // §6 ─ 휠 줌 ──────────────────────────────────────────────

    private void OnCanvasWheel(object sender, MouseWheelEventArgs e)
    {
        _vm?.ApplyWheelZoom(e.Delta);
        e.Handled = true;
    }

    // §7 ─ PortsLayer 관리 (★ S-12B 핵심) ────────────────────

    /// <summary>
    /// 전체 PortsLayer 재구성.
    /// 노드 추가/삭제 시 호출.
    /// </summary>
    private void _RebuildPortsLayer()
    {
        PortsLayer.Children.Clear();
        if (_vm is null) return;

        foreach (var node in _vm.Nodes)
        {
            _CreatePortEllipses(node);
        }
    }

    /// <summary>
    /// 특정 노드의 포트 Ellipse 위치만 갱신.
    /// 노드 드래그 중 호출.
    /// </summary>
    private void _UpdatePortPositions(AbstractCanvasNode node)
    {
        foreach (UIElement el in PortsLayer.Children)
        {
            if (el is Ellipse ell && ell.Tag is NodePort port
                && port.OwnerNodeId == node.NodeId)
            {
                if (port.Direction == PortDirection.Input)
                {
                    System.Windows.Controls.Canvas.SetLeft(ell,
                        node.InputPortX - NodeLayout.PortRadius);
                    System.Windows.Controls.Canvas.SetTop(ell,
                        node.GetPortCanvasY(port.Index) - NodeLayout.PortRadius);
                }
                else
                {
                    System.Windows.Controls.Canvas.SetLeft(ell,
                        node.OutputPortX - NodeLayout.PortRadius);
                    System.Windows.Controls.Canvas.SetTop(ell,
                        node.GetPortCanvasY(port.Index) - NodeLayout.PortRadius);
                }
            }
        }
    }

    /// <summary>노드의 모든 포트 Ellipse를 PortsLayer에 생성</summary>
    private void _CreatePortEllipses(AbstractCanvasNode node)
    {
        double r = NodeLayout.PortRadius;
        double d = r * 2;

        foreach (var port in node.InputPorts)
        {
            var ell = _MakePortEllipse(port, isOutput: false, d: d);
            System.Windows.Controls.Canvas.SetLeft(ell,
                node.InputPortX - r);
            System.Windows.Controls.Canvas.SetTop(ell,
                node.GetPortCanvasY(port.Index) - r);
            PortsLayer.Children.Add(ell);
        }

        foreach (var port in node.OutputPorts)
        {
            var ell = _MakePortEllipse(port, isOutput: true, d: d);
            System.Windows.Controls.Canvas.SetLeft(ell,
                node.OutputPortX - r);
            System.Windows.Controls.Canvas.SetTop(ell,
                node.GetPortCanvasY(port.Index) - r);
            PortsLayer.Children.Add(ell);
        }
    }

    private Ellipse _MakePortEllipse(NodePort port, bool isOutput, double d)
    {
        var ell = new Ellipse
        {
            Width           = d,
            Height          = d,
            Tag             = port,
            Cursor          = Cursors.Cross,
            ToolTip         = new ToolTip { Content = port.Label }
        };

        if (isOutput)
        {
            ell.Fill   = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#60a5fa"));
            ell.Stroke = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#1e40af"));
            ell.StrokeThickness = 1.5;
        }
        else
        {
            ell.Fill   = new SolidColorBrush(Colors.Transparent);
            ell.Stroke = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#60a5fa"));
            ell.StrokeThickness = 2;
        }

        return ell;
    }

    // §8 ─ 비주얼 트리 역추적 ─────────────────────────────────

    private static AbstractCanvasNode? _FindNodeFromVisual(DependencyObject? visual)
    {
        while (visual is not null)
        {
            if (visual is ContentPresenter cp
                && cp.Content is AbstractCanvasNode node)
                return node;
            visual = VisualTreeHelper.GetParent(visual);
        }
        return null;
    }

    private (AbstractCanvasNode? node, NodePort? port)
        _FindPortFromVisual(DependencyObject? visual)
    {
        while (visual is not null)
        {
            if (visual is Ellipse el && el.Tag is NodePort port && _vm is not null)
            {
                var owner = _vm.Nodes.FirstOrDefault(
                    n => n.NodeId == port.OwnerNodeId);
                return (owner, port);
            }
            visual = VisualTreeHelper.GetParent(visual);
        }
        return (null, null);
    }
}
