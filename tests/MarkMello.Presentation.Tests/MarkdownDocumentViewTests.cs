using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

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
    public void CodeBlockUsesDedicatedHorizontalScrollViewer()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownCodeBlock(string.Empty, "var veryLongIdentifierName = \"this line should scroll horizontally instead of wrapping\";")
        ]);

        var view = CreateView(document);

        var viewport = Assert.IsType<Border>(view.Content);
        var root = Assert.IsType<StackPanel>(viewport.Child);
        var codeBlock = Assert.IsType<Border>(Assert.Single(root.Children));
        var contentGrid = Assert.IsType<Grid>(codeBlock.Child);
        var body = Assert.IsType<StackPanel>(contentGrid.Children[0]);
        var copyButton = Assert.IsType<Button>(contentGrid.Children[1]);
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(body.Children));
        var scrollContent = Assert.IsType<Border>(scrollViewer.Content);

        Assert.Contains("mm-code-copy-button", copyButton.Classes);
        Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.VerticalScrollBarVisibility);
        Assert.Equal(16, scrollContent.Padding.Bottom);
        Assert.NotNull(scrollContent.Child);
    }

    [Fact]
    public void SelectionPointerPolicyIgnoresScrollBarChrome()
    {
        Assert.True(MarkdownDocumentView.IsPointerInputFromScrollBarChrome(new ScrollBar()));
        Assert.False(MarkdownDocumentView.IsPointerInputFromScrollBarChrome(new Border()));
        Assert.False(MarkdownDocumentView.IsPointerInputFromScrollBarChrome(null));
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

        var viewport = Assert.IsType<Border>(view.Content);
        var root = Assert.IsType<StackPanel>(viewport.Child);
        var fragment = Assert.IsType<MarkdownImageFlowFragment>(Assert.Single(root.Children));

        view.SelectAll();

        Assert.True(view.HasSelection);
        Assert.Contains("GitHub Release", view.SelectedText, StringComparison.Ordinal);
        Assert.Contains("GitHub License", view.SelectedText, StringComparison.Ordinal);
        Assert.Contains("GitHub Repo stars", view.SelectedText, StringComparison.Ordinal);
        Assert.False(fragment.SelectionRange.IsEmpty);
    }

    [Fact]
    public void ImageSourceResolverChangeRebuildsMixedParagraphImageFragment()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("Before "),
                new MarkdownImageInline("data:image/png;base64,AQIDBA==", null, null),
                new MarkdownTextInline(" after")
            ])
        ]);
        var resolver = new TestImageSourceResolver();
        var view = CreateView(document);

        var initialFragment = GetOnlyDocumentChild<MarkdownSelectionTextFragment>(view);
        Assert.Null(initialFragment.ImageSourceResolver);

        view.ImageSourceResolver = resolver;

        var rebuiltFragment = GetOnlyDocumentChild<MarkdownSelectionTextFragment>(view);
        Assert.Same(resolver, rebuiltFragment.ImageSourceResolver);
    }

    [Fact]
    public void ApplySearchQueryCountsMatchesCaseInsensitively()
    {
        var view = CreateView(CreateCompositeDocument());

        view.ApplySearchQuery("a");

        Assert.Equal("a", view.ActiveSearchQuery);
        Assert.Equal(GetMatchCount(view, "a"), view.MatchCount);
        Assert.Equal(0, view.MatchIndex);
    }

    [Fact]
    public void ApplySearchQueryMovesToFirstMatchAndHighlightsFragments()
    {
        var view = CreateView(CreateCompositeDocument());

        view.ApplySearchQuery("Body");

        Assert.True(view.MatchCount > 0);
        Assert.Equal(0, view.MatchIndex);
        Assert.Contains(
            GetTextFragments(view),
            fragment => fragment.SearchHighlightRanges.Any(range => range.Start > 0 && !range.IsEmpty));
    }

    [Fact]
    public void FindNextCyclesThroughMatchesAndWrapsAround()
    {
        var view = CreateView(CreateCompositeDocument());
        view.ApplySearchQuery("a");

        var count = view.MatchCount;
        var visited = new List<int>();
        for (var index = 0; index < count; index++)
        {
            visited.Add(view.MatchIndex);
            view.FindNext();
        }

        Assert.Equal(count, visited.Distinct().Count());
        Assert.Equal(0, view.MatchIndex);
    }

    [Fact]
    public void FindPreviousWrapsAroundToLastMatch()
    {
        var view = CreateView(CreateCompositeDocument());
        view.ApplySearchQuery("a");

        var count = view.MatchCount;
        view.FindPrevious();

        Assert.Equal(count - 1, view.MatchIndex);
    }

    [Fact]
    public void NoMatchesSetsIndexToMinusOne()
    {
        var view = CreateView(CreateCompositeDocument());

        view.ApplySearchQuery("zzz-no-such-text");

        Assert.Equal(0, view.MatchCount);
        Assert.Equal(-1, view.MatchIndex);
    }

    [Fact]
    public void EmptyQueryClearsSearchStateAndHighlights()
    {
        var view = CreateView(CreateCompositeDocument());
        view.ApplySearchQuery("Body");
        Assert.True(view.MatchCount > 0);

        view.ApplySearchQuery(string.Empty);

        Assert.Null(view.ActiveSearchQuery);
        Assert.Equal(0, view.MatchCount);
        Assert.Equal(-1, view.MatchIndex);
        Assert.All(GetTextFragments(view), fragment => Assert.Empty(fragment.SearchHighlightRanges));
    }

    [Fact]
    public void DocumentChangeReappliesActiveQueryToNewText()
    {
        var view = CreateView(CreateCompositeDocument());
        view.ApplySearchQuery("Body");
        var originalCount = view.MatchCount;
        Assert.True(originalCount > 0);

        view.Document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock([new MarkdownTextInline("Body")])
        ]);

        Assert.Equal("Body", view.ActiveSearchQuery);
        Assert.Equal(1, view.MatchCount);
        Assert.Equal(0, view.MatchIndex);
    }

    [Fact]
    public void ReapplyingSameQueryKeepsCurrentMatchIndex()
    {
        var view = CreateView(CreateCompositeDocument());
        view.ApplySearchQuery("a");
        view.FindNext();
        view.FindNext();
        var index = view.MatchIndex;

        view.ApplySearchQuery("a");

        Assert.Equal(index, view.MatchIndex);
    }

    private static int GetMatchCount(MarkdownDocumentView view, string query)
    {
        var viewport = Assert.IsType<Border>(view.Content);
        var root = Assert.IsType<StackPanel>(viewport.Child);
        var textMap = MarkdownDocumentTextMap.Create(view.Document!);
        return MarkdownTextSearch.FindAll(textMap.Text, query).Count;
    }

    private static IEnumerable<MarkdownSelectionTextFragment> GetTextFragments(MarkdownDocumentView view)
    {
        var viewport = Assert.IsType<Border>(view.Content);
        var root = Assert.IsType<StackPanel>(viewport.Child);
        return root.Children.OfType<MarkdownSelectionTextFragment>();
    }

    private static MarkdownDocumentView CreateView(RenderedMarkdownDocument document)
        => new()
        {
            Document = document,
            ReadingPreferences = ReadingPreferences.Default
        };

    private static T GetOnlyDocumentChild<T>(MarkdownDocumentView view)
        where T : Control
    {
        var viewport = Assert.IsType<Border>(view.Content);
        var root = Assert.IsType<StackPanel>(viewport.Child);
        return Assert.IsType<T>(Assert.Single(root.Children));
    }

    private sealed class TestImageSourceResolver : IImageSourceResolver
    {
        public Task<Stream?> TryOpenAsync(string url, string? baseDirectory, CancellationToken cancellationToken)
            => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3, 4]));
    }

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
