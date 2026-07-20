// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/ConveyorControl.cs
//  역할: 컨베이어 장비 아이콘 카드 (ConveyorNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 흐름(이송) 효과 구현 — 바인딩된 Tag의 EngValue 절대값에 비례해
//         아이콘 글리프를 좌우로 왕복시켜 이송 흐름을 표현한다.
//         Quality 가 Bad/Timeout/Disconnected 이거나 값이 0/미바인딩이면 정지.
//  HM-20: 이모지 글리프 대신 실제 컨베이어 형태(좌우 롤러 + 벨트 + 화물 표시)를
//         벡터 도형으로 직접 그려 IconHost 에 넣는다.
//  HM-20b(사용자 피드백): "실제로 컨베이어가 돌아가거나 회전하는" 느낌이 나도록
//         전면 재작업 — 화물 3개가 좌우로 왕복(oscillate)하던 방식 대신
//         ① 좌우 롤러가 실제로 계속 회전(스포크 표시로 회전이 보임)하고
//         ② 벨트 상/하 라인을 점선(StrokeDashArray)으로 그려 StrokeDashOffset 를
//            한쪽 방향으로 계속 흘려보내는(마칭 앤츠) 방식으로 "벨트가 흐르는"
//            느낌을 낸다. 정지 시에는 두 애니메이션 모두 멈추고 초기 상태로 복귀.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Converters;
using IIoT.HMI.Core.Layout;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace IIoT.HMI.Views.DeviceControls;

/// <summary>컨베이어 아이콘 카드 (ConveyorNode 전용) — 롤러 회전 + 벨트 스크롤 애니메이션.</summary>
public sealed class ConveyorControl : DeviceControlBase
{
    // 롤러 위치/반지름 (도형 좌표계 기준)
    private const double RollerR = 7;
    private const double LeftCx = 10, RightCx = 42, RollerCy = 28;
    private const double BeltTopY = 20, BeltBottomY = 36;

    private readonly RotateTransform _rollerLeftRotate  = new();
    private readonly RotateTransform _rollerRightRotate = new();
    private Line? _beltTop;
    private Line? _beltBottom;

    protected override void OnDeviceControlLoaded()
    {
        IconHost.Children.Clear();

        var frame = new Canvas { Width = 52, Height = 48 };
        var frameBrush = ThemeResource.Find("Text2Brush");
        var beltBrush  = ThemeResource.Find("AccBrush");

        // ── 벨트 상/하 라인 — 점선 + StrokeDashOffset 애니메이션으로 "흐르는" 느낌 ──
        _beltTop = new Line
        {
            X1 = LeftCx, Y1 = BeltTopY, X2 = RightCx, Y2 = BeltTopY,
            Stroke = beltBrush, StrokeThickness = 3,
            StrokeDashArray = new DoubleCollection { 3, 2 }
        };
        _beltBottom = new Line
        {
            X1 = LeftCx, Y1 = BeltBottomY, X2 = RightCx, Y2 = BeltBottomY,
            Stroke = beltBrush, StrokeThickness = 3,
            StrokeDashArray = new DoubleCollection { 3, 2 }
        };
        frame.Children.Add(_beltTop);
        frame.Children.Add(_beltBottom);

        // ── 좌/우 롤러 — 원 + 스포크(회전이 눈에 보이도록) ──
        _AddRoller(frame, LeftCx, RollerCy, frameBrush, _rollerLeftRotate);
        _AddRoller(frame, RightCx, RollerCy, frameBrush, _rollerRightRotate);

        IconHost.Children.Add(frame);

        if (DataContext is AbstractLayoutNode node)
        {
            node.PropertyChanged += _OnNodePropertyChanged;
            _ApplyState(node);
        }
    }

    /// <summary>롤러 1개(원+스포크 2개)를 만들어 캔버스에 추가하고, 스포크 그룹에
    /// 회전 트랜스폼을 걸어 둔다(실제 애니메이션 시작/정지는 _ApplyState 가 담당).</summary>
    private static void _AddRoller(Canvas parent, double cx, double cy, Brush brush, RotateTransform rotate)
    {
        var body = new Ellipse
        {
            Width = RollerR * 2, Height = RollerR * 2,
            Fill = ThemeResource.Find("SurfaceBrush"),
            Stroke = brush, StrokeThickness = 2
        };
        Canvas.SetLeft(body, cx - RollerR);
        Canvas.SetTop(body, cy - RollerR);
        parent.Children.Add(body);

        // 스포크 2개(십자) — 이 그룹이 회전하면 롤러가 도는 것처럼 보인다
        var spokes = new Canvas { Width = RollerR * 2, Height = RollerR * 2 };
        spokes.Children.Add(new Line { X1 = 0, Y1 = RollerR, X2 = RollerR * 2, Y2 = RollerR, Stroke = brush, StrokeThickness = 1.5 });
        spokes.Children.Add(new Line { X1 = RollerR, Y1 = 0, X2 = RollerR, Y2 = RollerR * 2, Stroke = brush, StrokeThickness = 1.5 });
        Canvas.SetLeft(spokes, cx - RollerR);
        Canvas.SetTop(spokes, cy - RollerR);
        rotate.CenterX = RollerR;
        rotate.CenterY = RollerR;
        spokes.RenderTransform = rotate;
        parent.Children.Add(spokes);
    }

    private void _OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not AbstractLayoutNode node) return;

        if (e.PropertyName is null
            or nameof(AbstractLayoutNode.EngValue)
            or nameof(AbstractLayoutNode.ValueQuality)
            or nameof(AbstractLayoutNode.IsBound))
        {
            _ApplyState(node);
        }
    }

    private void _ApplyState(AbstractLayoutNode node)
    {
        if (_beltTop is null || _beltBottom is null) return;

        var running = node.IsBound
            && node.ValueQuality is "" or "Good"
            && node.EngValue is double v && Math.Abs(v) > 0.01;

        if (!running)
        {
            _rollerLeftRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            _rollerRightRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            _rollerLeftRotate.Angle  = 0;
            _rollerRightRotate.Angle = 0;

            _beltTop.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
            _beltBottom.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
            _beltTop.StrokeDashOffset    = 0;
            _beltBottom.StrokeDashOffset = 0;
            return;
        }

        // 값이 클수록 짧은 주기(빠른 회전/스크롤) — 최고속은 약 0.4초로 캡
        var speed   = Math.Clamp(Math.Abs(node.EngValue!.Value), 1, 500);
        var seconds = Math.Max(0.4, 3.0 - speed / 180.0);

        var rotateAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(seconds))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        // 두 롤러가 같은 방향·같은 속도로 함께 돌도록 동일한 애니메이션 파라미터를 사용
        _rollerLeftRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
        _rollerRightRotate.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(seconds)) { RepeatBehavior = RepeatBehavior.Forever });

        // 벨트 점선 패턴 길이(3+2=5)만큼 한쪽으로 계속 흘려보내 "이동" 느낌을 낸다
        // (AutoReverse 없음 — 왕복이 아니라 한 방향으로 계속 흐르는 컨베이어 벨트 표현)
        var dashAnim = new DoubleAnimation(0, -5, TimeSpan.FromSeconds(seconds / 3.0))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        _beltTop.BeginAnimation(Shape.StrokeDashOffsetProperty, dashAnim);
        _beltBottom.BeginAnimation(Shape.StrokeDashOffsetProperty,
            new DoubleAnimation(0, -5, TimeSpan.FromSeconds(seconds / 3.0)) { RepeatBehavior = RepeatBehavior.Forever });
    }
}
