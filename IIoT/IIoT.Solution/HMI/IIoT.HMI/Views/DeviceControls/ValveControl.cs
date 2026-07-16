// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/ValveControl.cs
//  역할: 밸브 장비 아이콘 카드 (ValveNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 개폐 상태 색상 강조 구현 — 바인딩된 Tag의 EngValue > 0 이면 "열림"
//         (강조색), 그 외(0/미바인딩)면 "닫힘"(기본색)으로 아이콘 글리프 색상을 전환.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Converters;
using IIoT.HMI.Core.Layout;
using System.ComponentModel;

namespace IIoT.HMI.Views.DeviceControls;

/// <summary>밸브 아이콘 카드 (ValveNode 전용) — Tag EngValue 기반 개폐 상태 색상 강조.</summary>
public sealed class ValveControl : DeviceControlBase
{
    protected override void OnDeviceControlLoaded()
    {
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
        var isOpen = node.IsBound && node.EngValue is double v && v > 0;
        IconText.Foreground = ThemeResource.Find(isOpen ? "GreenBrush" : "TextBrush");
    }
}
