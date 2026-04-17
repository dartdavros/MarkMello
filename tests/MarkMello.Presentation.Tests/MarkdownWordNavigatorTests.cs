using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownWordNavigatorTests
{
    [Theory]
    [InlineData("alpha beta", 0, 0, 5)]
    [InlineData("alpha beta", 3, 0, 5)]
    [InlineData("alpha beta", 6, 6, 10)]
    [InlineData("hello, world!", 1, 0, 5)]
    [InlineData("hello, world!", 8, 7, 12)]
    [InlineData("snake_case value", 8, 0, 10)]
    public void GetWordRangeReturnsExpectedWordBounds(string text, int offset, int expectedStart, int expectedEnd)
    {
        var range = MarkdownWordNavigator.GetWordRange(text, offset);

        Assert.Equal(new DocumentTextRange(expectedStart, expectedEnd), range);
    }

    [Theory]
    [InlineData("hello, world!", 5)]
    [InlineData("hello, world!", 6)]
    [InlineData(" spaced", 0)]
    public void GetWordRangeReturnsEmptyForNonWordCharacter(string text, int offset)
    {
        Assert.Equal(DocumentTextRange.Empty, MarkdownWordNavigator.GetWordRange(text, offset));
    }
}
