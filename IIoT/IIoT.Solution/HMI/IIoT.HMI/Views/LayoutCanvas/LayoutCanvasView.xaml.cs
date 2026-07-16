// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/LayoutCanvas/LayoutCanvasView.xaml.cs
//  역할: 레이아웃 캔버스 인터랙션 코드비하인드 — 아이콘 드래그·줌·패닝
//        (IIoT.Studio Views/Canvas/CanvasView.xaml.cs 이식.
//         포트 드래그(§4~6의 _isDraggingPort 분기)와 PortsLayer 관리(§8)는
//         전부 제외 — HM-03 캔버스에는 포트/연결선 개념이 없다)
//  HM-03: 신규
//  HM-07: Loaded 시 _vm.InitializeAsync() 호출 추가 — hmi-layout.json 로드 및
//         마지막 활성 화면 복원 (CollectorManageView.Loaded 패턴과 동일)
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Layout;
using IIoT.HMI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IIoT.HMI.Views.LayoutCanvas;

public partial class LayoutCanvasView : UserControl
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private LayoutCanvasViewModel? _vm;

    // 노드 드래그
    private AbstractLayoutNode? _draggingNode;
    private Point                _dragStart;
    private double               _nodeStartX;
    private double               _nodeStartY;

    // 패닝
    private bool   _isPanning;
    private Point  _panStart;
    private double _panStartOffsetX;
    private double _panStartOffsetY;
    private bool   _spaceDown;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    /// <summary>
    /// ★ DI로 LayoutCanvasViewModel 을 직접 주입받아 DataContext 로 설정한다
    /// (CollectorManageView 와 동일 패턴 — 부모의 DataContext 상속에 의존하지 않음).
    /// </summary>
    public LayoutCanvasView(LayoutCanvasViewModel viewModel)
    {
        InitializeComponent();

        _vm         = viewModel;
        DataContext = viewModel;

        CanvasBorder.PreviewMouseDown  += OnCanvasMouseDown;
        CanvasBorder.PreviewMouseMove  += OnCanvasMouseMove;
        CanvasBorder.PreviewMouseUp    += OnCanvasMouseUp;
        CanvasBorder.PreviewMouseWheel += OnCanvasWheel;

        Loaded  += async (_, _) =>
        {
            Focus();
            if (_vm is not null) await _vm.InitializeAsync();
        };
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

    // §3 ─ 마우스 다운 ────────────────────────────────────────

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
        var nodeHit     = VisualTreeHelper.HitTest(NodesLayer, posOnCanvas)?.VisualHit;
        var node        = _FindNodeFromVisual(nodeHit);

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

        if (_draggingNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var cur = e.GetPosition(RootCanvas);
            _draggingNode.X = _nodeStartX + (cur.X - _dragStart.X);
            _draggingNode.Y = _nodeStartY + (cur.Y - _dragStart.Y);
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

        if (_draggingNode is not null && e.ChangedButton == MouseButton.Left)
        {
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

    // §7 ─ 비주얼 트리 역추적 ────────────────────────────────

    private static AbstractLayoutNode? _FindNodeFromVisual(DependencyObject? visual)
    {
        while (visual is not null)
        {
            if (visual is ContentPresenter cp
                && cp.Content is AbstractLayoutNode node)
                return node;
            visual = VisualTreeHelper.GetParent(visual);
        }
        return null;
    }
}
