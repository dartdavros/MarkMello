using MarkMello.Domain;
using MarkMello.Presentation.Clipboard;

namespace MarkMello.Presentation.Tests.Clipboard;

public sealed class MarkdownSelectionLinkCollectorTests
{
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
}
