namespace MarkMello.Domain;

/// <summary>
/// Настройки панели оглавления (ADR-0008 Rule 11-12). Размеры шрифта заданы в пикселях
/// и не зависят от <see cref="ReadingPreferences.InterfaceFontSize"/>.
/// </summary>
public sealed record OutlinePreferences(
    bool IsVisible = false,
    OutlinePanelSide Side = OutlinePanelSide.Left,
    int MaxLevel = OutlinePreferences.DefaultMaxLevel,
    double LineHeight = OutlinePreferences.DefaultLineHeight,
    double Width = OutlinePreferences.DefaultWidth,
    int H1FontSize = 15,
    int H2FontSize = 14,
    int H3FontSize = 13,
    int H4FontSize = 13,
    int H5FontSize = 13,
    int H6FontSize = 13)
{
    public const int MinLevel = 1;
    public const int MaxLevelLimit = 6;
    public const int DefaultMaxLevel = 3;
    public const double MinLineHeight = 1.2;
    public const double MaxLineHeight = 2.0;
    public const double LineHeightStep = 0.1;
    public const double DefaultLineHeight = 1.5;
    public const int MinFontSize = 11;
    public const int MaxFontSize = 20;
    public const double MinWidth = 220;
    public const double MaxWidth = 600;
    public const double DefaultWidth = 260;

    public static OutlinePreferences Default { get; } = new();

    public static OutlinePreferences Normalize(OutlinePreferences? preferences)
    {
        if (preferences is null)
        {
            return Default;
        }

        return new OutlinePreferences(
            preferences.IsVisible,
            Enum.IsDefined(preferences.Side) ? preferences.Side : Default.Side,
            Math.Clamp(preferences.MaxLevel, MinLevel, MaxLevelLimit),
            NormalizeLineHeight(preferences.LineHeight),
            NormalizeWidth(preferences.Width),
            ClampFont(preferences.H1FontSize),
            ClampFont(preferences.H2FontSize),
            ClampFont(preferences.H3FontSize),
            ClampFont(preferences.H4FontSize),
            ClampFont(preferences.H5FontSize),
            ClampFont(preferences.H6FontSize));
    }

    public OutlinePreferences Normalize() => Normalize(this);

    public int GetFontSize(int level) => level switch
    {
        1 => H1FontSize,
        2 => H2FontSize,
        3 => H3FontSize,
        4 => H4FontSize,
        5 => H5FontSize,
        _ => H6FontSize
    };

    public OutlinePreferences WithFontSize(int level, int size) => level switch
    {
        1 => this with { H1FontSize = size },
        2 => this with { H2FontSize = size },
        3 => this with { H3FontSize = size },
        4 => this with { H4FontSize = size },
        5 => this with { H5FontSize = size },
        _ => this with { H6FontSize = size }
    };

    public static double NormalizeWidth(double value)
        => double.IsFinite(value) ? Math.Clamp(value, MinWidth, MaxWidth) : DefaultWidth;

    private static int ClampFont(int value) => Math.Clamp(value, MinFontSize, MaxFontSize);

    private static double NormalizeLineHeight(double value)
    {
        if (!double.IsFinite(value))
        {
            return DefaultLineHeight;
        }

        var clamped = Math.Clamp(value, MinLineHeight, MaxLineHeight);
        var rounded = Math.Round(clamped / LineHeightStep, MidpointRounding.AwayFromZero) * LineHeightStep;
        return Math.Round(rounded, 1, MidpointRounding.AwayFromZero);
    }
}
