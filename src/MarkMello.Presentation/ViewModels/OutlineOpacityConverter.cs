using System.Globalization;
using Avalonia.Data.Converters;

namespace MarkMello.Presentation.ViewModels;

/// <summary>Строки уровней глубже MaxLevel приглушены, но редактируемы (ADR-0008 Rule 11).</summary>
public sealed class OutlineOpacityConverter : IValueConverter
{
    public static OutlineOpacityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 1.0 : 0.5;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
