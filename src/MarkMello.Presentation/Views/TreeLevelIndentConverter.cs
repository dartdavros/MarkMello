using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Отступ строки дерева по её уровню: 6px у корня и +12px на уровень.
/// Штатный отступ Fluent считает свой конвертер, и переопределить его иначе нельзя:
/// вместе с ним пришлось бы потерять и сам отступ вложенности.
/// </summary>
public sealed class TreeLevelIndentConverter : IValueConverter
{
    public const double RootIndent = 6;
    public const double LevelIndent = 12;

    public static TreeLevelIndentConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int depth && depth > 0 ? depth : 0;

        return new Thickness(RootIndent + (level * LevelIndent), 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Ширина места под шеврон: 18px (12 плюс щель 6) у каталогов и ноль у файлов.
/// Псевдокласс <c>:empty</c> для этого не годится — каталог до чтения тоже пуст.
/// </summary>
public sealed class TreeChevronWidthConverter : IValueConverter
{
    public const double DirectoryWidth = 18;

    public static TreeChevronWidthConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? DirectoryWidth : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
