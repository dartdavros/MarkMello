using MarkMello.Domain;
using MarkMello.Infrastructure.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownAutolinkRenderingTests
{
    [Fact]
    public void RenderConvertsAngleBracketUrlToLinkInline()
    {
        const string markdown = "- <https://docs.microsoft.com/en-gb/powershell/>";
        var renderer = new MarkdigMarkdownDocumentRenderer();

        var document = renderer.Render(markdown);

        var list = Assert.IsType<MarkdownListBlock>(Assert.Single(document.Blocks));
        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(list.Items[0].Blocks));
        var link = Assert.IsType<MarkdownLinkInline>(Assert.Single(paragraph.Inlines));
        Assert.Equal("https://docs.microsoft.com/en-gb/powershell/", link.Url);
        Assert.Equal("https://docs.microsoft.com/en-gb/powershell/", Assert.IsType<MarkdownTextInline>(Assert.Single(link.Inlines)).Text);
    }

    [Fact]
    public void RenderConvertsAngleBracketEmailToMailtoLinkInline()
    {
        const string markdown = "Contact <user@example.com>";
        var renderer = new MarkdigMarkdownDocumentRenderer();

        var document = renderer.Render(markdown);

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        Assert.Collection(
            paragraph.Inlines,
            inline => Assert.Equal("Contact ", Assert.IsType<MarkdownTextInline>(inline).Text),
            inline =>
            {
                var link = Assert.IsType<MarkdownLinkInline>(inline);
                Assert.Equal("mailto:user@example.com", link.Url);
                Assert.Equal("user@example.com", Assert.IsType<MarkdownTextInline>(Assert.Single(link.Inlines)).Text);
            });
    }
}
