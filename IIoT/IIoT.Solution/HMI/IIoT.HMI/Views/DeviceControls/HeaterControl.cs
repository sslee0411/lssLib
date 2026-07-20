// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/HeaterControl.cs
//  역할: 히터(발열체) 아이콘 카드 (HeaterNode 전용)
//  HM-23: 신규 — 바인딩된 Tag의 EngValue 를 온도로 해석해 발열선(지그재그) 색상을
//         3단계(꺼짐=회색, 가열중=황색, 고온=적색)로 전환하고, 고온 구간에서는
//         은은한 발광 펄스 애니메이션(Opacity 진동)으로 "가열 중"임을 강조한다.
//         ValveControl/MotorControl과 동일하게 임계값 기반 상태 판정 관례를 따른다.
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

/// <summary>히터 아이콘 카드 (HeaterNode 전용) — Tag EngValue(온도) 3단계 색상 + 고온 발광 펄스.</summary>
public sealed class HeaterControl : DeviceControlBase
{
    private Path? _coil;
    private Rectangle? _frameRect;

    protected override void OnDeviceControlLoaded()
    {
        IconHost.Children.Clear();

        var frame = new Canvas { Width = 52, Height = 44 };

        // 히터 프레임(외곽 테두리)
        _frameRect = new Rectangle
        {
            Width           = 44,
            Height          = 32,
            RadiusX         = 4,
            RadiusY         = 4,
            Fill            = ThemeResource.Find("SurfaceBrush"),
            Stroke          = ThemeResource.Find("BorderBrush"),
            StrokeThickness = 2
        };
        Canvas.SetLeft(_frameRect, 4);
        Canvas.SetTop(_frameRect, 6);
        frame.Children.Add(_frameRect);

        // 발열선(지그재그 코일) — 온도에 따라 색상 전환 대상
        var figure = new PathFigure { StartPoint = new Point(10, 20) };
        var pts = new[] { (18, 12), (26, 28), (34, 12), (42, 20) };
        foreach (var (x, y) in pts)
            figure.Segments.Add(new LineSegment(new Point(x, y), true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        _coil = new Path
        {
            Data               = geometry,
            Stroke             = ThemeResource.Find("Text2Brush"),
            StrokeThickness    = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap   = PenLineCap.Round,
            StrokeLineJoin     = PenLineJoin.Round
        };
        frame.Children.Add(_coil);

        IconHost.Children.Add(frame);

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
        if (_coil is null) return;

        // 펄스 애니메이션은 항상 먼저 정지 후 필요할 때만 재시작(중복 방지)
        _coil.BeginAnimation(UIElement.OpacityProperty, null);
        _coil.Opacity = 1.0;

        var ok = node.IsBound && node.ValueQuality is "" or "Good" && node.EngValue is double;
        var temp = ok ? Math.Abs(node.EngValue!.Value) : 0.0;

        if (!ok || temp < 1.0)
        {
            _coil.Stroke = ThemeResource.Find("Text2Brush");   // 꺼짐(상온)
            return;
        }

        if (temp < 60.0)
        {
            _coil.Stroke = ThemeResource.Find("YellowBrush");  // 가열 중
            return;
        }

        // 고온 — 적색 + 은은한 발광 펄스
        _coil.Stroke = ThemeResource.Find("RedBrush");
        var anim = new DoubleAnimation(1.0, 0.5, TimeSpan.FromMilliseconds(600))
        {
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        _coil.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}
