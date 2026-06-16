// ══════════════════════════════════════════════════════════
//  IIoT.Controls · Controls/TagValueCell.cs
//  역할: 수집 태그 한 줄 표시 (Code-Only)
//        TagId / TagName / Value / Unit / Quality 표시
//        값 변경 시 배경 플래시 애니메이션
//  수정: 2026-06-13 — XAML partial 오류 완전 회피
// ══════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IIoT.Controls;

/// <summary>
/// 수집 태그 한 줄 표시 컴포넌트.
/// <code>
/// &lt;uc:TagValueCell TagName="{Binding TagName}"
///                 Value="{Binding DisplayValue}"
///                 Unit="{Binding Unit}"
///                 Quality="{Binding QualityStatus}"
///                 FlashOnChange="True"/&gt;
/// </code>
/// </summary>
public class TagValueCell : UserControl
{
    // §1 ─ 내부 UI 요소 ───────────────────────────────────────
    private readonly Border           _cellBorder;
    private readonly StatusIndicator  _indicator;
    private readonly TextBlock        _nameText;
    private readonly TextBlock        _idText;
    private readonly TextBlock        _valueText;
    private readonly TextBlock        _unitText;

    // §2 ─ DependencyProperty ─────────────────────────────────

    public static readonly DependencyProperty TagIdProperty =
        DependencyProperty.Register(nameof(TagId), typeof(string), typeof(TagValueCell),
            new PropertyMetadata(string.Empty,
                (d, e) => ((TagValueCell)d)._idText.Text = e.NewValue as string ?? ""));
    public string TagId { get => (string)GetValue(TagIdProperty); set => SetValue(TagIdProperty, value); }

    public static readonly DependencyProperty TagNameProperty =
        DependencyProperty.Register(nameof(TagName), typeof(string), typeof(TagValueCell),
            new PropertyMetadata(string.Empty,
                (d, e) => ((TagValueCell)d)._nameText.Text = e.NewValue as string ?? ""));
    public string TagName { get => (string)GetValue(TagNameProperty); set => SetValue(TagNameProperty, value); }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(TagValueCell),
            new PropertyMetadata("—", (d, e) =>
            {
                var ctrl = (TagValueCell)d;
                ctrl._valueText.Text = e.NewValue as string ?? "—";
                if (ctrl.FlashOnChange && ctrl.IsLoaded)
                    ctrl._Flash();
            }));
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(TagValueCell),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var ctrl = (TagValueCell)d;
                var u = e.NewValue as string ?? "";
                ctrl._unitText.Text       = u;
                ctrl._unitText.Visibility = ctrl.ShowUnit && !string.IsNullOrEmpty(u)
                    ? Visibility.Visible : Visibility.Collapsed;
            }));
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    public static readonly DependencyProperty QualityProperty =
        DependencyProperty.Register(nameof(Quality), typeof(IndicatorStatus), typeof(TagValueCell),
            new PropertyMetadata(IndicatorStatus.Unknown, (d, e) =>
            {
                var ctrl    = (TagValueCell)d;
                var status  = (IndicatorStatus)e.NewValue;
                ctrl._indicator.Status = status;

                var brushKey = status switch
                {
                    IndicatorStatus.Good    => "TextBrush",
                    IndicatorStatus.Warn    => "YellowBrush",
                    IndicatorStatus.Bad     => "RedBrush",
                    _                       => "Text3Brush",
                };
                var brush = Application.Current.TryFindResource(brushKey) as Brush;
                if (brush is not null) ctrl._valueText.Foreground = brush;
            }));
    public IndicatorStatus Quality { get => (IndicatorStatus)GetValue(QualityProperty); set => SetValue(QualityProperty, value); }

    public static readonly DependencyProperty ShowTagIdProperty =
        DependencyProperty.Register(nameof(ShowTagId), typeof(bool), typeof(TagValueCell),
            new PropertyMetadata(false,
                (d, e) => ((TagValueCell)d)._idText.Visibility =
                    (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));
    public bool ShowTagId { get => (bool)GetValue(ShowTagIdProperty); set => SetValue(ShowTagIdProperty, value); }

    public static readonly DependencyProperty ShowUnitProperty =
        DependencyProperty.Register(nameof(ShowUnit), typeof(bool), typeof(TagValueCell),
            new PropertyMetadata(true, (d, e) =>
            {
                var ctrl = (TagValueCell)d;
                ctrl._unitText.Visibility = (bool)e.NewValue && !string.IsNullOrEmpty(ctrl.Unit)
                    ? Visibility.Visible : Visibility.Collapsed;
            }));
    public bool ShowUnit { get => (bool)GetValue(ShowUnitProperty); set => SetValue(ShowUnitProperty, value); }

    public static readonly DependencyProperty FlashOnChangeProperty =
        DependencyProperty.Register(nameof(FlashOnChange), typeof(bool), typeof(TagValueCell),
            new PropertyMetadata(true));
    public bool FlashOnChange { get => (bool)GetValue(FlashOnChangeProperty); set => SetValue(FlashOnChangeProperty, value); }

    // §3 ─ 생성자 (UI 빌드) ───────────────────────────────────
    public TagValueCell()
    {
        MinHeight = 28;

        // 상태 인디케이터
        _indicator = new StatusIndicator { Size = IndicatorSize.Small,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0) };

        // 태그명
        _nameText = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _nameText.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _nameText.SetResourceReference(TextBlock.FontSizeProperty,   "FontBase");
        _nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        // TagId (기본 숨김)
        _idText = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize     = 10,
            Visibility   = Visibility.Collapsed,
        };
        _idText.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFont");
        _idText.SetResourceReference(TextBlock.ForegroundProperty, "Text3Brush");

        var nameStack = new StackPanel { Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(_nameText);
        nameStack.Children.Add(_idText);

        // 값
        _valueText = new TextBlock
        {
            Text          = "—",
            MinWidth      = 60,
            TextAlignment = TextAlignment.Right,
            FontWeight    = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _valueText.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFont");
        _valueText.SetResourceReference(TextBlock.FontSizeProperty,   "FontData");
        _valueText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        // 단위
        _unitText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(4, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _unitText.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _unitText.SetResourceReference(TextBlock.FontSizeProperty,   "FontSm");
        _unitText.SetResourceReference(TextBlock.ForegroundProperty, "Text3Brush");

        var valuePanel = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 0, 0),
        };
        valuePanel.Children.Add(_valueText);
        valuePanel.Children.Add(_unitText);

        // 그리드
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_indicator,  0);
        Grid.SetColumn(nameStack,   1);
        Grid.SetColumn(valuePanel,  2);
        grid.Children.Add(_indicator);
        grid.Children.Add(nameStack);
        grid.Children.Add(valuePanel);

        // 외곽 Border (플래시 대상)
        _cellBorder = new Border
        {
            Background    = Brushes.Transparent,
            Padding       = new Thickness(8, 4, 8, 4),
            Child         = grid,
        };
        _cellBorder.SetResourceReference(Border.CornerRadiusProperty, "RadiusSm");

        Content = _cellBorder;
    }

    // §4 ─ 플래시 애니메이션 ──────────────────────────────────
    private void _Flash()
    {
        var accent = Application.Current.TryFindResource("AccFaintBrush") as SolidColorBrush;
        var color  = accent?.Color ?? Color.FromArgb(30, 79, 124, 255);

        var flashBrush = new SolidColorBrush(color);
        _cellBorder.Background = flashBrush;

        var anim = new ColorAnimation
        {
            To       = Colors.Transparent,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }
}
