using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownStyledTextTests
{
    [Fact]
    public void FromInlinesCreatesSingleMergedLinkRangeForNestedLinkContent()
    {
        var styled = MarkdownStyledText.FromInlines(
        [
            new MarkdownTextInline("See "),
            new MarkdownLinkInline(
            [
                new MarkdownTextInline("docs"),
                new MarkdownStrongInline([new MarkdownTextInline("-api")])
            ],
            "https://example.com/docs",
            null),
            new MarkdownTextInline(" now")
        ]);

        Assert.Equal("See docs-api now", styled.Text);
        Assert.Single(styled.Links);
        Assert.Equal(new DocumentTextRange(4, 12), styled.Links[0].Range);
        Assert.Equal("https://example.com/docs", styled.Links[0].Url);
    }

    [Fact]
    public void FromInlinesFallsBackToUrlWhenLinkHasNoLabel()
    {
        var styled = MarkdownStyledText.FromInlines(
        [
            new MarkdownLinkInline(Array.Empty<MarkdownInline>(), "https://example.com", null)
        ]);

        Assert.Equal("https://example.com", styled.Text);
        Assert.Single(styled.Links);
        Assert.Equal(new DocumentTextRange(0, styled.Text.Length), styled.Links[0].Range);
    }
}
