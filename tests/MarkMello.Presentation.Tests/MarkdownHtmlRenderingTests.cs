using MarkMello.Domain;
using MarkMello.Infrastructure.Images;
using MarkMello.Infrastructure.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownHtmlRenderingTests
{
    private const string TinyPngDataUri = "data:image/png;base64,AQIDBA==";

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
            inline =>
            {
                var text = Assert.IsType<MarkdownTextInline>(inline);
                Assert.Equal(" ", text.Text);
            },
            inline =>
            {
                var image = Assert.IsType<MarkdownImageInline>(inline);
                Assert.Equal("GitHub License", image.AltText);
            },
            inline =>
            {
                var text = Assert.IsType<MarkdownTextInline>(inline);
                Assert.Equal(" ", text.Text);
            },
            inline =>
            {
                var image = Assert.IsType<MarkdownImageInline>(inline);
                Assert.Equal("GitHub Repo stars", image.AltText);
            });
    }

    [Fact]
    public void RenderPreservesReferenceStyleDataUriImageInsideMixedParagraph()
    {
        var markdown = $"""
Text before ![][image1] and after.

[image1]: <{TinyPngDataUri}>
""";

        var renderer = new MarkdigMarkdownDocumentRenderer();

        var document = renderer.Render(markdown);

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        Assert.Collection(
            paragraph.Inlines,
            inline => Assert.Equal("Text before ", Assert.IsType<MarkdownTextInline>(inline).Text),
            inline =>
            {
                var image = Assert.IsType<MarkdownImageInline>(inline);
                Assert.Equal(TinyPngDataUri, image.Url);
                Assert.Null(image.AltText);
            },
            inline => Assert.Equal(" and after.", Assert.IsType<MarkdownTextInline>(inline).Text));
    }

    [Fact]
    public async Task RenderedReferenceStyleDataUriWithPlusCanBeOpenedByImageResolver()
    {
        const string markdown = """
Text ![][image1]

[image1]: <data:image/png;base64,+w==>
""";

        var renderer = new MarkdigMarkdownDocumentRenderer();
        var resolver = new DefaultImageSourceResolver();

        var document = renderer.Render(markdown);

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        var image = Assert.IsType<MarkdownImageInline>(paragraph.Inlines[1]);
        await using var stream = await resolver.TryOpenAsync(image.Url, null, CancellationToken.None);

        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        Assert.Equal([251], copy.ToArray());
    }

    [Fact]
    public void RenderPreservesHardLineBreakAsMarkdownLineBreakInline()
    {
        const string markdown = """
Line one  
Line two
""";

        var renderer = new MarkdigMarkdownDocumentRenderer();

        var document = renderer.Render(markdown);

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        Assert.Collection(
            paragraph.Inlines,
            inline => Assert.Equal("Line one", Assert.IsType<MarkdownTextInline>(inline).Text),
            inline => Assert.IsType<MarkdownLineBreakInline>(inline),
            inline => Assert.Equal("Line two", Assert.IsType<MarkdownTextInline>(inline).Text));
    }
}
