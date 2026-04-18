using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;
using Avalonia.Controls;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownDocumentViewTests
{
    [Fact]
    public void SelectAllReturnsCanonicalTextAcrossAllBlockTypes()
    {
        var document = CreateCompositeDocument();
        var view = CreateView(document);

        view.SelectAll();

        var expected = MarkdownDocumentTextMap.Create(document).Text;
        Assert.Equal(expected, view.SelectedText);
        Assert.True(view.HasSelection);
        Assert.Equal(0, view.SelectionStart);
        Assert.Equal(expected.Length, view.SelectionEnd);
    }

    [Fact]
    public void SelectRangeReturnsSubstringAcrossBlockBoundaries()
    {
        var document = CreateCompositeDocument();
        var textMap = MarkdownDocumentTextMap.Create(document);
        var view = CreateView(document);

        var start = textMap.Text.IndexOf("Body ", StringComparison.Ordinal);
        var end = textMap.Text.IndexOf("quoted", StringComparison.Ordinal) + "quoted".Length;
        var expectedRange = new DocumentTextRange(start, end);

        view.SelectRange(expectedRange);

        Assert.Equal(textMap.GetText(expectedRange), view.SelectedText);
        Assert.Equal(expectedRange.Start, view.SelectionStart);
        Assert.Equal(expectedRange.End, view.SelectionEnd);
    }

    [Fact]
    public void SelectRangeCanSelectInsideLinkTextWithoutBreakingContinuity()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
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
            ])
        ]);

        var textMap = MarkdownDocumentTextMap.Create(document);
        var view = CreateView(document);
        var start = textMap.Text.IndexOf("docs", StringComparison.Ordinal);
        var range = new DocumentTextRange(start, start + "docs-api".Length);

        view.SelectRange(range);

        Assert.Equal("docs-api", view.SelectedText);
        Assert.True(view.HasSelection);
    }

    [Fact]
    public void SelectRangeIncludesListMarkerWhenSelectingListItem()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownListBlock(false,
            [
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("One")])])
            ])
        ]);

        var textMap = MarkdownDocumentTextMap.Create(document);
        var view = CreateView(document);
        var range = new DocumentTextRange(0, "• One".Length);

        view.SelectRange(range);

        Assert.Equal("• One", view.SelectedText);
        Assert.Equal(textMap.GetText(range), view.SelectedText);
    }

    [Fact]
    public void ClearSelectionResetsStateAfterProgrammaticSelection()
    {
        var document = CreateCompositeDocument();
        var view = CreateView(document);

        view.SelectRange(new DocumentTextRange(1, 8));
        view.ClearSelection();

        Assert.False(view.HasSelection);
        Assert.Equal(string.Empty, view.SelectedText);
        Assert.Null(view.SelectionAnchor);
        Assert.Equal(0, view.SelectionStart);
        Assert.Equal(0, view.SelectionEnd);
    }

    [Fact]
    public void SelectRangeClampsEndToDocumentBounds()
    {
        var document = CreateCompositeDocument();
        var textMap = MarkdownDocumentTextMap.Create(document);
        var view = CreateView(document);

        view.SelectRange(new DocumentTextRange(0, textMap.Text.Length + 50));

        Assert.Equal(textMap.Text, view.SelectedText);
        Assert.Equal(0, view.SelectionStart);
        Assert.Equal(textMap.Text.Length, view.SelectionEnd);
    }

    [Fact]
    public void ViewIsKeyboardReachableForSelectionHotkeys()
    {
        var view = CreateView(CreateCompositeDocument());

        Assert.True(view.Focusable);
        Assert.True(view.IsTabStop);
    }

    [Fact]
    public void ParagraphOfBadgeImagesUsesImageFlowFragmentInsteadOfAltTextFallback()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownImageInline("https://img.shields.io/github/v/release/skarodev/skaro", "GitHub Release", null),
                new MarkdownLineBreakInline(),
                new MarkdownImageInline("https://img.shields.io/github/license/skarodev/skaro?style=flat", "GitHub License", null),
                new MarkdownLineBreakInline(),
                new MarkdownImageInline("https://img.shields.io/github/stars/skarodev/skaro?style=flat", "GitHub Repo stars", null)
            ])
        ]);

        var view = CreateView(document);

        var root = Assert.IsType<StackPanel>(view.Content);
        var fragment = Assert.IsType<MarkdownImageFlowFragment>(Assert.Single(root.Children));

        view.SelectAll();

        Assert.True(view.HasSelection);
        Assert.Contains("GitHub Release", view.SelectedText, StringComparison.Ordinal);
        Assert.Contains("GitHub License", view.SelectedText, StringComparison.Ordinal);
        Assert.Contains("GitHub Repo stars", view.SelectedText, StringComparison.Ordinal);
        Assert.False(fragment.SelectionRange.IsEmpty);
    }

    private static MarkdownDocumentView CreateView(RenderedMarkdownDocument document)
        => new()
        {
            Document = document,
            ReadingPreferences = ReadingPreferences.Default
        };

    private static RenderedMarkdownDocument CreateCompositeDocument()
        => new(
        [
            new MarkdownHeadingBlock(1, [new MarkdownTextInline("Heading")]),
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("Body "),
                new MarkdownLinkInline([new MarkdownTextInline("link")], "https://example.com", null),
                new MarkdownTextInline(" tail")
            ]),
            new MarkdownQuoteBlock(
            [
                new MarkdownParagraphBlock([new MarkdownTextInline("quoted")])
            ]),
            new MarkdownListBlock(false,
            [
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("item one")])]),
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("item two")])])
            ]),
            new MarkdownCodeBlock("csharp", "var x = 1;"),
            new MarkdownTableBlock(
                [
                    new MarkdownTableCell([new MarkdownTextInline("H1")]),
                    new MarkdownTableCell([new MarkdownTextInline("H2")])
                ],
                [
                    new MarkdownTableCell[]
                    {
                        new([new MarkdownTextInline("R1C1")]),
                        new([new MarkdownTextInline("R1C2")])
                    },
                    new MarkdownTableCell[]
                    {
                        new([new MarkdownTextInline("R2C1")]),
                        new([new MarkdownTextInline("R2C2")])
                    }
                ])
        ]);
}
