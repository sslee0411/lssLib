// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/ValveControl.cs
//  역할: 밸브 장비 아이콘 카드 (ValveNode 전용)
//  HM-04: 신규(빈 상속 클래스 — 베이스 그대로 사용)
//  HM-06: 개폐 상태 색상 강조 구현 — 바인딩된 Tag의 EngValue > 0 이면 "열림"
//         (강조색), 그 외(0/미바인딩)면 "닫힘"(기본색)으로 아이콘 글리프 색상을 전환.
//  HM-20: 이모지 글리프 대신 실제 밸브 형태(배관 + 원형 밸브 바디 + 손잡이)를
//         벡터 도형으로 그려 IconHost 에 넣는다. 상태 표현을 텍스트 색상 대신
//         손잡이 색상 전환 + 회전(열림=배관과 나란히/닫힘=배관과 수직 — 실제
//         밸브 심볼의 표준 표기 방식)으로 확장했다. HM-06의 "EngValue>0 → 열림"
//         판정 기준 자체는 그대로 유지.
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

/// <summary>밸브 아이콘 카드 (ValveNode 전용) — Tag EngValue 기반 개폐 상태 표시.</summary>
public sealed class ValveControl : DeviceControlBase
{
    private readonly RotateTransform _handleRotate = new();
    private Line? _handle;

    protected override void OnDeviceControlLoaded()
    {
        // ★ HM-20: 배관 + 밸브 바디(원) + 손잡이(선)로 구성된 밸브 벡터 아이콘을 직접 그린다
        IconHost.Children.Clear();

        var frame = new Canvas { Width = 48, Height = 40 };
        var pipeBrush = ThemeResource.Find("Text2Brush");

        // 배관 (좌우 관통)
        var pipe = new Line { X1 = 4, Y1 = 20, X2 = 44, Y2 = 20, Stroke = pipeBrush, StrokeThickness = 5 };
        frame.Children.Add(pipe);

        // 밸브 바디
        var body = new Ellipse
        {
            Width           = 20,
            Height          = 20,
            Fill            = ThemeResource.Find("SurfaceBrush"),
            Stroke          = pipeBrush,
            StrokeThickness = 2.5
        };
        Canvas.SetLeft(body, 24 - 10);
        Canvas.SetTop(body, 20 - 10);
        frame.Children.Add(body);

        // 손잡이 — 열림(0°, 배관과 나란함)/닫힘(90°, 배관과 수직) 회전 + 색상으로 상태 표시
        _handle = new Line
        {
            X1                    = 24 - 12,
            Y1                    = 20,
            X2                    = 24 + 12,
            Y2                    = 20,
            StrokeThickness       = 4,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = _handleRotate
        };
        frame.Children.Add(_handle);

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
        if (_handle is null) return;

        var isOpen = node.IsBound && node.EngValue is double v && v > 0;

        _handle.Stroke = ThemeResource.Find(isOpen ? "GreenBrush" : "TextBrush");

        var targetAngle = isOpen ? 0.0 : 90.0;
        var anim = new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(200));
        _handleRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }
}
