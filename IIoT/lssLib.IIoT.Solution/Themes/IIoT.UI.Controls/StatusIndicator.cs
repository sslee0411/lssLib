// ══════════════════════════════════════════════════════════
//  IIoT.Controls · Controls/StatusIndicator.cs
//  역할: Good/Warn/Bad/Unknown 상태 점 + 텍스트 (Code-Only)
//  수정: 2026-06-13 — XAML partial 오류 완전 회피
// ══════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace IIoT.Controls;

// ── 공개 열거형 ────────────────────────────────────────────
public enum IndicatorStatus { Good, Warn, Bad, Unknown }
public enum IndicatorSize   { Small, Normal, Large }

/// <summary>
/// 산업용 상태 표시 컴포넌트.
/// Good → 녹색 펄스 / Warn → 황색 깜박 / Bad → 적색 빠른 깜박
///
/// <code>
/// &lt;uc:StatusIndicator Status="{Binding Quality}"
///                     ShowText="True" Size="Normal"/&gt;
/// </code>
/// </summary>
public class StatusIndicator : UserControl
{
    // §1 ─ 내부 UI 요소 ───────────────────────────────────────
    private readonly Ellipse   _dot;
    private readonly Ellipse   _pulse;
    private readonly TextBlock _label;
    private Storyboard?        _anim;

    // §2 ─ DependencyProperty ─────────────────────────────────

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(IndicatorStatus), typeof(StatusIndicator),
            new PropertyMetadata(IndicatorStatus.Unknown,
                (d, e) => { if (((StatusIndicator)d).IsLoaded) ((StatusIndicator)d)._Apply(); }));

    public IndicatorStatus Status
    {
        get => (IndicatorStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty ShowTextProperty =
        DependencyProperty.Register(nameof(ShowText), typeof(bool), typeof(StatusIndicator),
            new PropertyMetadata(false, (d, e) =>
                ((StatusIndicator)d)._label.Visibility =
                    (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public bool ShowText
    {
        get => (bool)GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    public static readonly DependencyProperty CustomTextProperty =
        DependencyProperty.Register(nameof(CustomText), typeof(string), typeof(StatusIndicator),
            new PropertyMetadata(null,
                (d, _) => { if (((StatusIndicator)d).IsLoaded) ((StatusIndicator)d)._Apply(); }));

    public string? CustomText
    {
        get => (string?)GetValue(CustomTextProperty);
        set => SetValue(CustomTextProperty, value);
    }

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(IndicatorSize), typeof(StatusIndicator),
            new PropertyMetadata(IndicatorSize.Normal,
                (d, e) => { if (((StatusIndicator)d).IsLoaded) ((StatusIndicator)d)._ApplySize((IndicatorSize)e.NewValue); }));

    public new IndicatorSize Size
    {
        get => (IndicatorSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    // §3 ─ 생성자 ─────────────────────────────────────────────
    public StatusIndicator()
    {
        // 상태 점
        _dot = new Ellipse { Width = 10, Height = 10 };

        // 펄스 링
        _pulse = new Ellipse
        {
            Width           = 16,
            Height          = 16,
            Fill            = Brushes.Transparent,
            StrokeThickness = 1.5,
            Opacity         = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
        };

        // 점 + 링 겹치기
        var dotGrid = new Grid { Width = 18, Height = 18 };
        dotGrid.Children.Add(_pulse);
        dotGrid.Children.Add(_dot);

        // 레이블
        _label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(6, 0, 0, 0),
            Visibility        = Visibility.Collapsed,
        };
        _label.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _label.SetResourceReference(TextBlock.FontSizeProperty,   "FontSm");

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(dotGrid);
        panel.Children.Add(_label);

        Content = panel;
        Loaded += (_, _) => { _ApplySize(Size); _Apply(); };
    }

    // §4 ─ 상태 적용 ──────────────────────────────────────────
    private void _Apply()
    {
        _anim?.Stop(this);
        _anim        = null;
        _dot.Opacity = 1;
        _pulse.Opacity = 0;

        var (key, text) = Status switch
        {
            IndicatorStatus.Good    => ("GreenBrush",  CustomText ?? "정상"),
            IndicatorStatus.Warn    => ("YellowBrush", CustomText ?? "경고"),
            IndicatorStatus.Bad     => ("RedBrush",    CustomText ?? "오류"),
            _                       => ("Text3Brush",  CustomText ?? "알 수 없음"),
        };

        var brush = Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
        _dot.Fill      = brush;
        _pulse.Stroke  = brush;
        _label.Text    = text;
        _label.Foreground = brush;

        switch (Status)
        {
            case IndicatorStatus.Good: _StartPulse();        break;
            case IndicatorStatus.Warn: _StartBlink(1.2);     break;
            case IndicatorStatus.Bad:  _StartBlink(0.5);     break;
        }
    }

    private void _ApplySize(IndicatorSize sz)
    {
        double dot = sz switch { IndicatorSize.Small => 6, IndicatorSize.Large => 14, _ => 10 };
        _dot.Width   = dot;  _dot.Height   = dot;
        _pulse.Width = dot + 6; _pulse.Height = dot + 6;
        _label.FontSize = sz switch { IndicatorSize.Small => 11, IndicatorSize.Large => 14, _ => 12 };
    }

    private void _StartPulse()
    {
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        var fade = new DoubleAnimation { From = 0.7, To = 0,
            Duration = TimeSpan.FromSeconds(1.5),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(fade, _pulse);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));

        var sx = new DoubleAnimation { From = 1, To = 2,
            Duration = TimeSpan.FromSeconds(1.5),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(sx, _pulse);
        Storyboard.SetTargetProperty(sx,
            new PropertyPath("RenderTransform.(ScaleTransform.ScaleX)"));

        var sy = sx.Clone();
        Storyboard.SetTarget(sy, _pulse);
        Storyboard.SetTargetProperty(sy,
            new PropertyPath("RenderTransform.(ScaleTransform.ScaleY)"));

        sb.Children.Add(fade); sb.Children.Add(sx); sb.Children.Add(sy);
        _anim = sb;
        sb.Begin(this, true);
    }

    private void _StartBlink(double period)
    {
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var kf = new DoubleAnimationUsingKeyFrames();
        kf.KeyFrames.Add(new DiscreteDoubleKeyFrame(1,   KeyTime.FromTimeSpan(TimeSpan.Zero)));
        kf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.2, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(period * .5))));
        kf.KeyFrames.Add(new DiscreteDoubleKeyFrame(1,   KeyTime.FromTimeSpan(TimeSpan.FromSeconds(period))));
        Storyboard.SetTarget(kf, _dot);
        Storyboard.SetTargetProperty(kf, new PropertyPath(OpacityProperty));
        sb.Children.Add(kf);
        _anim = sb;
        sb.Begin(this, true);
    }
}
