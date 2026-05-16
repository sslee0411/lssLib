// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes · Controls/ThemeSelectorPanel.xaml.cs
//  역할: ThemeSelectorPanel UserControl Code-behind
//        + XAML 변환기 (HexToColor, ThemePreviewBg, BoolToVis)
//  생성: 2025-05-16
// ══════════════════════════════════════════════════════════
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace IIoT.UI.Themes.Controls;

// ── UserControl ────────────────────────────────────────────

public partial class ThemeSelectorPanel : UserControl
{
    public ThemeSelectorPanel()
    {
        InitializeComponent();

        // DataContext를 자동으로 ThemeSelectorViewModel로 설정
        DataContext = new ThemeSelectorViewModel();
    }
}

// ══════════════════════════════════════════════════════════
//  변환기 모음
// ══════════════════════════════════════════════════════════

/// <summary>#RRGGBB 문자열 → WPF Color 변환</summary>
public sealed class HexToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { }
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>IsDark(bool) → 프리뷰 배경색 Color 변환</summary>
public sealed class ThemePreviewBgConverter : IMultiValueConverter
{
    // 어두운 테마: #111520  /  밝은 테마: #EEF1F6
    private static readonly Color DarkBg  = Color.FromRgb(0x11, 0x15, 0x20);
    private static readonly Color LightBg = Color.FromRgb(0xEE, 0xF1, 0xF6);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length > 0 && values[0] is bool isDark
            ? (isDark ? DarkBg : LightBg)
            : DarkBg;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → Visibility 변환</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (Visibility)value == Visibility.Visible;
}
