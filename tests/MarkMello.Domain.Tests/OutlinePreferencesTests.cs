using MarkMello.Domain;

namespace MarkMello.Domain.Tests;

public sealed class OutlinePreferencesTests
{
    private static readonly int[] DefaultFontSizes = [15, 14, 13, 13, 13, 13];

    [Fact]
    public void DefaultMatchesAdr()
    {
        var d = OutlinePreferences.Default;

        Assert.False(d.IsVisible);
        Assert.Equal(OutlinePanelSide.Left, d.Side);
        Assert.Equal(3, d.MaxLevel);
        Assert.Equal(1.5, d.LineHeight);
        Assert.Equal(260, d.Width);
        Assert.Equal(DefaultFontSizes, Enumerable.Range(1, 6).Select(d.GetFontSize));
    }

    [Fact]
    public void NormalizeReturnsDefaultForNull()
        => Assert.Equal(OutlinePreferences.Default, OutlinePreferences.Normalize(null));

    [Theory]
    [InlineData(5000, 600)]
    [InlineData(601, 600)]
    [InlineData(500, 500)]
    [InlineData(600, 600)]
    [InlineData(100, 220)]
    public void NormalizeClampsWidthToRange(double width, double expected)
        => Assert.Equal(expected, OutlinePreferences.Normalize(OutlinePreferences.Default with { Width = width }).Width);

    [Fact]
    public void NormalizeClampsEveryValue()
    {
        var candidate = new OutlinePreferences(
            IsVisible: true,
            Side: (OutlinePanelSide)42,
            MaxLevel: 9,
            LineHeight: 5,
            Width: 10,
            H1FontSize: 99,
            H2FontSize: 1);

        var n = OutlinePreferences.Normalize(candidate);

        Assert.True(n.IsVisible);
        Assert.Equal(OutlinePanelSide.Left, n.Side);
        Assert.Equal(6, n.MaxLevel);
        Assert.Equal(2.0, n.LineHeight);
        Assert.Equal(220, n.Width);
        Assert.Equal(20, n.H1FontSize);
        Assert.Equal(11, n.H2FontSize);
    }

    [Fact]
    public void NormalizeRoundsLineHeightToStep()
    {
        var n = OutlinePreferences.Normalize(OutlinePreferences.Default with { LineHeight = 1.56 });

        Assert.Equal(1.6, n.LineHeight);
    }

    [Fact]
    public void NormalizeReplacesNonFiniteNumbersWithDefaults()
    {
        var n = OutlinePreferences.Normalize(
            OutlinePreferences.Default with { LineHeight = double.NaN, Width = double.PositiveInfinity });

        Assert.Equal(OutlinePreferences.DefaultLineHeight, n.LineHeight);
        Assert.Equal(OutlinePreferences.DefaultWidth, n.Width);
    }

    [Theory]
    [InlineData(1, 18)]
    [InlineData(4, 12)]
    [InlineData(6, 20)]
    public void WithFontSizeChangesOnlyThatLevel(int level, int size)
    {
        var changed = OutlinePreferences.Default.WithFontSize(level, size);

        Assert.Equal(size, changed.GetFontSize(level));
        foreach (var other in Enumerable.Range(1, 6).Where(l => l != level))
        {
            Assert.Equal(OutlinePreferences.Default.GetFontSize(other), changed.GetFontSize(other));
        }
    }
}
