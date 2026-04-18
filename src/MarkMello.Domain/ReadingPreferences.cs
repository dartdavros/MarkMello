namespace MarkMello.Domain;

/// <summary>
/// Пользовательские настройки чтения документа. Применяются live, не требуют перезапуска.
/// </summary>
/// <param name="FontFamily">Семейство шрифта (Serif/Sans/Mono).</param>
/// <param name="FontSize">Базовый размер шрифта в пикселях.</param>
/// <param name="LineHeight">Межстрочный интервал (множитель к размеру шрифта).</param>
/// <param name="ContentWidth">Максимальная ширина центральной колонки чтения в пикселях.</param>
public sealed record ReadingPreferences(
    FontFamilyMode FontFamily,
    int FontSize,
    double LineHeight,
    int ContentWidth)
{
    public const int MinFontSize = 14;
    public const int MaxFontSize = 24;
    public const double MinLineHeight = 1.4;
    public const double MaxLineHeight = 2.0;
    public const double LineHeightStep = 0.05;
    public const int MinContentWidth = 580;
    public const int MaxContentWidth = 860;
    public const int ContentWidthStep = 20;

    /// <summary>
    /// Безопасные значения по умолчанию. Используются при отсутствии или повреждении сохранённых настроек.
    /// </summary>
    public static ReadingPreferences Default { get; } = new(
        FontFamily: FontFamilyMode.Serif,
        FontSize: 18,
        LineHeight: 1.7,
        ContentWidth: 720);

    /// <summary>
    /// Нормализует пользовательские настройки до безопасного и предсказуемого диапазона.
    /// Используется для live-обновлений и восстановления из persistence.
    /// </summary>
    public static ReadingPreferences Normalize(ReadingPreferences? preferences)
    {
        if (preferences is null)
        {
            return Default;
        }

        var fontFamily = Enum.IsDefined(preferences.FontFamily)
            ? preferences.FontFamily
            : Default.FontFamily;

        var fontSize = Math.Clamp(preferences.FontSize, MinFontSize, MaxFontSize);
        var lineHeight = NormalizeLineHeight(preferences.LineHeight);
        var contentWidth = NormalizeContentWidth(preferences.ContentWidth);

        return new ReadingPreferences(fontFamily, fontSize, lineHeight, contentWidth);
    }

    public ReadingPreferences Normalize() => Normalize(this);

    private static double NormalizeLineHeight(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return Default.LineHeight;
        }

        var clamped = Math.Clamp(value, MinLineHeight, MaxLineHeight);
        var rounded = Math.Round(clamped / LineHeightStep, MidpointRounding.AwayFromZero) * LineHeightStep;
        return Math.Round(rounded, 2, MidpointRounding.AwayFromZero);
    }

    private static int NormalizeContentWidth(int value)
    {
        var clamped = Math.Clamp(value, MinContentWidth, MaxContentWidth);
        var rounded = (int)Math.Round(clamped / (double)ContentWidthStep, MidpointRounding.AwayFromZero) * ContentWidthStep;
        return Math.Clamp(rounded, MinContentWidth, MaxContentWidth);
    }
}
