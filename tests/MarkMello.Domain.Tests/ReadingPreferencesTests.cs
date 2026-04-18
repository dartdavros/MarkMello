using MarkMello.Domain;

namespace MarkMello.Domain.Tests;

public sealed class ReadingPreferencesTests
{
    [Fact]
    public void NormalizeReturnsDefaultsForNull()
    {
        var normalized = ReadingPreferences.Normalize(null);

        Assert.Equal(ReadingPreferences.Default, normalized);
    }

    [Fact]
    public void NormalizeClampsAndRoundsOutOfRangeValues()
    {
        var candidate = new ReadingPreferences(
            FontFamily: (FontFamilyMode)42,
            FontSize: 200,
            LineHeight: 0.2,
            ContentWidth: 517);

        var normalized = ReadingPreferences.Normalize(candidate);

        Assert.Equal(FontFamilyMode.Serif, normalized.FontFamily);
        Assert.Equal(ReadingPreferences.MaxFontSize, normalized.FontSize);
        Assert.Equal(ReadingPreferences.MinLineHeight, normalized.LineHeight);
        Assert.Equal(ReadingPreferences.MinContentWidth, normalized.ContentWidth);
    }
}
