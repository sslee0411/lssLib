// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/GaugeControl.cs
//  역할: 범용 계기 게이지 아이콘 카드 (GaugeNode 전용)
//  HM-23: 신규 — TankControl의 다이얼(수위 전용, 단색 아치)과 달리 압력·온도·
//         유량 등 범용 계측값(0~100 스케일)을 위한 것으로, 다이얼 배경에
//         녹색(0~70%)/황색(70~90%)/적색(90~100%) 위험구간 밴드를 그려
//         바늘이 어느 구간에 있는지 한눈에 보이게 한다(속도계 대신 압력계/
//         온도계 스타일). 바늘 회전 애니메이션은 TankControl과 동일 원리.
//         실사용 요청(2026-07-20)으로 추가.
//  생성: 2026-07-20
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

/// <summary>범용 게이지 아이콘 카드 (GaugeNode 전용) — 위험구간 밴드가 있는 계기판 스타일 다이얼.</summary>
public sealed class GaugeControl : DeviceControlBase
{
    private const double Cx = 26, Cy = 30, R = 20;
    private const double MinAngle = -120, MaxAngle = 120;

    // 위험구간 경계(비율) — 0~0.7=정상(녹색), 0.7~0.9=주의(황색), 0.9~1.0=위험(적색)
    private static readonly (double from, double to, string brushKey)[] _zones =
    [
        (0.0, 0.7, "GreenBrush"),
        (0.7, 0.9, "YellowBrush"),
        (0.9, 1.0, "RedBrush"),
    ];

    private readonly RotateTransform _needleRotate = new();

    protected override void OnDeviceControlLoaded()
    {
        IconHost.Children.Clear();

        var dial = new Canvas { Width = 52, Height = 56 };

        // ── 위험구간 밴드(3개 호) — 값 구간별 색상으로 다이얼 배경 아치를 나눠 그린다 ──
        foreach (var (from, to, brushKey) in _zones)
        {
            var a1 = MinAngle + from * (MaxAngle - MinAngle);
            var a2 = MinAngle + to   * (MaxAngle - MinAngle);
            var figure = new PathFigure { StartPoint = _PointOnCircle(Cx, Cy, R, a1) };
            figure.Segments.Add(new ArcSegment(
                _PointOnCircle(Cx, Cy, R, a2), new Size(R, R), 0,
                isLargeArc: (to - from) > 0.5, SweepDirection.Clockwise, isStroked: true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            dial.Children.Add(new Path
            {
                Data               = geometry,
                Stroke             = ThemeResource.Find(brushKey),
                StrokeThickness    = 5,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap   = PenLineCap.Flat
            });
        }

        // ── 눈금(0/25/50/75/100%) ──
        foreach (var pct in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            var angle = MinAngle + pct * (MaxAngle - MinAngle);
            var inner = _PointOnCircle(Cx, Cy, R - 6, angle);
            var outer = _PointOnCircle(Cx, Cy, R + 1, angle);
            dial.Children.Add(new Line
            {
                X1 = inner.X, Y1 = inner.Y, X2 = outer.X, Y2 = outer.Y,
                Stroke          = ThemeResource.Find("Text2Brush"),
                StrokeThickness = 1.5
            });
        }

        // ── 바늘 ──
        var needle = new Line
        {
            X1 = Cx, Y1 = Cy, X2 = Cx, Y2 = Cy - (R - 3),
            Stroke             = ThemeResource.Find("TextBrush"),
            StrokeThickness    = 2.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap   = PenLineCap.Round
        };
        _needleRotate.CenterX = Cx;
        _needleRotate.CenterY = Cy;
        needle.RenderTransform = _needleRotate;
        dial.Children.Add(needle);

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
        var percent = node.IsBound && node.EngValue is double v
            ? Math.Clamp(v, 0, 100) / 100.0
            : 0.0;

        var targetAngle = MinAngle + percent * (MaxAngle - MinAngle);

        var anim = new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _needleRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private static Point _PointOnCircle(double cx, double cy, double r, double angleFromUpDeg)
    {
        var thetaRad = (90 - angleFromUpDeg) * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(thetaRad), cy - r * Math.Sin(thetaRad));
    }
}
