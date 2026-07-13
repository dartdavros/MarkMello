using MarkMello.Domain;
using MarkMello.Presentation.Clipboard;

namespace MarkMello.Presentation.Tests.Clipboard;

public sealed class TelegramMarkdownV2WriterTests
{
    [Fact]
    public void FormatPreservesInlineLinks()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("See "),
                new MarkdownLinkInline([new MarkdownTextInline("docs")], "https://example.com/docs", null)
            ])
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("See [docs](https://example.com/docs)", result);
    }

    [Fact]
    public void FormatDataUriImageUsesTextPlaceholderInsteadOfEmbeddingBase64Url()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("Cost "),
                new MarkdownImageInline("data:image/png;base64,AQIDBA==", null, null)
            ])
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("Cost image", result);
        Assert.DoesNotContain("AQID", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEscapesMarkdownV2TextCharacters()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock([new MarkdownTextInline("a_b *c* [x]!")])
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("a\\_b \\*c\\* \\[x\\]\\!", result);
    }

    [Fact]
    public void FormatCodeBlockUsesFencedBlockWithLanguage()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownCodeBlock("csharp", "var x = 1;\nConsole.WriteLine(x);")
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```", result);
    }

    [Fact]
    public void FormatInlineCodeEscapesBackticks()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock([new MarkdownCodeInline("a ` b")])
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("`a \\` b`", result);
    }

    [Fact]
    public void FormatUnorderedListUsesBulletMarkers()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownListBlock(false,
            [
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("one")])]),
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("two")])])
            ])
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("• one\n• two", result);
    }

    [Fact]
    public void FormatOrderedListEscapesMarkerDot()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownListBlock(true,
            [
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("one")])]),
                new MarkdownListItem([new MarkdownParagraphBlock([new MarkdownTextInline("two")])])
            ])
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("1\\. one\n2\\. two", result);
    }

    [Fact]
    public void FormatDiagramUsesMermaidFencedBlock()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownDiagramBlock(MarkdownDiagramKind.Mermaid, "graph TD\nA-->B")
        ]);

        var result = TelegramMarkdownFormatter.Format(document);

        Assert.Equal("```mermaid\ngraph TD\nA-->B\n```", result);
    }

    [Fact]
    public void FormatSelectionPreservesSelectedLink()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("See "),
                new MarkdownLinkInline([new MarkdownTextInline("docs")], "https://example.com/docs", null),
                new MarkdownTextInline(" now")
            ])
        ]);
        var textMap = MarkdownDocumentTextMap.Create(document);
        var start = textMap.Text.IndexOf("docs", StringComparison.Ordinal);

        var result = TelegramMarkdownFormatter.FormatSelection(document, new DocumentTextRange(start, start + "docs".Length));

        Assert.Equal("[docs](https://example.com/docs)", result);
    }

    [Fact]
    public void FormatSelectionSlicesAcrossTextAndLink()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("See "),
                new MarkdownLinkInline([new MarkdownTextInline("docs")], "https://example.com/docs", null),
                new MarkdownTextInline(" now")
            ])
        ]);
        var textMap = MarkdownDocumentTextMap.Create(document);
        var start = textMap.Text.IndexOf("See", StringComparison.Ordinal);
        var end = textMap.Text.IndexOf(" now", StringComparison.Ordinal);

        var result = TelegramMarkdownFormatter.FormatSelection(document, new DocumentTextRange(start, end));

        Assert.Equal("See [docs](https://example.com/docs)", result);
    }

    [Fact]
    public void FormatSelectionSlicesCodeBlockInsideFence()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownCodeBlock("csharp", "var x = 1;\nConsole.WriteLine(x);")
        ]);
        var textMap = MarkdownDocumentTextMap.Create(document);
        var start = textMap.Text.IndexOf("Console", StringComparison.Ordinal);
        var end = textMap.Text.IndexOf("(x)", StringComparison.Ordinal) + "(x)".Length;

        var result = TelegramMarkdownFormatter.FormatSelection(document, new DocumentTextRange(start, end));

        Assert.Equal("```csharp\nConsole.WriteLine(x)\n```", result);
    }
}
