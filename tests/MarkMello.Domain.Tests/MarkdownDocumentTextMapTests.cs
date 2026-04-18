using MarkMello.Domain;

namespace MarkMello.Domain.Tests;

public sealed class MarkdownDocumentTextMapTests
{
    [Fact]
    public void CreateBuildsCanonicalPlainTextForTypicalDocument()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownHeadingBlock(1, [new MarkdownTextInline("Title")]),
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("Hello "),
                new MarkdownLinkInline([new MarkdownTextInline("docs")], "https://example.com/docs", null),
                new MarkdownTextInline("!")
            ]),
            new MarkdownListBlock(false,
            [
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("One")])]),
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("Two")])])
            ]),
            new MarkdownCodeBlock("csharp", "line1\nline2"),
            new MarkdownTableBlock(
            [
                new MarkdownTableCell([new MarkdownTextInline("A")]),
                new MarkdownTableCell([new MarkdownTextInline("B")])
            ],
            [
                [
                    new MarkdownTableCell([new MarkdownTextInline("1")]),
                    new MarkdownTableCell([new MarkdownTextInline("2")])
                ],
                [
                    new MarkdownTableCell([new MarkdownTextInline("3")]),
                    new MarkdownTableCell([new MarkdownTextInline("4")])
                ]
            ])
        ]);

        var textMap = MarkdownDocumentTextMap.Create(document);

        Assert.Equal(
            "Title\n\nHello docs!\n\n• One\n• Two\n\nline1\nline2\n\nA\tB\n1\t2\n3\t4\n\n",
            textMap.Text);

        Assert.Collection(
            textMap.Fragments,
            fragment => AssertFragment(textMap.Text, fragment, "b0", MarkdownDocumentTextFragmentKind.Heading, "Title"),
            fragment => AssertFragment(textMap.Text, fragment, "b1", MarkdownDocumentTextFragmentKind.Paragraph, "Hello docs!"),
            fragment => AssertFragment(textMap.Text, fragment, "b2.i0.m", MarkdownDocumentTextFragmentKind.ListMarker, "• "),
            fragment => AssertFragment(textMap.Text, fragment, "b2.i0.b0", MarkdownDocumentTextFragmentKind.Paragraph, "One"),
            fragment => AssertFragment(textMap.Text, fragment, "b2.i1.m", MarkdownDocumentTextFragmentKind.ListMarker, "• "),
            fragment => AssertFragment(textMap.Text, fragment, "b2.i1.b0", MarkdownDocumentTextFragmentKind.Paragraph, "Two"),
            fragment => AssertFragment(textMap.Text, fragment, "b3", MarkdownDocumentTextFragmentKind.CodeBlock, "line1\nline2"),
            fragment => AssertFragment(textMap.Text, fragment, "b4.h0", MarkdownDocumentTextFragmentKind.TableCell, "A"),
            fragment => AssertFragment(textMap.Text, fragment, "b4.h1", MarkdownDocumentTextFragmentKind.TableCell, "B"),
            fragment => AssertFragment(textMap.Text, fragment, "b4.r0.c0", MarkdownDocumentTextFragmentKind.TableCell, "1"),
            fragment => AssertFragment(textMap.Text, fragment, "b4.r0.c1", MarkdownDocumentTextFragmentKind.TableCell, "2"),
            fragment => AssertFragment(textMap.Text, fragment, "b4.r1.c0", MarkdownDocumentTextFragmentKind.TableCell, "3"),
            fragment => AssertFragment(textMap.Text, fragment, "b4.r1.c1", MarkdownDocumentTextFragmentKind.TableCell, "4"));
    }

    [Fact]
    public void GetTextReturnsContinuousSliceAcrossBlockBoundaries()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownHeadingBlock(1, [new MarkdownTextInline("Title")]),
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("Hello "),
                new MarkdownLinkInline([new MarkdownTextInline("docs")], "https://example.com/docs", null),
                new MarkdownTextInline("!")
            ]),
            new MarkdownListBlock(false,
            [
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("One")])]),
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("Two")])])
            ])
        ]);

        var textMap = MarkdownDocumentTextMap.Create(document);
        var start = textMap.Text.IndexOf("docs", StringComparison.Ordinal);
        var end = textMap.Text.IndexOf("Two", StringComparison.Ordinal) + "Two".Length;

        var selectedText = textMap.GetText(new DocumentTextRange(start, end));

        Assert.Equal("docs!\n\n• One\n• Two", selectedText);
    }

    [Fact]
    public void CreateAddsOrderedListMarkersToCanonicalText()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownListBlock(true,
            [
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("First")])]),
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("Second")])])
            ])
        ]);

        var textMap = MarkdownDocumentTextMap.Create(document);

        Assert.Equal("1. First\n2. Second\n\n", textMap.Text);
        Assert.Collection(
            textMap.Fragments,
            fragment => AssertFragment(textMap.Text, fragment, "b0.i0.m", MarkdownDocumentTextFragmentKind.ListMarker, "1. "),
            fragment => AssertFragment(textMap.Text, fragment, "b0.i0.b0", MarkdownDocumentTextFragmentKind.Paragraph, "First"),
            fragment => AssertFragment(textMap.Text, fragment, "b0.i1.m", MarkdownDocumentTextFragmentKind.ListMarker, "2. "),
            fragment => AssertFragment(textMap.Text, fragment, "b0.i1.b0", MarkdownDocumentTextFragmentKind.Paragraph, "Second"));
    }

    [Fact]
    public void ExtractPlainTextUsesLinkTextAndFallsBackToUrlWhenLabelIsMissing()
    {
        var inlines = new MarkdownInline[]
        {
            new MarkdownTextInline("Read "),
            new MarkdownLinkInline([new MarkdownTextInline("documentation")], "https://example.com/docs", null),
            new MarkdownTextInline(" or visit "),
            new MarkdownLinkInline(Array.Empty<MarkdownInline>(), "https://example.com/root", null)
        };

        var text = MarkdownDocumentTextMap.ExtractPlainText(inlines);

        Assert.Equal("Read documentation or visit https://example.com/root", text);
    }

    [Fact]
    public void TryGetFragmentReturnsFalseForUnknownKey()
    {
        var textMap = MarkdownDocumentTextMap.Create(
            new RenderedMarkdownDocument(
            [
                new MarkdownParagraphBlock([new MarkdownTextInline("Only paragraph")])
            ]));

        var found = textMap.TryGetFragment("missing", out var fragment);

        Assert.False(found);
        Assert.Equal(default, fragment);
    }

    private static void AssertFragment(
        string fullText,
        MarkdownDocumentTextFragment fragment,
        string expectedKey,
        MarkdownDocumentTextFragmentKind expectedKind,
        string expectedText)
    {
        Assert.Equal(expectedKey, fragment.Key);
        Assert.Equal(expectedKind, fragment.Kind);
        Assert.Equal(expectedText, fragment.Text);
        Assert.Equal(expectedText.Length, fragment.Range.Length);
        Assert.Equal(expectedText, fullText[fragment.Range.Start..fragment.Range.End]);
    }
}
