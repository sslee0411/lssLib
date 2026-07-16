// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Layout/HexColorConverter.cs
//  역할: "#2a7fd4" 문자열 → WPF Color 변환기
//        LayoutCanvasView.xaml 에서 layout: 네임스페이스로 참조
//        (IIoT.Studio Core/Canvas/HexColorConverter.cs 이식)
//  HM-03: 신규
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows.Media;

namespace IIoT.HMI.Core.Layout;

/// <summary>
/// "#2a7fd4" 형식 문자열 → WPF Color 변환기.
/// XAML 에서 layout:HexColorConverter 로 참조.
/// </summary>
public sealed class HexColorConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType,
        object parameter, CultureInfo culture)
    {
        if (value is not string hex) return Colors.Gray;
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = System.Convert.ToByte(hex[0..2], 16);
                byte g = System.Convert.ToByte(hex[2..4], 16);
                byte b = System.Convert.ToByte(hex[4..6], 16);
                return Color.FromRgb(r, g, b);
            }
        }
        catch { /* 변환 실패 시 회색 반환 */ }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType,
        object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
