// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/TankControl.cs
//  역할: 탱크 장비 아이콘 카드 (TankNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 수위 게이지 구현 — 바인딩된 Tag의 EngValue 를 0~100(%) 수위로 해석해
//         베이스의 LevelTrack/LevelFill(기본 Collapsed, 예비 확장 지점)을 표시하고
//         채움 비율을 반영한다.
//         ※ EngValue 가 0~100 범위의 % 값이 되도록 Collector 쪽 Tag 스케일 규칙을
//         맞추는 것을 권장(예: 리터 단위라면 스케일로 %로 변환).
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Layout;
using System.ComponentModel;
using System.Windows;

namespace IIoT.HMI.Views.DeviceControls;

/// <summary>탱크 아이콘 카드 (TankNode 전용) — Tag EngValue 기반 수위 게이지.</summary>
public sealed class TankControl : DeviceControlBase
{
    protected override void OnDeviceControlLoaded()
    {
        if (DataContext is AbstractLayoutNode node)
        {
            node.PropertyChanged += _OnNodePropertyChanged;
            _ApplyState(node);
        }

        // ★ LevelTrack 은 최초 레이아웃 패스에서 ActualWidth 가 0일 수 있으므로,
        //   실제 크기가 확정된 뒤(SizeChanged) 다시 한 번 반영해 정확한 채움 폭을 계산한다.
        LevelTrack.SizeChanged += (_, _) =>
        {
            if (DataContext is AbstractLayoutNode n) _ApplyState(n);
        };
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
        if (!node.IsBound || node.EngValue is not double v)
        {
            LevelTrack.Visibility = Visibility.Collapsed;
            return;
        }

        LevelTrack.Visibility = Visibility.Visible;

        var percent = Math.Clamp(v, 0, 100) / 100.0;
        LevelFill.Width = LevelTrack.ActualWidth * percent;
    }
}
