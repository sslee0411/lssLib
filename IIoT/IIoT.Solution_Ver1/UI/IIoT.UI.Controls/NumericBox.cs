// ══════════════════════════════════════════════════════════
//  IIoT.Controls · Controls/NumericBox.cs
//  역할: 산업용 숫자 전용 입력 컴포넌트 (Code-Only)
//        XAML 없이 코드만으로 구성 — Library 프로젝트 XAML
//        partial 연결 문제(CS0103/CS1061) 완전 회피
//  수정: 2026-06-13 — CS0103/CS1061/CS1056 오류 수정
// ══════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IIoT.Controls;

/// <summary>
/// 산업용 숫자 전용 입력 컨트롤.
/// Min/Max 범위 검증, ▲▼ 스텝 버튼, 단위 Suffix 지원.
///
/// <code>
/// &lt;uc:NumericBox Value="{Binding Temperature, Mode=TwoWay}"
///               Min="0" Max="200" Step="0.5"
///               DecimalPlaces="1" Suffix="°C"/&gt;
/// </code>
/// </summary>
public class NumericBox : UserControl
{
    // §1 ─ 내부 UI 요소 ───────────────────────────────────────
    private readonly Border        _outerBorder;
    private readonly TextBox       _inputBox;
    private readonly TextBlock     _suffixText;
    private readonly StackPanel    _stepPanel;
    private readonly RepeatButton  _btnUp;
    private readonly RepeatButton  _btnDown;

    // §1-1 ─ 내부 상태
    private bool _isUpdatingText;

