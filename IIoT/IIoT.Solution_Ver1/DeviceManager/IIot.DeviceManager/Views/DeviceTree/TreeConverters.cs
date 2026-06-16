// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · TreeConverters.cs
//  역할: DeviceTreeView 전용 값 변환기 모음
//  생성: 2025-05-22
// ══════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IIoT.DeviceManager.Views.DeviceTree;

/// <summary>
/// bool → Visibility (true = Visible, false = Collapsed)
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>
/// bool → Visibility 반전 (true = Collapsed, false = Visible)
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityInvertConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>
/// null → Visibility (null = Collapsed, not-null = Visible)
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
