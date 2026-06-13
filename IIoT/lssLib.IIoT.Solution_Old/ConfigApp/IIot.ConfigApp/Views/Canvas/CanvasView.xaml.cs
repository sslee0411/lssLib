// ══════════════════════════════════════════════════════════
//  IIoT.ConfigApp · Views/Canvas/CanvasView.xaml.cs
//  역할: 캔버스 코드비하인드
//        · 노드 드래그 이동 (MouseDown/Move/Up)
//        · 노드 선택 (단일 클릭)
//        · 캔버스 패닝 (중간 버튼 드래그 / Space+드래그)
//        · 휠 줌
//  Phase 11: 신규
// ══════════════════════════════════════════════════════════

using IIoT.ConfigApp.Core.Canvas;
using IIoT.ConfigApp.ViewModels.Canvas;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IIoT.ConfigApp.Views.Canvas;

public partial class CanvasView : UserControl
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private AbstractNode? _draggingNode;
    private Point         _dragStart;
    private Point         _nodeStartPos;
    private bool          _isPanning;
    private Point         _panStart;
    private bool          _spaceDown;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public CanvasView()
    {
        InitializeComponent();

        // 키보드 이벤트 (Space 패닝)
        Focusable = true;
        KeyDown  += (_, e) => { if (e.Key == Key.Space) _spaceDown = true; };
        KeyUp    += (_, e) => { if (e.Key == Key.Space) _spaceDown = false; };

        // 캔버스 이벤트
        CanvasBorder.MouseWheel           += _OnMouseWheel;
        CanvasBorder.MouseMiddleButtonDown += _OnPanStart;
        CanvasBorder.MouseMove            += _OnPanMove;
        CanvasBorder.MouseMiddleButtonUp  += _OnPanEnd;

        // 노드 선택/드래그 (NodesControl 에서 이벤트 버블링)
        NodesControl.PreviewMouseLeftButtonDown += _OnNodeMouseDown;
        NodesControl.PreviewMouseMove           += _OnNodeMouseMove;
        NodesControl.PreviewMouseLeftButtonUp   += _OnNodeMouseUp;

        // 빈 캔버스 클릭 → 선택 해제
        CanvasBorder.MouseLeftButtonDown += (_, _) =>
        {
            if (DataContext is CanvasViewModel vm)
                vm.SelectedNode = null;
            Focus();
        };
    }

    // §3 ─ 줌 ─────────────────────────────────────────────────
    private void _OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not CanvasViewModel vm) return;
        double factor = e.Delta > 0 ? 1.1 : 0.9;
        vm.Scale      = Math.Clamp(vm.Scale * factor, 0.3, 3.0);
        e.Handled     = true;
    }

    // §4 ─ 패닝 ───────────────────────────────────────────────
    private void _OnPanStart(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _panStart  = e.GetPosition(CanvasBorder);
        CanvasBorder.CaptureMouse();
    }

    private void _OnPanMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        if (DataContext is not CanvasViewModel vm) return;
        var pos    = e.GetPosition(CanvasBorder);
        vm.OffsetX += pos.X - _panStart.X;
        vm.OffsetY += pos.Y - _panStart.Y;
        _panStart   = pos;
    }

    private void _OnPanEnd(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        CanvasBorder.ReleaseMouseCapture();
    }

    // §5 ─ 노드 드래그 ────────────────────────────────────────

    private void _OnNodeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Source is not FrameworkElement fe) return;
        if (fe.DataContext is not AbstractNode node) return;
        if (DataContext is not CanvasViewModel vm) return;

        vm.SelectedNode = node;
        _draggingNode   = node;
        _dragStart      = e.GetPosition(MainCanvas);
        _nodeStartPos   = new Point(node.X, node.Y);

        fe.CaptureMouse();
        e.Handled = true;
    }

    private void _OnNodeMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingNode is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { _OnNodeMouseUp(sender, null); return; }

        var pos = e.GetPosition(MainCanvas);
        _draggingNode.X = Math.Max(0, _nodeStartPos.X + (pos.X - _dragStart.X));
        _draggingNode.Y = Math.Max(0, _nodeStartPos.Y + (pos.Y - _dragStart.Y));
    }

    private void _OnNodeMouseUp(object sender, MouseButtonEventArgs? e)
    {
        if (_draggingNode is null) return;
        if (e?.Source is FrameworkElement fe) fe.ReleaseMouseCapture();
        if (DataContext is CanvasViewModel vm) vm.HasUnsavedChanges = true;
        _draggingNode = null;
    }
}
