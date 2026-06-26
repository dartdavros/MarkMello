using MarkMello.Domain;
using MarkMello.Presentation.Clipboard;

namespace MarkMello.Presentation.Tests.Clipboard;

public sealed class TelegramMarkdownFormatterTests
{
    [Fact]
    public void FormatReturnsEmptyForEmptyDocument()
    {
        var result = TelegramMarkdownFormatter.Format(RenderedMarkdownDocument.Empty);

        Assert.Equal(string.Empty, result);
    }

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
    public void FormatSelectionHtmlPreservesSelectedLinkAsAnchor()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline("See "),
                new MarkdownLinkInline([new MarkdownTextInline("docs")], "https://example.com/docs?x=1&y=2", null),
                new MarkdownTextInline(" now")
            ])
        ]);
        var textMap = MarkdownDocumentTextMap.Create(document);
        var start = textMap.Text.IndexOf("docs", StringComparison.Ordinal);

        var result = TelegramMarkdownFormatter.FormatSelectionHtml(document, new DocumentTextRange(start, start + "docs".Length));

        Assert.Equal("<a href=\"https://example.com/docs?x=1&amp;y=2\">docs</a>", result);
    }

    [Fact]
    public void FormatSelectionHtmlPreservesListLinksForRichPaste()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownListBlock(false,
            [
                new MarkdownListItem(
                [
                    new MarkdownParagraphBlock(
                    [
                        new MarkdownLinkInline(
                            [new MarkdownTextInline("Работа с геймпадами")],
                            "https://t.me/gamedev_stinger/286",
                            null)
                    ])
                ]),
                new MarkdownListItem(
                [
                    new MarkdownParagraphBlock(
                    [
                        new MarkdownLinkInline(
                            [new MarkdownTextInline("UX дизайн управления")],
                            "https://t.me/gamedev_stinger/285",
                            null)
                    ])
                ])
            ])
        ]);

        var textMap = MarkdownDocumentTextMap.Create(document);
        var result = TelegramMarkdownFormatter.FormatSelectionHtml(document, new DocumentTextRange(0, textMap.Text.Length));

        Assert.Equal(
            "• <a href=\"https://t.me/gamedev_stinger/286\">Работа с геймпадами</a><br>• <a href=\"https://t.me/gamedev_stinger/285\">UX дизайн управления</a>",
            result);
    }

    [Fact]
    public void GetSelectionLinkUrlsReturnsSelectedLinksInDocumentOrder()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownListBlock(false,
            [
                new MarkdownListItem(
                [
                    new MarkdownParagraphBlock(
                    [
                        new MarkdownLinkInline([new MarkdownTextInline("first")], "https://example.com/1", null)
                    ])
                ]),
                new MarkdownListItem(
                [
                    new MarkdownParagraphBlock(
                    [
                        new MarkdownLinkInline([new MarkdownTextInline("second")], "https://example.com/2", null)
                    ])
                ])
            ])
        ]);
        var textMap = MarkdownDocumentTextMap.Create(document);

        var result = TelegramMarkdownFormatter.GetSelectionLinkUrls(document, new DocumentTextRange(0, textMap.Text.Length));

        Assert.Equal(["https://example.com/1", "https://example.com/2"], result);
    }

    [Fact]
    public void GetSelectionLinkUrlsIgnoresLinksOutsideSelection()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownLinkInline([new MarkdownTextInline("first")], "https://example.com/1", null),
                new MarkdownTextInline(" and "),
                new MarkdownLinkInline([new MarkdownStrongInline([new MarkdownTextInline("second")])], "https://example.com/2", null)
            ])
        ]);
        var textMap = MarkdownDocumentTextMap.Create(document);
        var start = textMap.Text.IndexOf("second", StringComparison.Ordinal);

        var result = TelegramMarkdownFormatter.GetSelectionLinkUrls(document, new DocumentTextRange(start, start + "second".Length));

        Assert.Equal(["https://example.com/2"], result);
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
