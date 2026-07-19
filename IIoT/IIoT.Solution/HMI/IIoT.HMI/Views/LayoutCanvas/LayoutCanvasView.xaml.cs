// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/LayoutCanvas/LayoutCanvasView.xaml.cs
//  역할: 레이아웃 캔버스 인터랙션 코드비하인드 — 아이콘 드래그·줌·패닝
//        (IIoT.Studio Views/Canvas/CanvasView.xaml.cs 이식.
//         포트 드래그(§4~6의 _isDraggingPort 분기)와 PortsLayer 관리(§8)는
//         전부 제외 — HM-03 캔버스에는 포트/연결선 개념이 없다)
//  HM-03: 신규
//  HM-07: Loaded 시 _vm.InitializeAsync() 호출 추가 — hmi-layout.json 로드 및
//         마지막 활성 화면 복원 (CollectorManageView.Loaded 패턴과 동일)
//  HM-09: OnCanvasMouseDown 에 더블클릭(ClickCount==2) 분기 추가 — Tag 바인딩된
//         카드를 더블클릭하면 ForceWriteDialog 를 띄워 원격 강제쓰기를 수행한다
//         (IIoT.Collector StatusView.xaml.cs ForceWriteButton_Click 과 동일 패턴,
//         결과는 MessageBox 로 표시). 드래그 시작 로직보다 먼저 검사해 더블클릭
//         시에는 드래그가 시작되지 않도록 분리했다.
//  HM-10: PageTab_MouseLeftButtonDown/PageTabNameBox_LostFocus/KeyDown 추가 —
//         화면(페이지) 탭 클릭=SelectPage(화면 전환), 더블클릭=이름 편집 모드
//         (인라인 TextBox 로 전환 후 Dispatcher.BeginInvoke 로 포커스 지연 부여).
//  HM-12: _OpenForceWriteDialogAsync 에 화면 잠금 체크 추가(_vm.IsForceWriteLocked
//         이면 안내 메시지만 표시하고 다이얼로그를 열지 않음) + ForceWriteDialog
//         생성 시 node.HasActiveAlarm/AlarmMessage 전달(활성 알람 경고) +
//         dialog.PrefillApiKey(_vm.GetCachedApiKey(...)) 로 세션 캐시 API Key 적용.
//  HM-17: OnCanvasMouseDown 에 우클릭(MouseButton.Right) 분기 추가 — Tag
//         바인딩된 카드를 우클릭하면 TrendWindow(실시간 트렌드 창)를 비모달로
//         띄운다. 여러 개를 동시에 열 수 있다(노드별 독립 창).
//  HM-18: CaptureButton_Click 추가 — 현재 캔버스 화면(CanvasBorder)을
//         RenderTargetBitmap 으로 렌더링해 PNG 파일로 저장한다(WPF 내장 기능만
//         사용, 새 NuGet 의존성 없음). 사용자 확인: PDF 리포트는 범위 밖.
//  HM-19: SecondaryWindowButton_Click 추가 — 같은 _vm(LayoutCanvasViewModel)을
//         공유하는 두 번째 LayoutCanvasView 를 새로 생성해 SecondaryDisplayWindow
//         (독립 최상위 창, Owner 없음)로 띄운다. 다른 모니터로 옮겨 최대화하면
//         메인 창과 동일한 레이아웃이 실시간으로 함께 갱신된다.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Layout;
using IIoT.HMI.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        if (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Right) return;

        var posOnCanvas = e.GetPosition(RootCanvas);
        var nodeHit     = VisualTreeHelper.HitTest(NodesLayer, posOnCanvas)?.VisualHit;
        var node        = _FindNodeFromVisual(nodeHit);

        // ★ HM-17: 우클릭 → 실시간 트렌드 창 (Tag 바인딩된 카드만, 비모달·다중 오픈 가능)
        if (e.ChangedButton == MouseButton.Right)
        {
            if (node is not null && node.IsBound)
            {
                _vm.SelectNode(node);
                e.Handled = true;
                _OpenTrendWindow(node);
            }
            return;
        }

        // ★ HM-09: 더블클릭 → ForceWrite 다이얼로그 (드래그 시작 없이 여기서 처리 종료)
        if (node is not null && e.ClickCount == 2)
        {
            _vm.SelectNode(node);
            e.Handled = true;
            _ = _OpenForceWriteDialogAsync(node);
            return;
        }

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

    // §3-1 ─ HM-09: ForceWrite 다이얼로그 ────────────────────

    /// <summary>
    /// 더블클릭된 노드가 Tag 바인딩되어 있으면 ForceWriteDialog 를 띄우고,
    /// 확인 시 ViewModel.ForceWriteAsync() 를 호출해 Collector 로 원격 강제쓰기를
    /// 요청한다. 결과(성공/실패)는 MessageBox 로 표시한다
    /// (IIoT.Collector Views/Status/StatusView.xaml.cs ForceWriteButton_Click 과 동일 패턴).
    /// </summary>
    private async Task _OpenForceWriteDialogAsync(AbstractLayoutNode node)
    {
        if (_vm is null) return;

        // ★ HM-12: 화면 잠금 모드 — 운영자 실수로 인한 오조작 방지
        if (_vm.IsForceWriteLocked)
        {
            MessageBox.Show(Window.GetWindow(this),
                "화면이 잠금 모드입니다. 툴바의 🔒 버튼으로 잠금을 해제한 뒤 다시 시도하세요.",
                "잠금 모드", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!node.IsBound)
        {
            MessageBox.Show(Window.GetWindow(this),
                "이 카드는 아직 Tag가 바인딩되어 있지 않습니다.\n먼저 속성 패널에서 Tag를 바인딩하세요.",
                "안내", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // ★ HM-12: 활성 알람 중 강제쓰기 경고(hasActiveAlarm/alarmMessage) +
        //   세션 캐시된 API Key 미리 채우기(PrefillApiKey)
        var dialog = new ForceWriteDialog(
            node.BoundTagName, $"{node.Label} · PlcId: {node.BoundPlcId}",
            node.HasActiveAlarm, node.AlarmMessage)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.PrefillApiKey(_vm.GetCachedApiKey(node.BoundCollectorId));

        if (dialog.ShowDialog() != true || dialog.ResultValue is null)
            return;

        var result = await _vm.ForceWriteAsync(node, dialog.ResultValue, dialog.ResultApiKey);

        MessageBox.Show(
            Window.GetWindow(this),
            result.IsSuccess
                ? $"'{node.BoundTagName}' 에 값 쓰기 성공."
                : $"쓰기 실패: {result.Error}",
            result.IsSuccess ? "완료" : "오류",
            MessageBoxButton.OK,
            result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    // §3-1a ─ HM-17: 실시간 트렌드 창 ─────────────────────────

    /// <summary>
    /// 우클릭된 노드의 실시간 트렌드 창을 새로 열어 표시한다(비모달, 여러 개 동시
    /// 오픈 가능 — 노드별로 독립된 창에서 각자 값을 구독/누적한다).
    /// ★ 과거(창 열기 이전) 이력은 조회하지 않는다 — Collector 의 시계열 저장소에
    /// 조회(읽기) API가 없어 범위 밖으로 확정(사용자 확인, 2026-07-19).
    /// </summary>
    private void _OpenTrendWindow(AbstractLayoutNode node)
    {
        var window = new TrendWindow(node)
        {
            Owner = Window.GetWindow(this)
        };
        window.Show();
    }

    // §3-1b ─ HM-18: 화면 캡처(PNG) ────────────────────────────

    /// <summary>
    /// 현재 캔버스 화면(CanvasBorder — 현재 줌/팬 상태 그대로)을 PNG 이미지로
    /// 저장한다. WPF 내장 RenderTargetBitmap/PngBitmapEncoder 만 사용하며
    /// 새 NuGet 의존성은 없다. ★ 사용자 확인(2026-07-19): PDF 리포트(제목/
    /// 타임스탬프 등을 포함한 문서 형태)는 범위 밖 — PNG 캡처만 제공한다.
    /// </summary>
    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        var width  = (int)CanvasBorder.ActualWidth;
        var height = (int)CanvasBorder.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var pageName = _vm?.ActivePage?.Name ?? "레이아웃";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "PNG 이미지 (*.png)|*.png",
            FileName = $"hmi-capture-{pageName}-{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(CanvasBorder);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            MessageBox.Show(Window.GetWindow(this),
                $"화면 캡처 저장 완료:\n{dialog.FileName}",
                "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"캡처 저장 실패: {ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // §3-1c ─ HM-19: 다중 모니터 보조 화면 ─────────────────────

    /// <summary>
    /// 같은 LayoutCanvasViewModel 을 공유하는 두 번째 LayoutCanvasView 를 생성해
    /// 독립 창(SecondaryDisplayWindow)으로 띄운다. Owner 를 지정하지 않으므로
    /// 다른 모니터로 자유롭게 옮기고 그 모니터에서 최대화할 수 있다(단, App.xaml.cs
    /// 의 ShutdownMode=OnMainWindowClose 설정에 따라 메인 창을 닫으면 이 창도
    /// 함께 닫힌다). 여러 번 눌러 여러 개의 보조 창을 동시에 열 수도 있다.
    /// </summary>
    private void SecondaryWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;

        var mirrorView = new LayoutCanvasView(_vm);
        var window = new SecondaryDisplayWindow(mirrorView);
        window.Show();
    }

    // §3-2 ─ HM-10: 화면(페이지) 탭 ───────────────────────────

    /// <summary>
    /// 화면 탭 클릭 처리. 더블클릭(ClickCount==2)이면 이름 편집 모드로 전환하고
    /// 인라인 TextBox 에 포커스를 준다(레이아웃 갱신 후 포커스가 걸리도록
    /// Dispatcher.BeginInvoke 로 지연). 단일 클릭이면 SelectPage() 로 화면을 전환한다
    /// (SelectNode() 와 동일 패턴 — 커맨드가 아닌 공개 메서드 직접 호출).
    /// </summary>
    private void PageTab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not LayoutPageViewModel page) return;

        if (e.ClickCount == 2)
        {
            page.IsEditingName = true;
            e.Handled = true;

            Dispatcher.BeginInvoke(() =>
            {
                if (fe.FindName("NameBox") is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            });
            return;
        }

        _vm?.SelectPage(page);
    }

    /// <summary>이름 편집 TextBox 가 포커스를 잃으면 편집 모드를 종료(확정)한다.</summary>
    private void PageTabNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is LayoutPageViewModel page)
            page.IsEditingName = false;
    }

    /// <summary>Enter=확정(포커스 해제→LostFocus 유도), Esc=취소(편집 모드만 종료).</summary>
    private void PageTabNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && sender is FrameworkElement fe && fe.DataContext is LayoutPageViewModel page)
        {
            page.IsEditingName = false;
            e.Handled = true;
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
