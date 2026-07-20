// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/MotorControl.cs
//  역할: 모터 장비 아이콘 카드 (MotorNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 회전 애니메이션 구현 — 바인딩된 Tag의 EngValue 절대값에 비례해
//         아이콘 글리프를 연속 회전시킨다(값이 클수록 빠르게 회전, RPM 개념).
//         Quality 가 Bad/Timeout/Disconnected 이거나 값이 0/미바인딩이면 정지.
//         DeviceControlBase.OnDeviceControlLoaded() 훅을 통해 IconText(베이스의
//         명명된 요소)에 RenderTransform 을 적용 — 카드 프레임은 그대로 재사용.
//  HM-20: 이모지 글리프 대신 실제 모터 형태(원형 하우징 + 3개 회전 날개)를
//         벡터 도형(Ellipse/Rectangle)으로 직접 그려 IconHost 에 넣는다.
//         회전 애니메이션 로직(_rotate/_ApplyState/_OnNodePropertyChanged)은
//         HM-06 그대로 — 대상만 IconText(TextBlock) → 날개 Canvas 로 바뀌었다.
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

/// <summary>모터 아이콘 카드 (MotorNode 전용) — Tag EngValue 비례 회전 애니메이션.</summary>
public sealed class MotorControl : DeviceControlBase
{
    private readonly RotateTransform _rotate = new();

    protected override void OnDeviceControlLoaded()
    {
        // ★ HM-20: 원형 하우징 + 3개 날개로 구성된 모터 벡터 아이콘을 직접 그린다
        IconHost.Children.Clear();

        var housing = new Ellipse
        {
            Width           = 44,
            Height          = 44,
            Fill            = ThemeResource.Find("SurfaceBrush"),
            Stroke          = ThemeResource.Find("AccBrush"),
            StrokeThickness = 3
        };
        IconHost.Children.Add(housing);

        // 날개 3개(120도 간격)를 담는 Canvas — 이 Canvas 전체를 회전시키면 팬(Fan)이 돈다
        var rotor = new Canvas
        {
            Width  = 44,
            Height = 44
        };
        for (var i = 0; i < 3; i++)
        {
            var blade = new Rectangle
            {
                Width                = 6,
                Height               = 17,
                RadiusX              = 3,
                RadiusY              = 3,
                Fill                 = ThemeResource.Find("AccBrush"),
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform      = new RotateTransform(i * 120)
            };
            Canvas.SetLeft(blade, 22 - 3); // 캔버스 중심(22,22)에서 위쪽으로 뻗도록 배치
            Canvas.SetTop(blade, 22 - 17);
            rotor.Children.Add(blade);
        }

        var hub = new Ellipse
        {
            Width  = 9,
            Height = 9,
            Fill   = ThemeResource.Find("TextBrush")
        };
        Canvas.SetLeft(hub, 22 - 4.5);
        Canvas.SetTop(hub, 22 - 4.5);
        rotor.Children.Add(hub);

        // ★ HM-06 애니메이션 대상 — 예전에는 IconText(TextBlock) 였고, 이제는 rotor(Canvas)
        rotor.RenderTransformOrigin = new Point(0.5, 0.5);
        rotor.RenderTransform       = _rotate;
        IconHost.Children.Add(rotor);

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

        // 값이 클수록 짧은 주기(빠른 회전) — 최고속은 약 0.3초/회전으로 캡
        var speed   = Math.Clamp(Math.Abs(node.EngValue!.Value), 1, 500);
        var seconds = Math.Max(0.3, 3.0 - speed / 200.0);

        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(seconds))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        _rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }
}
