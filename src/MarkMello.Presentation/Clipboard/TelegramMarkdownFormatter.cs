using MarkMello.Domain;

namespace MarkMello.Presentation.Clipboard;

public static class TelegramMarkdownFormatter
{
    public static string Format(RenderedMarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Blocks.Count == 0
            ? string.Empty
            : TelegramMarkdownV2Writer.Format(document.Blocks, MarkdownSelectionFormatContext.ForDocument());
    }

    public static string FormatSelection(RenderedMarkdownDocument document, DocumentTextRange selectionRange)
    {
        ArgumentNullException.ThrowIfNull(document);

        return MarkdownSelectionFormatContext.TryCreateForSelection(document, selectionRange, out var context)
            ? TelegramMarkdownV2Writer.Format(document.Blocks, context)
            : string.Empty;
    }

    public static string FormatSelectionHtml(RenderedMarkdownDocument document, DocumentTextRange selectionRange)
    {
        ArgumentNullException.ThrowIfNull(document);

        return MarkdownSelectionFormatContext.TryCreateForSelection(document, selectionRange, out var context)
            ? TelegramHtmlClipboardWriter.Format(document.Blocks, context)
            : string.Empty;
    }

    public static IReadOnlyList<string> GetSelectionLinkUrls(RenderedMarkdownDocument document, DocumentTextRange selectionRange)
    {
        ArgumentNullException.ThrowIfNull(document);

        return MarkdownSelectionFormatContext.TryCreateForSelection(document, selectionRange, out var context)
            ? MarkdownSelectionLinkCollector.Collect(document.Blocks, context)
            : Array.Empty<string>();
    }
}