    // §2 ─ DependencyProperty ─────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericBox),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((NumericBox)d)._RefreshDisplay()));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MinProperty =
        DependencyProperty.Register(nameof(Min), typeof(double), typeof(NumericBox),
            new PropertyMetadata(double.MinValue));
    public double Min { get => (double)GetValue(MinProperty); set => SetValue(MinProperty, value); }

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(nameof(Max), typeof(double), typeof(NumericBox),
            new PropertyMetadata(double.MaxValue));
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(double), typeof(NumericBox),
            new PropertyMetadata(1.0));
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(NumericBox),
            new PropertyMetadata(0, (d, _) => ((NumericBox)d)._RefreshDisplay()));
    public int DecimalPlaces { get => (int)GetValue(DecimalPlacesProperty); set => SetValue(DecimalPlacesProperty, value); }

    public static readonly DependencyProperty SuffixProperty =
        DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(NumericBox),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var ctrl = (NumericBox)d;
                var s = e.NewValue as string ?? string.Empty;
                ctrl._suffixText.Text       = s;
                ctrl._suffixText.Visibility = string.IsNullOrEmpty(s)
                    ? Visibility.Collapsed : Visibility.Visible;
            }));
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(NumericBox),
            new PropertyMetadata(false, (d, e) =>
            {
                var ctrl = (NumericBox)d;
                bool ro = (bool)e.NewValue;
                ctrl._inputBox.IsReadOnly  = ro;
                ctrl._stepPanel.Visibility = ro ? Visibility.Collapsed : Visibility.Visible;
            }));
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    public static readonly DependencyProperty ShowStepButtonsProperty =
        DependencyProperty.Register(nameof(ShowStepButtons), typeof(bool), typeof(NumericBox),
            new PropertyMetadata(true, (d, e) =>
            {
                var ctrl = (NumericBox)d;
                ctrl._stepPanel.Visibility = (bool)e.NewValue && !ctrl.IsReadOnly
                    ? Visibility.Visible : Visibility.Collapsed;
            }));
    public bool ShowStepButtons { get => (bool)GetValue(ShowStepButtonsProperty); set => SetValue(ShowStepButtonsProperty, value); }

    // §3 ─ 생성자 (UI 빌드) ───────────────────────────────────
    public NumericBox()
    {
        Height    = 32;
        MinWidth  = 80;

        // ── 입력 TextBox
        _inputBox = new TextBox
        {
            Background                = Brushes.Transparent,
            BorderThickness           = new Thickness(0),
            VerticalContentAlignment  = VerticalAlignment.Center,
            HorizontalContentAlignment= HorizontalAlignment.Right,
            Padding                   = new Thickness(8, 0, 4, 0),
            FocusVisualStyle          = null,
        };
        _inputBox.SetResourceReference(TextBox.ForegroundProperty,  "TextBrush");
        _inputBox.SetResourceReference(TextBox.CaretBrushProperty,  "AccBrush");
        _inputBox.SetResourceReference(TextBox.FontFamilyProperty,  "MonoFont");
        _inputBox.SetResourceReference(TextBox.FontSizeProperty,    "FontBase");

        _inputBox.TextChanged      += InputBox_TextChanged;
        _inputBox.LostFocus        += InputBox_LostFocus;
        _inputBox.GotFocus         += InputBox_GotFocus;
        _inputBox.PreviewKeyDown   += InputBox_PreviewKeyDown;

        // ── Suffix TextBlock
        _suffixText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(2, 0, 6, 0),
            Visibility        = Visibility.Collapsed,
        };
        _suffixText.SetResourceReference(TextBlock.ForegroundProperty, "Text3Brush");
        _suffixText.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _suffixText.SetResourceReference(TextBlock.FontSizeProperty,   "FontSm");

        // ── ▲▼ 버튼
        _btnUp   = _MakeStepButton(isUp: true);
        _btnDown = _MakeStepButton(isUp: false);
        _btnUp.Click   += (_, _) => _ChangeValue(+Step);
        _btnDown.Click += (_, _) => _ChangeValue(-Step);

        _stepPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(0, 2, 2, 2),
        };
        _stepPanel.Children.Add(_btnUp);
        _stepPanel.Children.Add(_btnDown);

        // ── 그리드 조립
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_inputBox,   0);
        Grid.SetColumn(_suffixText, 1);
        Grid.SetColumn(_stepPanel,  2);
        grid.Children.Add(_inputBox);
        grid.Children.Add(_suffixText);
        grid.Children.Add(_stepPanel);

        // ── 외곽 Border
        _outerBorder = new Border
        {
            BorderThickness = new Thickness(1),
            Child           = grid,
        };
        _outerBorder.SetResourceReference(Border.BackgroundProperty,   "CardBrush");
        _outerBorder.SetResourceReference(Border.BorderBrushProperty,  "Border2Brush");
        _outerBorder.SetResourceReference(Border.CornerRadiusProperty, "RadiusSm");

        Content = _outerBorder;

        Loaded += (_, _) => _RefreshDisplay();
    }

    // §4 ─ 디스플레이 갱신 ────────────────────────────────────
    private void _RefreshDisplay()
    {
        if (_isUpdatingText) return;
        _isUpdatingText = true;
        try
        {
            _inputBox.Text = Value.ToString("F" + DecimalPlaces);
            _SetErrorState(false);
        }
        finally { _isUpdatingText = false; }
    }

    // §5 ─ 검증 ───────────────────────────────────────────────
    private void _SetErrorState(bool isError, string? message = null)
    {
        var errorBrush  = Application.Current.TryFindResource("RedBrush")    as Brush ?? Brushes.Red;
        var normalBrush = Application.Current.TryFindResource("Border2Brush") as Brush ?? Brushes.Gray;
        _outerBorder.BorderBrush     = isError ? errorBrush : normalBrush;
        _outerBorder.BorderThickness = new Thickness(isError ? 2 : 1);
        ToolTipService.SetToolTip(this, isError ? message : null);
    }

    private bool _TryValidateAndApply(string text)
    {
        if (!double.TryParse(text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double val))
        {
            _SetErrorState(true, "숫자를 입력하세요");
            return false;
        }
        if (val < Min)
        {
            // CS1056 Fix: 중첩 {} 금지 → 사전 계산
            var minStr = Min.ToString("F" + DecimalPlaces);
            _SetErrorState(true, $"최솟값은 {minStr} 입니다");
            return false;
        }
        if (val > Max)
        {
            var maxStr = Max.ToString("F" + DecimalPlaces);
            _SetErrorState(true, $"최댓값은 {maxStr} 입니다");
            return false;
        }
        _SetErrorState(false);
        _isUpdatingText = true;
        try { Value = val; }
        finally { _isUpdatingText = false; }
        return true;
    }

    // §6 ─ 이벤트 핸들러 ──────────────────────────────────────
    private void InputBox_TextChanged(object s, TextChangedEventArgs e)
    {
        if (_isUpdatingText) return;
        _SetErrorState(false);
    }

    private void InputBox_LostFocus(object s, RoutedEventArgs e)
    {
        if (!_TryValidateAndApply(_inputBox.Text))
            _RefreshDisplay();
    }

    private void InputBox_GotFocus(object s, RoutedEventArgs e)
    {
        _inputBox.SelectAll();
        var acc = Application.Current.TryFindResource("AccBrush") as Brush;
        if (acc is not null) _outerBorder.BorderBrush = acc;
    }

    private void InputBox_PreviewKeyDown(object s, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:  _TryValidateAndApply(_inputBox.Text); e.Handled = true; break;
            case Key.Escape: _RefreshDisplay(); e.Handled = true; break;
            case Key.Up:     _ChangeValue(+Step); e.Handled = true; break;
            case Key.Down:   _ChangeValue(-Step); e.Handled = true; break;
            default:
                if (!_IsAllowedKey(e.Key)) e.Handled = true;
                break;
        }
    }

    // §7 ─ 내부 헬퍼 ──────────────────────────────────────────
    private void _ChangeValue(double delta)
    {
        double newVal = Math.Clamp(Value + delta, Min, Max);
        if (DecimalPlaces >= 0) newVal = Math.Round(newVal, DecimalPlaces);
        Value = newVal;
    }

    private static bool _IsAllowedKey(Key key)
    {
        if (key is Key.Back or Key.Delete or Key.Tab
                or Key.Left or Key.Right or Key.Home or Key.End) return true;
        if (key >= Key.D0 && key <= Key.D9) return true;
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return true;
        if (key is Key.OemPeriod or Key.Decimal) return true;
        if (key is Key.OemMinus or Key.Subtract) return true;
        if (Keyboard.Modifiers == ModifierKeys.Control
            && key is Key.A or Key.C or Key.V or Key.X) return true;
        return false;
    }

    private static RepeatButton _MakeStepButton(bool isUp)
    {
        // 화살표 Path
        var path = new Path
        {
            Data            = Geometry.Parse(isUp ? "M 2,6 L 7,2 L 12,6" : "M 2,2 L 7,6 L 12,2"),
            StrokeThickness = 1.5,
            Fill            = Brushes.Transparent,
            Width           = 10,
            Height          = 6,
            Stretch         = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        path.SetResourceReference(Path.StrokeProperty, "Text2Brush");

        var border = new Border
        {
            Child  = path,
            Padding = new Thickness(2),
        };

        var btn = new RepeatButton
        {
            Content         = border,
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Height          = 13,
            Delay           = 400,
            Interval        = 80,
            Focusable       = false,
            Cursor          = System.Windows.Input.Cursors.Hand,
            FocusVisualStyle = null,
        };
        return btn;
    }
}
