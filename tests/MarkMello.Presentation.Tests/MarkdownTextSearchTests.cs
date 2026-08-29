using MarkMello.Domain;
using MarkMello.Presentation;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownTextSearchTests
{
    [Fact]
    public void FindAllMatchesCaseInsensitive()
    {
        var matches = MarkdownTextSearch.FindAll("Alpha beta ALPHA gamma", "alpha");

        Assert.Equal(
            [new DocumentTextRange(0, 5), new DocumentTextRange(11, 16)],
            matches);
    }

    [Fact]
    public void FindAllReturnsOverlappingAwareSequentialMatches()
    {
        var matches = MarkdownTextSearch.FindAll("aaaa", "aa");

        Assert.Equal(
            [new DocumentTextRange(0, 2), new DocumentTextRange(2, 4)],
            matches);
    }

    [Fact]
    public void FindAllIgnoresEmptyOrNullQuery()
    {
        Assert.Empty(MarkdownTextSearch.FindAll("text", string.Empty));
        Assert.Empty(MarkdownTextSearch.FindAll("text", null));
        Assert.Empty(MarkdownTextSearch.FindAll("text", "   "));
    }

    [Fact]
    public void FindAllReturnsEmptyForNoMatchOrEmptyText()
    {
        Assert.Empty(MarkdownTextSearch.FindAll("text", "missing"));
        Assert.Empty(MarkdownTextSearch.FindAll(string.Empty, "x"));
    }

    [Fact]
    public void FindAllFindsMatchAtVeryEndOfText()
    {
        var matches = MarkdownTextSearch.FindAll("abc def", "def");

        Assert.Equal([new DocumentTextRange(4, 7)], matches);
    }

    [Fact]
    public void NextIndexWrapsAroundToFirstMatch()
    {
        Assert.Equal(0, MarkdownTextSearch.NextIndex(-1, 3));
        Assert.Equal(1, MarkdownTextSearch.NextIndex(0, 3));
        Assert.Equal(2, MarkdownTextSearch.NextIndex(1, 3));
        Assert.Equal(0, MarkdownTextSearch.NextIndex(2, 3));
        Assert.Equal(-1, MarkdownTextSearch.NextIndex(0, 0));
    }

    [Fact]
    public void PreviousIndexWrapsAroundToLastMatch()
    {
        Assert.Equal(2, MarkdownTextSearch.PreviousIndex(0, 3));
        Assert.Equal(1, MarkdownTextSearch.PreviousIndex(2, 3));
        Assert.Equal(0, MarkdownTextSearch.PreviousIndex(1, 3));
        Assert.Equal(2, MarkdownTextSearch.PreviousIndex(-1, 3));
        Assert.Equal(-1, MarkdownTextSearch.PreviousIndex(0, 0));
    }
}
