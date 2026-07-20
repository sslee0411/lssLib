// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/TankControl.cs
//  역할: 탱크 장비 아이콘 카드 (TankNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 수위 게이지 구현 — 바인딩된 Tag의 EngValue 를 0~100(%) 수위로 해석해
//         베이스의 LevelTrack/LevelFill(기본 Collapsed, 예비 확장 지점)을 표시하고
//         채움 비율을 반영한다.
//  HM-20: 이모지 글리프 대신 실제 탱크 형태(원통 몸통 + 위쪽 타원 뚜껑)를 정적
//         벡터 도형으로 그려 IconHost 에 넣음(수위 게이지 로직은 그대로 유지).
//  HM-20b(사용자 피드백): 직사각형 막대 게이지(LevelTrack/LevelFill) 대신
//         "차량 속도계"처럼 눈금(0/25/50/75/100%)과 회전 바늘이 있는 다이얼
//         게이지로 전면 교체. 탱크 실린더 외형은 유지하되, 수위 표시는
//         LevelTrack/LevelFill 대신 바늘 회전각으로 표현한다 — 이 컨트롤만
//         LevelTrack/LevelFill 사용을 중단(베이스 요소 자체는 그대로 유지,
//         향후 다른 장비가 막대형 게이지가 필요하면 계속 재사용 가능).
//         바늘은 값이 바뀔 때마다 400ms 애니메이션으로 부드럽게 회전한다.
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

/// <summary>탱크 아이콘 카드 (TankNode 전용) — Tag EngValue 기반 속도계 스타일 수위 게이지.</summary>
public sealed class TankControl : DeviceControlBase
{
    // 게이지 중심/반지름(도형 좌표계 기준)
    private const double Cx = 26, Cy = 30, R = 20;

    /// <summary>0%/100% 바늘 각도(위쪽=0°, 시계방향 +) — 속도계처럼 240도 스윕</summary>
    private const double MinAngle = -120, MaxAngle = 120;

    private readonly RotateTransform _needleRotate = new();

    protected override void OnDeviceControlLoaded()
    {
        IconHost.Children.Clear();

        var dial = new Canvas { Width = 52, Height = 56 };

        // ── 다이얼(원호) 배경 — 눈금이 있는 240도 아치 ──
        var dialStart = _PointOnCircle(Cx, Cy, R, MinAngle);
        var dialEnd   = _PointOnCircle(Cx, Cy, R, MaxAngle);
        var dialFigure = new PathFigure { StartPoint = dialStart };
        dialFigure.Segments.Add(new ArcSegment(
            dialEnd, new Size(R, R), 0, isLargeArc: true,
            SweepDirection.Clockwise, isStroked: true));
        var dialGeometry = new PathGeometry();
        dialGeometry.Figures.Add(dialFigure);
        dial.Children.Add(new Path
        {
            Data            = dialGeometry,
            Stroke          = ThemeResource.Find("BorderBrush"),
            StrokeThickness = 4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap   = PenLineCap.Round
        });

        // ── 눈금 (0/25/50/75/100%) ──
        foreach (var pct in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            var angle = MinAngle + pct * (MaxAngle - MinAngle);
            var inner = _PointOnCircle(Cx, Cy, R - 5, angle);
            var outer = _PointOnCircle(Cx, Cy, R + 2, angle);
            dial.Children.Add(new Line
            {
                X1 = inner.X, Y1 = inner.Y,
                X2 = outer.X, Y2 = outer.Y,
                Stroke          = ThemeResource.Find("Text2Brush"),
                StrokeThickness = 2
            });
        }

        // ── 바늘(needle) — 기본 위치는 정면 위(0%), RotateTransform 로 회전 ──
        // ★ Line 은 Canvas.Left/Top 을 지정하지 않았으므로 X1/Y1/X2/Y2 좌표가 곧 Canvas
        //   좌표계와 일치한다 — 그래서 RenderTransformOrigin(상대 0~1 좌표) 대신
        //   RotateTransform.CenterX/CenterY 에 절대좌표(Cx,Cy)를 직접 지정해 바늘의
        //   뿌리(축)를 정확히 그 점에 고정한다(둘을 함께 쓰면 이중 보정되어 어긋난다).
        var needle = new Line
        {
            X1 = Cx, Y1 = Cy,
            X2 = Cx, Y2 = Cy - (R - 2),
            Stroke             = ThemeResource.Find("RedBrush"),
            StrokeThickness    = 2.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap   = PenLineCap.Round
        };
        _needleRotate.CenterX = Cx;
        _needleRotate.CenterY = Cy;
        needle.RenderTransform = _needleRotate;
        dial.Children.Add(needle);

        // 중심 허브
        var hub = new Ellipse { Width = 6, Height = 6, Fill = ThemeResource.Find("TextBrush") };
        Canvas.SetLeft(hub, Cx - 3);
        Canvas.SetTop(hub, Cy - 3);
        dial.Children.Add(hub);

        IconHost.Children.Add(dial);

        if (DataContext is AbstractLayoutNode node)
        {
            node.PropertyChanged += _OnNodePropertyChanged;
            _ApplyState(node);
        }
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
        // 미바인딩/값없음이면 0% 위치로 되돌린다(게이지 자체는 항상 보이는 정적 아이콘 요소이므로
        // LevelTrack 처럼 Visibility 토글은 하지 않는다 — 항상 다이얼 형태를 유지)
        var percent = node.IsBound && node.EngValue is double v
            ? Math.Clamp(v, 0, 100) / 100.0
            : 0.0;

        var targetAngle = MinAngle + percent * (MaxAngle - MinAngle);

        // ★ HM-20b: 값이 바뀔 때마다 바늘이 스냅되지 않고 부드럽게 돌아가도록 애니메이션
        var anim = new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _needleRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    /// <summary>
    /// 중심(cx,cy) 기준 반지름 r, "위쪽=0°·시계방향 +" 각도(angleFromUpDeg)에 대응하는
    /// 화면 좌표를 계산한다(속도계 다이얼/눈금/바늘 좌표 계산에 공통 사용).
    /// </summary>
    private static Point _PointOnCircle(double cx, double cy, double r, double angleFromUpDeg)
    {
        var thetaRad = (90 - angleFromUpDeg) * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(thetaRad), cy - r * Math.Sin(thetaRad));
    }
}
