// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/SignalTowerControl.cs
//  역할: 적층 신호등(시그널 타워) 아이콘 카드 (SignalTowerNode 전용)
//  HM-23: 신규 — 설비 자체가 아니라 "설비 상태를 알리는 표시기" 역할.
//         바인딩된 Tag의 EngValue 를 상태 코드로 해석한다:
//           0(또는 미바인딩/Bad Quality) → 전체 소등
//           1 → 녹색 점등(정상운전)
//           2 → 황색 점멸(경고)
//           3 이상 → 적색 점멸(고장/비상)
//         실제 HMI에서 상태등 3색 적층 표시기는 가장 흔히 쓰이는 요소 중 하나라
//         사용자 요청(2026-07-20)으로 추가.
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

/// <summary>적층 신호등 아이콘 카드 (SignalTowerNode 전용) — Tag EngValue 상태 코드 기반 점등/점멸.</summary>
public sealed class SignalTowerControl : DeviceControlBase
{
    private Ellipse? _red;
    private Ellipse? _yellow;
    private Ellipse? _green;

    protected override void OnDeviceControlLoaded()
    {
        IconHost.Children.Clear();

        var frame = new Canvas { Width = 32, Height = 52 };

        _red    = _CreateLamp(frame, 8,  ThemeResource.Find("RedBrush"));
        _yellow = _CreateLamp(frame, 24, ThemeResource.Find("YellowBrush"));
        _green  = _CreateLamp(frame, 40, ThemeResource.Find("GreenBrush"));

        // 받침대(폴대)
        var pole = new Rectangle
        {
            Width  = 6,
            Height = 8,
            Fill   = ThemeResource.Find("Text2Brush")
        };
        Canvas.SetLeft(pole, 13);
        Canvas.SetTop(pole, 48);
        frame.Children.Add(pole);

        IconHost.Children.Add(frame);

        if (DataContext is AbstractLayoutNode node)
        {
            node.PropertyChanged += _OnNodePropertyChanged;
            _ApplyState(node);
        }
    }

    /// <summary>램프 1개(원)를 만들어 캔버스에 배치. 기본은 소등(회색 반투명) 상태.</summary>
    private static Ellipse _CreateLamp(Canvas parent, double top, Brush litColor)
    {
        var lamp = new Ellipse
        {
            Width           = 22,
            Height          = 14,
            Fill            = litColor,
            Stroke          = ThemeResource.Find("BorderBrush"),
            StrokeThickness = 1,
            Opacity         = 0.25   // 소등 기본값
        };
        Canvas.SetLeft(lamp, 5);
        Canvas.SetTop(lamp, top);
        parent.Children.Add(lamp);
        return lamp;
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
        if (_red is null || _yellow is null || _green is null) return;

        // 전체 소등 + 점멸 애니메이션 정지(기본 상태로 초기화)
        void Reset(Ellipse lamp)
        {
            lamp.BeginAnimation(UIElement.OpacityProperty, null);
            lamp.Opacity = 0.25;
        }
        Reset(_red);
        Reset(_yellow);
        Reset(_green);

        var ok = node.IsBound && node.ValueQuality is "" or "Good" && node.EngValue is double;
        if (!ok) return;

        var state = (int)Math.Round(node.EngValue!.Value);

        switch (state)
        {
            case <= 0:
                // 전체 소등 유지(위에서 이미 리셋됨)
                break;
            case 1:
                _green.Opacity = 1.0;   // 정상운전 — 점등만, 점멸 없음
                break;
            case 2:
                _yellow.Opacity = 1.0;
                _Blink(_yellow);
                break;
            default: // 3 이상
                _red.Opacity = 1.0;
                _Blink(_red);
                break;
        }
    }

    /// <summary>주어진 램프를 1.0↔0.35 사이로 점멸시킨다(고장/경고 강조).</summary>
    private static void _Blink(Ellipse lamp)
    {
        var anim = new DoubleAnimation(1.0, 0.35, TimeSpan.FromMilliseconds(450))
        {
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        lamp.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}
