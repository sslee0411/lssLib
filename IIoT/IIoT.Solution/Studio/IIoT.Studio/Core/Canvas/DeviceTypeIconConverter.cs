// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/DeviceTypeIconConverter.cs
//  역할: "PLC" | "Device" → 이모지 아이콘 변환기
//        XAML 에서 canvas: 네임스페이스로 참조
//  S-12B: CanvasView.xaml.cs 에서 분리 (MC3074 해결)
//  생성: 2026-06-18
// ══════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows.Data;

namespace IIoT.Studio.Core.Canvas;

public sealed class DeviceTypeIconConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is "PLC" ? "🖥" : "📟";

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
