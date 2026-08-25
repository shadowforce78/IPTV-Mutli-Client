using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace IptvClient.Converters;

/// <summary>True → fully opaque, false → fully transparent. Paired with a Transitions/
/// DoubleTransition on Opacity in XAML to get a fade instead of an instant cut.</summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
