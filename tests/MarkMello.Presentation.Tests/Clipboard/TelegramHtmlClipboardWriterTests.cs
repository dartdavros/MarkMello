using MarkMello.Domain;
using MarkMello.Presentation.Clipboard;

namespace MarkMello.Presentation.Tests.Clipboard;

public sealed class TelegramHtmlClipboardWriterTests
{
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
}
