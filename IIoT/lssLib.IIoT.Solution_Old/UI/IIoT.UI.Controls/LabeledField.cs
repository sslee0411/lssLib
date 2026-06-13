// ══════════════════════════════════════════════════════════
//  IIoT.Controls · Controls/LabeledField.cs
//  역할: 레이블 + 입력 컨트롤 한 쌍 래퍼 (Code-Only)
//  수정: 2026-06-13
//    CS0246 Fix: [ContentProperty] → System.Windows.Markup using 추가
//               + XAML partial 오류 완전 회피 (Code-Only)
// ══════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;   // ★ ContentPropertyAttribute

namespace IIoT.Controls;

/// <summary>
/// 레이블 + 입력 컨트롤 한 쌍 래퍼.
/// 어떤 WPF 컨트롤이든 Content 슬롯에 넣을 수 있습니다.
///
/// <code>
/// &lt;uc:LabeledField Label="온도" IsRequired="True" Hint="0~200 °C"&gt;
///     &lt;uc:NumericBox Value="{Binding Temp}" Min="0" Max="200" Suffix="°C"/&gt;
/// &lt;/uc:LabeledField&gt;
/// </code>
/// </summary>
[ContentProperty(nameof(FieldContent))]   // ★ using System.Windows.Markup 필수
public class LabeledField : UserControl
{
    // §1 ─ 내부 UI 요소 ───────────────────────────────────────
    private readonly TextBlock     _labelText;
    private readonly TextBlock     _requiredMark;
    private readonly TextBlock     _hintText;
    private readonly ContentPresenter _contentSlot;
    private readonly TextBlock     _errorText;

    // §2 ─ DependencyProperty ─────────────────────────────────

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(LabeledField),
            new PropertyMetadata(string.Empty,
                (d, e) => ((LabeledField)d)._labelText.Text = e.NewValue as string ?? ""));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(LabeledField),
            new PropertyMetadata(false,
                (d, e) => ((LabeledField)d)._requiredMark.Visibility =
                    (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(LabeledField),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var ctrl = (LabeledField)d;
                var h = e.NewValue as string ?? "";
                ctrl._hintText.Text       = h;
                ctrl._hintText.Visibility = string.IsNullOrEmpty(h)
                    ? Visibility.Collapsed : Visibility.Visible;
            }));

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(LabeledField),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var ctrl = (LabeledField)d;
                var msg = e.NewValue as string ?? "";
                ctrl._errorText.Text       = msg;
                ctrl._errorText.Visibility = string.IsNullOrEmpty(msg)
                    ? Visibility.Collapsed : Visibility.Visible;
            }));

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    // ★ ContentProperty 로 지정된 슬롯 — XAML 중첩 자식이 여기에 들어옴
    public static readonly DependencyProperty FieldContentProperty =
        DependencyProperty.Register(nameof(FieldContent), typeof(object), typeof(LabeledField),
            new PropertyMetadata(null,
                (d, e) => ((LabeledField)d)._contentSlot.Content = e.NewValue));

    public object? FieldContent
    {
        get => GetValue(FieldContentProperty);
        set => SetValue(FieldContentProperty, value);
    }

    // §3 ─ 생성자 (UI 빌드) ───────────────────────────────────
    public LabeledField()
    {
        // 레이블
        _labelText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _labelText.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _labelText.SetResourceReference(TextBlock.FontSizeProperty,   "FontSm");
        _labelText.SetResourceReference(TextBlock.ForegroundProperty, "Text2Brush");
        _labelText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        // 필수 * 마커
        _requiredMark = new TextBlock
        {
            Text            = " *",
            FontWeight      = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility      = Visibility.Collapsed,
        };
        _requiredMark.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _requiredMark.SetResourceReference(TextBlock.FontSizeProperty,   "FontSm");
        _requiredMark.SetResourceReference(TextBlock.ForegroundProperty, "RedBrush");

        // 힌트
        _hintText = new TextBlock
        {
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility        = Visibility.Collapsed,
        };
        _hintText.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _hintText.SetResourceReference(TextBlock.ForegroundProperty, "Text3Brush");

        // 레이블 행 (Label + * | Hint)
        var labelRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftPanel = new StackPanel { Orientation = Orientation.Horizontal };
        leftPanel.Children.Add(_labelText);
        leftPanel.Children.Add(_requiredMark);
        Grid.SetColumn(leftPanel, 0);
        Grid.SetColumn(_hintText, 1);
        labelRow.Children.Add(leftPanel);
        labelRow.Children.Add(_hintText);

        // 콘텐츠 슬롯
        _contentSlot = new ContentPresenter();

        // 에러 메시지
        _errorText = new TextBlock
        {
            FontSize     = 10,
            Margin       = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Visibility   = Visibility.Collapsed,
        };
        _errorText.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        _errorText.SetResourceReference(TextBlock.ForegroundProperty, "RedBrush");

        // 전체 StackPanel
        var root = new StackPanel { Orientation = Orientation.Vertical };
        root.Children.Add(labelRow);
        root.Children.Add(_contentSlot);
        root.Children.Add(_errorText);

        Content = root;
    }
}
