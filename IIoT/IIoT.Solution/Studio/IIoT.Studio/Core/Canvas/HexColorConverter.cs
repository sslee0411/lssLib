// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/HexColorConverter.cs
//  역할: "#2a7fd4" 문자열 → WPF Color 변환기
//        CanvasView.xaml 에서 canvas: 네임스페이스로 참조
//  S-11: Core.Canvas 네임스페이스로 이동 (XDG0008 해결)
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows.Media;

namespace IIoT.Studio.Core.Canvas;

/// <summary>
/// "#2a7fd4" 형식 문자열 → WPF Color 변환기.
/// XAML 에서 canvas:HexColorConverter 로 참조.
/// </summary>
public sealed class HexColorConverter : System.Windows.Data.IValueConverter
{
    public static readonly HexColorConverter Instance = new();

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
