// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/Canvas/CanvasView.xaml.cs
//  역할: 캔버스 인터랙션 코드비하인드
//  S-11: 노드 드래그·줌·패닝
//  S-12: 포트 드래그 → 연결선 생성
//        노드 이동 완료 후 RefreshConnections() 호출
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Canvas;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
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

    // 포트 연결 드래그 (★ S-12)
    private bool     _isDraggingPort;
    private NodePort? _dragSourcePort;
    private AbstractCanvasNode? _dragSourceNode;

    // §2 ─ 생성자 ─────────────────────────────────────────────

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
        => _vm = DataContext as CanvasViewModel;

    // §3 ─ 마우스 다운 ────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;

        // ── 패닝 (중간버튼 or Space+좌클릭) ─────────────────
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
        var hit = VisualTreeHelper.HitTest(RootCanvas, posOnCanvas)?.VisualHit;

        // ── 포트 클릭 감지 (★ S-12) ──────────────────────────
        var (portNode, port) = _FindPortFromVisual(hit);
        if (portNode is not null && port is not null)
        {
            // 출력 포트 드래그 시작
            if (port.Direction == PortDirection.Output)
            {
                _isDraggingPort = true;
                _dragSourcePort = port;
                _dragSourceNode = portNode;
                _vm.BeginConnectionDrag(portNode, port);
                CanvasBorder.CaptureMouse();
                e.Handled = true;
            }
            return;
        }

        // ── 노드 카드 클릭 ────────────────────────────────────
        var node = _FindNodeFromVisual(hit);
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

        // 패닝
        if (_isPanning)
        {
            var cur = e.GetPosition(CanvasBorder);
            _vm.OffsetX = _panStartOffsetX + (cur.X - _panStart.X);
            _vm.OffsetY = _panStartOffsetY + (cur.Y - _panStart.Y);
            return;
        }

        // 포트 연결 드래그 미리보기 (★ S-12)
        if (_isDraggingPort && e.LeftButton == MouseButtonState.Pressed)
        {
            // 캔버스 좌표 = (마우스 - 오프셋) / 스케일
            var posOnBorder = e.GetPosition(CanvasBorder);
            var cx = (posOnBorder.X - _vm.OffsetX) / _vm.Scale;
            var cy = (posOnBorder.Y - _vm.OffsetY) / _vm.Scale;
            _vm.UpdateConnectionDrag(cx, cy);
            return;
        }

        // 노드 드래그
        if (_draggingNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var cur = e.GetPosition(RootCanvas);
            _draggingNode.X = _nodeStartX + (cur.X - _dragStart.X);
            _draggingNode.Y = _nodeStartY + (cur.Y - _dragStart.Y);
            // 연결선 실시간 갱신 (★ S-12)
            _vm.RefreshConnections(_draggingNode.NodeId);
        }
    }

    // §5 ─ 마우스 업 ──────────────────────────────────────────

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;

        // 패닝 종료
        if (_isPanning
            && (e.ChangedButton == MouseButton.Middle
                || e.ChangedButton == MouseButton.Left))
        {
            _isPanning = false;
            CanvasBorder.ReleaseMouseCapture();
            return;
        }

        // 포트 연결 완료 (★ S-12)
        if (_isDraggingPort && e.ChangedButton == MouseButton.Left)
        {
            var posOnCanvas = e.GetPosition(RootCanvas);
            var hit = VisualTreeHelper.HitTest(RootCanvas, posOnCanvas)?.VisualHit;
            var (targetNode, targetPort) = _FindPortFromVisual(hit);

            // 입력 포트 위에서 놓으면 연결 확정
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

        // 노드 드래그 종료
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

    // §7 ─ 비주얼 트리 역추적 헬퍼 ───────────────────────────

    /// <summary>비주얼에서 노드 카드 ContentPresenter → AbstractCanvasNode 역추적</summary>
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

    /// <summary>
    /// 비주얼에서 포트 Ellipse → (소유 노드, NodePort) 역추적. (★ S-12)
    /// 포트 Ellipse 의 Tag = NodePort 인스턴스.
    /// 포트 소유 노드는 포트의 OwnerNodeId 로 Nodes 컬렉션에서 검색.
    /// </summary>
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
