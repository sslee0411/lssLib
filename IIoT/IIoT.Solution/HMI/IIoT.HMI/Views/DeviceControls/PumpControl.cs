// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/PumpControl.cs
//  역할: 펌프 장비 아이콘 카드 (PumpNode 전용)
//  HM-23: 신규 — MotorControl과 마찬가지로 바인딩된 Tag의 EngValue 절대값에
//         비례해 회전(RPM 개념)하지만, 하우징(볼류트 케이싱) + 토출배관 스텁을
//         함께 그려 "유체를 미는 회전기기"라는 형태로 모터와 구분한다.
//         정지/회전 판정 기준(EngValue 절대값 > 0.01, Quality Good)은
//         MotorControl과 동일 — 실사용 요청(2026-07-20)으로 추가.
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

/// <summary>펌프 아이콘 카드 (PumpNode 전용) — Tag EngValue 비례 임펠러 회전 애니메이션.</summary>
public sealed class PumpControl : DeviceControlBase
{
    private readonly RotateTransform _rotate = new();

    protected override void OnDeviceControlLoaded()
    {
        IconHost.Children.Clear();

        var frame = new Canvas { Width = 52, Height = 48 };

        // 토출배관 스텁 — 하우징 우측 위로 뻗는 짧은 배관(펌프의 특징적 형태)
        var outlet = new Rectangle
        {
            Width  = 14,
            Height = 8,
            Fill   = ThemeResource.Find("Text2Brush")
        };
        Canvas.SetLeft(outlet, 34);
        Canvas.SetTop(outlet, 6);
        frame.Children.Add(outlet);

        // 볼류트 케이싱(하우징) — 모터보다 약간 작게, 하단에 배치해 배관과 어울리게
        var housing = new Ellipse
        {
            Width           = 40,
            Height          = 40,
            Fill            = ThemeResource.Find("SurfaceBrush"),
            Stroke          = ThemeResource.Find("AccBrush"),
            StrokeThickness = 3
        };
        Canvas.SetLeft(housing, 6);
        Canvas.SetTop(housing, 8);
        frame.Children.Add(housing);

        // 임펠러(회전 날개 4개) — housing 중심(26,28)에서 회전
        var impeller = new Canvas { Width = 40, Height = 40 };
        for (var i = 0; i < 4; i++)
        {
            var blade = new Rectangle
            {
                Width                 = 5,
                Height                = 14,
                RadiusX               = 2,
                RadiusY               = 2,
                Fill                  = ThemeResource.Find("AccBrush"),
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform       = new RotateTransform(i * 90)
            };
            Canvas.SetLeft(blade, 20 - 2.5);
            Canvas.SetTop(blade, 20 - 14);
            impeller.Children.Add(blade);
        }
        var hub = new Ellipse { Width = 8, Height = 8, Fill = ThemeResource.Find("TextBrush") };
        Canvas.SetLeft(hub, 20 - 4);
        Canvas.SetTop(hub, 20 - 4);
        impeller.Children.Add(hub);

        Canvas.SetLeft(impeller, 6);
        Canvas.SetTop(impeller, 8);
        impeller.RenderTransformOrigin = new Point(0.5, 0.5);
        impeller.RenderTransform       = _rotate;
        frame.Children.Add(impeller);

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
        var running = node.IsBound
            && node.ValueQuality is "" or "Good"
            && node.EngValue is double v && Math.Abs(v) > 0.01;

        if (!running)
        {
            _rotate.BeginAnimation(RotateTransform.AngleProperty, null);
            _rotate.Angle = 0;
            return;
        }

        var speed   = Math.Clamp(Math.Abs(node.EngValue!.Value), 1, 500);
        var seconds = Math.Max(0.3, 3.0 - speed / 200.0);

        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(seconds))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        _rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }
}
