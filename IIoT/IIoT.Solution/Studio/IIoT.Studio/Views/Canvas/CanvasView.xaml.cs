// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/Canvas/CanvasView.xaml.cs
//  역할: 캔버스 인터랙션 코드비하인드
//  S-11: 노드 드래그·줌·패닝
//  S-12: 포트 드래그 → 연결선 생성
//  S-12B: PortsLayer 절대좌표 갱신 (HitTest 완전 해결)
//  S-13B: ApplyTemplateDialog → Views/DeviceTree/ 로 이동
//         using 참조 변경
//  S-20 (N포트 노드): NodePortsChanged 이벤트 구독 → 포트 추가/삭제 시
//               PortsLayer 재구성(_RebuildPortsLayer)
//  생성: 2026-06-17 / 수정: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Canvas;
using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using IIoT.Studio.Views.DeviceTree;    // ★ S-13B: ApplyTemplateDialog 위치 변경
using System.Windows;
using System.Windows.Controls;
using WpfCanvas = System.Windows.Controls.Canvas;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IIoT.Studio.Views.Canvas;

public partial class CanvasView : UserControl
{
    // §1 ─ 필드 ──────────────────────────────────────────────

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
    private bool               _isDraggingPort;
    private NodePort?          _dragSourcePort;
    private AbstractCanvasNode? _dragSourceNode;

    // §2 ─ 외부 주입 속성 ─────────────────────────────────────

    /// <summary>DeviceTreeViewModel — ApplyTemplate 시 Tag 트리에 추가 용도</summary>
    public DeviceTreeViewModel? DeviceTreeVm { get; set; }

    /// <summary>
    /// 템플릿 목록 제공자 (Func 델리게이트).
    /// App.xaml.cs에서 () => tagTemplateVm.Templates 로 주입.
    /// CanvasView는 TagTemplateViewModel 타입을 직접 참조하지 않음.
    /// </summary>
    public Func<IEnumerable<TagTemplate>>? GetTemplates { get; set; }

    // §3 ─ 생성자 ─────────────────────────────────────────────

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
        // ★ S-20: 이전 VM 구독 해제 (DataContext 재바인딩 대비 — 현재는 앱 생애주기 동안
        //   1회만 바인딩되지만, 이중 구독 방지를 위해 안전하게 처리)
        if (_vm is not null)
            _vm.NodePortsChanged -= _RebuildPortsLayer;

        _vm = DataContext as CanvasViewModel;
        if (_vm is not null)
        {
            _vm.Nodes.CollectionChanged += (_, _) => _RebuildPortsLayer();
            // ★ S-20: Splitter/CompositeCalc 포트 추가·삭제 시 PortsLayer 재구성
            _vm.NodePortsChanged += _RebuildPortsLayer;
        }
    }

    // §4 ─ 마우스 다운 ────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;

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

        // 포트 감지 (PortsLayer — 최상단)
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

    // §5 ─ 마우스 이동 ────────────────────────────────────────

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
            _UpdatePortPositions(_draggingNode);
        }
    }

    // §6 ─ 마우스 업 ──────────────────────────────────────────

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

    // §7 ─ 휠 줌 ──────────────────────────────────────────────

    private void OnCanvasWheel(object sender, MouseWheelEventArgs e)
    {
        _vm?.ApplyWheelZoom(e.Delta);
        e.Handled = true;
    }

    // §8 ─ PortsLayer 관리 ────────────────────────────────────

    private void _RebuildPortsLayer()
    {
        PortsLayer.Children.Clear();
        if (_vm is null) return;
        foreach (var node in _vm.Nodes)
            _CreatePortEllipses(node);
    }

    private void _UpdatePortPositions(AbstractCanvasNode node)
    {
        foreach (UIElement el in PortsLayer.Children)
        {
            if (el is Ellipse ell && ell.Tag is NodePort port
                && port.OwnerNodeId == node.NodeId)
            {
                double left = port.Direction == PortDirection.Input
                    ? node.InputPortX  - NodeLayout.PortRadius
                    : node.OutputPortX - NodeLayout.PortRadius;
                WpfCanvas.SetLeft(ell, left);
                WpfCanvas.SetTop(ell, node.GetPortCanvasY(port.Index) - NodeLayout.PortRadius);
            }
        }
    }

    private void _CreatePortEllipses(AbstractCanvasNode node)
    {
        double r = NodeLayout.PortRadius;
        double d = r * 2;

        foreach (var port in node.InputPorts)
        {
            var ell = _MakePortEllipse(port, isOutput: false, d: d);
            WpfCanvas.SetLeft(ell, node.InputPortX - r);
            WpfCanvas.SetTop(ell,  node.GetPortCanvasY(port.Index) - r);
            PortsLayer.Children.Add(ell);
        }
        foreach (var port in node.OutputPorts)
        {
            var ell = _MakePortEllipse(port, isOutput: true, d: d);
            WpfCanvas.SetLeft(ell, node.OutputPortX - r);
            WpfCanvas.SetTop(ell,  node.GetPortCanvasY(port.Index) - r);
            PortsLayer.Children.Add(ell);
        }
    }

    private static Ellipse _MakePortEllipse(NodePort port, bool isOutput, double d)
    {
        var ell = new Ellipse
        {
            Width   = d,
            Height  = d,
            Tag     = port,
            Cursor  = Cursors.Cross,
            ToolTip = new ToolTip { Content = port.Label }
        };
        if (isOutput)
        {
            ell.Fill            = new SolidColorBrush(Color.FromRgb(96, 165, 250));
            ell.Stroke          = new SolidColorBrush(Color.FromRgb(30, 64, 175));
            ell.StrokeThickness = 1.5;
        }
        else
        {
            ell.Fill            = Brushes.Transparent;
            ell.Stroke          = new SolidColorBrush(Color.FromRgb(96, 165, 250));
            ell.StrokeThickness = 2;
        }
        return ell;
    }

    // §9 ─ 태그 템플릿 적용 (★ S-13B) ────────────────────────

    public void OpenApplyTemplateDialog(DeviceCanvasNode targetNode)
    {
        if (GetTemplates is null || _vm is null) return;

        var templates = GetTemplates().ToList();
        if (!templates.Any())
        {
            MessageBox.Show(
                "저장된 태그 템플릿이 없습니다.\n장비 관리 탭의 [태그 템플릿] 패널에서 먼저 작성하세요.",
                "템플릿 없음",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // ★ ApplyTemplateDialog는 Views/DeviceTree/ 에 위치
        var dlg = new ApplyTemplateDialog(templates, Window.GetWindow(this));
        if (dlg.ShowDialog() != true) return;
        if (dlg.ResultTemplate is null) return;

        _vm.ApplyTemplate(
            targetNode,
            dlg.ResultTemplate,
            dlg.ResultStartAddress,
            DeviceTreeVm!);

        _RebuildPortsLayer();
    }

    private void OnContextMenuApplyTemplate(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi
            && mi.Parent is ContextMenu cm
            && cm.PlacementTarget is FrameworkElement fe
            && fe.DataContext is DeviceCanvasNode target)
        {
            OpenApplyTemplateDialog(target);
        }
    }

    // §10 ─ 비주얼 트리 역추적 ────────────────────────────────

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
                var owner = _vm.Nodes.FirstOrDefault(n => n.NodeId == port.OwnerNodeId);
                return (owner, port);
            }
            visual = VisualTreeHelper.GetParent(visual);
        }
        return (null, null);
    }
}
