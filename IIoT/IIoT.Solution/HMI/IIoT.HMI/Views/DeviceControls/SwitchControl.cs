// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/SwitchControl.cs
//  역할: 스위치·디지털 상태 표시 아이콘 카드 (SwitchNode 전용)
//  HM-23: 신규 — 수동 스위치·리밋 스위치·도어 인터록 등 On/Off 디지털 신호를
//         토글 스위치(알약형 트랙 + 슬라이딩 원) 형태로 표시한다.
//         EngValue>0 이면 On(트랙 강조색, 원이 우측) 그 외에는 Off(회색, 원이
//         좌측) — ValveControl의 개폐 판정 기준과 동일한 관례를 재사용.
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

/// <summary>스위치 아이콘 카드 (SwitchNode 전용) — Tag EngValue On/Off 슬라이드 토글.</summary>
public sealed class SwitchControl : DeviceControlBase
{
    private const double TrackW = 46, TrackH = 22, ThumbD = 16, Pad = 3;

    private readonly TranslateTransform _thumbMove = new();
    private Rectangle? _track;
    private Ellipse?   _thumb;

    protected override void OnDeviceControlLoaded()
    {
        IconHost.Children.Clear();

        var frame = new Canvas { Width = 52, Height = 40 };

        _track = new Rectangle
        {
            Width           = TrackW,
            Height          = TrackH,
            RadiusX         = TrackH / 2,
            RadiusY         = TrackH / 2,
            Fill            = ThemeResource.Find("BorderBrush"),
            Stroke          = ThemeResource.Find("Text2Brush"),
            StrokeThickness = 1
        };
        Canvas.SetLeft(_track, 3);
        Canvas.SetTop(_track, 9);
        frame.Children.Add(_track);

        _thumb = new Ellipse
        {
            Width           = ThumbD,
            Height          = ThumbD,
            Fill            = ThemeResource.Find("TextBrush"),
            RenderTransform = _thumbMove
        };
        Canvas.SetLeft(_thumb, 3 + Pad);
        Canvas.SetTop(_thumb, 9 + (TrackH - ThumbD) / 2);
        frame.Children.Add(_thumb);

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
        if (_track is null || _thumb is null) return;

        var isOn = node.IsBound && node.EngValue is double v && v > 0;

        _track.Fill = ThemeResource.Find(isOn ? "GreenBrush" : "BorderBrush");

        // 트랙 폭(46) - 여백(3*2) - thumb 지름(16) = 이동 가능 거리
        var travel = TrackW - Pad * 2 - ThumbD;
        var targetX = isOn ? travel : 0.0;

        var anim = new DoubleAnimation(targetX, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _thumbMove.BeginAnimation(TranslateTransform.XProperty, anim);
    }
}
