using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownImageViewTests
{
    [Fact]
    public void ResolveMaxWidthClampsRequestedWidthToViewerWidth()
    {
        var maxWidth = MarkdownImageView.ResolveMaxWidth(1920, 656);

        Assert.Equal(656d, maxWidth);
    }

    [Fact]
    public void ResolveMaxWidthPreservesSmallerRequestedWidth()
    {
        var maxWidth = MarkdownImageView.ResolveMaxWidth(320, 656);

        Assert.Equal(320d, maxWidth);
    }

    [Fact]
    public void ResolveMaxWidthFallsBackToViewerWidthWhenHtmlWidthMissing()
    {
        var maxWidth = MarkdownImageView.ResolveMaxWidth(null, 656);

        Assert.Equal(656d, maxWidth);
    }
}
