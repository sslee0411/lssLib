// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/Canvas/CanvasView.xaml.cs
//  역할: 캔버스 인터랙션 (드래그·줌·패닝) 코드비하인드
//  S-11 fix: HexColorConverter → Core/Canvas/ 로 분리
//            (CanvasView.xaml.cs 에서 제거)
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Canvas;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IIoT.Studio.Views.Canvas;

public partial class CanvasView : UserControl
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private CanvasViewModel? _vm;

    // 드래그 상태
    private AbstractCanvasNode? _draggingNode;
    private Point               _dragStart;
    private double              _nodeStartX;
    private double              _nodeStartY;

    // 패닝 상태
    private bool   _isPanning;
    private Point  _panStart;
    private double _panStartOffsetX;
    private double _panStartOffsetY;
    private bool   _spaceDown;

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

        // 패닝 시작 (중간 버튼 또는 Space + 좌클릭)
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

        // 노드 클릭 감지
        var hit = VisualTreeHelper.HitTest(NodesLayer,
            e.GetPosition(NodesLayer))?.VisualHit;
        var node = _FindNodeFromVisual(hit);

        if (node is not null)
        {
            _vm.SelectNode(node);
            _draggingNode = node;
            _dragStart    = e.GetPosition(RootCanvas);
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
        if (_isPanning
            && (e.ChangedButton == MouseButton.Middle
                || e.ChangedButton == MouseButton.Left))
        {
            _isPanning = false;
            CanvasBorder.ReleaseMouseCapture();
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

    // §7 ─ 비주얼 트리에서 노드 역추적 ───────────────────────

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
}
