// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/MotorControl.cs
//  역할: 모터 장비 아이콘 카드 (MotorNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 회전 애니메이션 구현 — 바인딩된 Tag의 EngValue 절대값에 비례해
//         아이콘 글리프를 연속 회전시킨다(값이 클수록 빠르게 회전, RPM 개념).
//         Quality 가 Bad/Timeout/Disconnected 이거나 값이 0/미바인딩이면 정지.
//         DeviceControlBase.OnDeviceControlLoaded() 훅을 통해 IconText(베이스의
//         명명된 요소)에 RenderTransform 을 적용 — 카드 프레임은 그대로 재사용.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Layout;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IIoT.HMI.Views.DeviceControls;

/// <summary>모터 아이콘 카드 (MotorNode 전용) — Tag EngValue 비례 회전 애니메이션.</summary>
public sealed class MotorControl : DeviceControlBase
{
    private readonly RotateTransform _rotate = new();

    protected override void OnDeviceControlLoaded()
    {
        IconText.RenderTransform = _rotate;

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
