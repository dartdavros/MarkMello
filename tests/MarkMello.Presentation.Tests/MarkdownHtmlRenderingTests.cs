using MarkMello.Domain;
using MarkMello.Infrastructure.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownHtmlRenderingTests
{
    [Fact]
    public void RenderConvertsPictureWrappedImgIntoSizedImageBlock()
    {
        const string markdown = """
<picture>
  <img src="assets/mark.svg" alt="Skaro" width="60" />
</picture>
""";

        var renderer = new MarkdigMarkdownDocumentRenderer();

        var document = renderer.Render(markdown);

        var image = Assert.IsType<MarkdownImageBlock>(Assert.Single(document.Blocks));
        Assert.Equal("assets/mark.svg", image.Url);
        Assert.Equal("Skaro", image.AltText);
        Assert.Equal(60d, image.Width);
        Assert.Null(image.Height);
    }

    [Fact]
    public void RenderConvertsStandaloneRemoteBadgeImageIntoImageBlock()
    {
        const string markdown = "![GitHub Release](https://img.shields.io/github/v/release/skarodev/skaro)";

        var renderer = new MarkdigMarkdownDocumentRenderer();

        var document = renderer.Render(markdown);

        var image = Assert.IsType<MarkdownImageBlock>(Assert.Single(document.Blocks));
        Assert.Equal("https://img.shields.io/github/v/release/skarodev/skaro", image.Url);
        Assert.Equal("GitHub Release", image.AltText);
    }

    [Fact]
    public void RenderPreservesBadgeParagraphAsImageInlines()
    {
        const string markdown = """
![GitHub Release](https://img.shields.io/github/v/release/skarodev/skaro)
![GitHub License](https://img.shields.io/github/license/skarodev/skaro?style=flat)
![GitHub Repo stars](https://img.shields.io/github/stars/skarodev/skaro?style=flat)
""";

        var renderer = new MarkdigMarkdownDocumentRenderer();

        var document = renderer.Render(markdown);

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        Assert.Collection(
            paragraph.Inlines,
            inline =>
            {
                var image = Assert.IsType<MarkdownImageInline>(inline);
                Assert.Equal("GitHub Release", image.AltText);
            },
            inline => Assert.IsType<MarkdownLineBreakInline>(inline),
            inline =>
            {
                var image = Assert.IsType<MarkdownImageInline>(inline);
                Assert.Equal("GitHub License", image.AltText);
            },
            inline => Assert.IsType<MarkdownLineBreakInline>(inline),
            inline =>
            {
                var image = Assert.IsType<MarkdownImageInline>(inline);
                Assert.Equal("GitHub Repo stars", image.AltText);
            });
    }
}
