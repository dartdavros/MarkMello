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

    [Fact]
    public void FromInlinesCreatesAtomicImageSpanForDataUriImage()
    {
        const string dataUri = "data:image/png;base64,AQIDBA==";
        var styled = MarkdownStyledText.FromInlines(
        [
            new MarkdownTextInline("Before "),
            new MarkdownImageInline(dataUri, null, null),
            new MarkdownTextInline(" after")
        ]);

        Assert.Equal("Before image after", styled.Text);
        var image = Assert.Single(styled.Images);
        Assert.Equal(new DocumentTextRange(7, 12), image.Range);
        Assert.Equal(dataUri, image.Url);
        Assert.Equal("image", image.PlaceholderText);

        var model = MarkdownDisplayLayoutModel.Create(styled);
        var imageSegment = Assert.Single(model.Segments, segment => segment.Kind == MarkdownDisplaySegmentKind.Image);
        Assert.Equal(1, imageSegment.DisplayLength);
        Assert.Equal(image.Range, imageSegment.CanonicalRange);
        Assert.Equal(image.Range.Start, model.GetCanonicalCaretForDisplayCaret(imageSegment.DisplayStart));
        Assert.Equal(image.Range.End, model.GetCanonicalCaretForDisplayCaret(imageSegment.DisplayEnd));
        Assert.Equal(imageSegment.DisplayStart, model.GetDisplayStartForCanonicalCaret(image.Range.Start + 2));
        Assert.Equal(imageSegment.DisplayEnd, model.GetDisplayEndForCanonicalCaret(image.Range.Start + 2));
    }
}
