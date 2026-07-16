// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/ConveyorControl.cs
//  역할: 컨베이어 장비 아이콘 카드 (ConveyorNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 흐름(이송) 효과 구현 — 바인딩된 Tag의 EngValue 절대값에 비례해
//         아이콘 글리프를 좌우로 왕복시켜 이송 흐름을 표현한다.
//         Quality 가 Bad/Timeout/Disconnected 이거나 값이 0/미바인딩이면 정지.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Layout;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IIoT.HMI.Views.DeviceControls;

/// <summary>컨베이어 아이콘 카드 (ConveyorNode 전용) — Tag EngValue 비례 흐름 애니메이션.</summary>
public sealed class ConveyorControl : DeviceControlBase
{
    private readonly TranslateTransform _shift = new();

    protected override void OnDeviceControlLoaded()
    {
        IconText.RenderTransform = _shift;

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
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = 0;
            return;
        }

        var speed   = Math.Clamp(Math.Abs(node.EngValue!.Value), 1, 500);
        var seconds = Math.Max(0.25, 1.6 - speed / 400.0);

        var anim = new DoubleAnimation
        {
            From = -6,
            To   = 6,
            Duration       = TimeSpan.FromSeconds(seconds),
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        _shift.BeginAnimation(TranslateTransform.XProperty, anim);
    }
}
