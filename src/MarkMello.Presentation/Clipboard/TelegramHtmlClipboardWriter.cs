using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Clipboard;

internal static class TelegramHtmlClipboardWriter
{
    public static string Format(IReadOnlyList<MarkdownBlock> blocks, MarkdownSelectionFormatContext context)
    {
        if (blocks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendTopLevelBlocks(builder, blocks, context);
        return builder.ToString();
    }

    private static bool AppendTopLevelBlocks(
        StringBuilder builder,
        IReadOnlyList<MarkdownBlock> blocks,
        MarkdownSelectionFormatContext context)
    {
        var appended = false;
        for (var index = 0; index < blocks.Count; index++)
        {
            AppendBlockWithSeparator(builder, blocks[index], $"b{index}", context, ref appended, "<br><br>");
        }

        return appended;
    }

    private static bool AppendNestedBlocks(
        StringBuilder builder,
        IReadOnlyList<MarkdownBlock> blocks,
        string path,
        MarkdownSelectionFormatContext context)
    {
        var appended = false;
        for (var index = 0; index < blocks.Count; index++)
        {
            AppendBlockWithSeparator(builder, blocks[index], $"{path}.b{index}", context, ref appended, "<br><br>");
        }

        return appended;
    }

    private static void AppendBlockWithSeparator(
        StringBuilder builder,
        MarkdownBlock block,
        string path,
        MarkdownSelectionFormatContext context,
        ref bool appended,
        string separator)
    {
        var blockBuilder = new StringBuilder();
        if (!AppendBlock(blockBuilder, block, path, context) || blockBuilder.Length == 0)
        {
            return;
        }

        if (appended)
        {
            builder.Append(separator);
        }

        builder.Append(blockBuilder);
        appended = true;
    }

    private static bool AppendBlock(
        StringBuilder builder,
        MarkdownBlock block,
        string path,
        MarkdownSelectionFormatContext context)
        => block switch
        {
            MarkdownHeadingBlock heading => AppendWrappedInlineFragment(builder, heading.Inlines, path, context, "strong"),
            MarkdownParagraphBlock paragraph => AppendInlineFragment(builder, paragraph.Inlines, path, context),
            MarkdownQuoteBlock quote => AppendQuote(builder, quote, path, context),
            MarkdownListBlock list => AppendList(builder, list, path, context),
            MarkdownCodeBlock code => AppendCodeBlock(builder, code, path, context),
            MarkdownTableBlock table => AppendTable(builder, table, path, context),
            _ => AppendTextFragment(builder, path, MarkdownDocumentTextMap.ExtractPlainText(block), context)
        };

    private static bool AppendWrappedInlineFragment(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        string path,
        MarkdownSelectionFormatContext context,
        string tagName)
    {
        var content = new StringBuilder();
        if (!AppendInlineFragment(content, inlines, path, context))
        {
            return false;
        }

        AppendTag(builder, tagName, content.ToString());
        return true;
    }

    private static bool AppendQuote(
        StringBuilder builder,
        MarkdownQuoteBlock quote,
        string path,
        MarkdownSelectionFormatContext context)
    {
        var inner = new StringBuilder();
        if (!AppendNestedBlocks(inner, quote.Blocks, path, context))
        {
            return false;
        }

        builder.Append("&gt; ");
        builder.Append(inner.Replace("<br>", "<br>&gt; "));
        return true;
    }

    private static bool AppendList(
        StringBuilder builder,
        MarkdownListBlock list,
        string path,
        MarkdownSelectionFormatContext context)
    {
        var appended = false;
        for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
        {
            var itemBuilder = new StringBuilder();
            AppendTextFragment(itemBuilder, $"{path}.i{itemIndex}.m", MarkdownClipboardTextHelpers.GetListMarkerText(list, itemIndex), context);
            AppendNestedBlocks(itemBuilder, list.Items[itemIndex].Blocks, $"{path}.i{itemIndex}", context);
            if (itemBuilder.Length == 0)
            {
                continue;
            }

            if (appended)
            {
                builder.Append("<br>");
            }
            builder.Append(itemBuilder);
            appended = true;
        }

        return appended;
    }

    private static bool AppendCodeBlock(
        StringBuilder builder,
        MarkdownCodeBlock block,
        string path,
        MarkdownSelectionFormatContext context)
    {
        if (!context.TryGetFragmentText(path, block.Code, out var code))
        {
            return false;
        }

        builder.Append("<pre><code>");
        builder.Append(HtmlClipboardEscaper.Encode(code));
        builder.Append("</code></pre>");
        return true;
    }

    private static bool AppendTable(
        StringBuilder builder,
        MarkdownTableBlock table,
        string path,
        MarkdownSelectionFormatContext context)
    {
        var appended = false;
        if (table.Header.Count > 0)
        {
            AppendTableRowWithSeparator(builder, table.Header, $"{path}.h", context, ref appended);
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            AppendTableRowWithSeparator(builder, table.Rows[rowIndex], $"{path}.r{rowIndex}.c", context, ref appended);
        }

        return appended;
    }

    private static void AppendTableRowWithSeparator(
        StringBuilder builder,
        IReadOnlyList<MarkdownTableCell> cells,
        string pathPrefix,
        MarkdownSelectionFormatContext context,
        ref bool appended)
    {
        var row = new StringBuilder();
        if (!AppendTableRow(row, cells, pathPrefix, context))
        {
            return;
        }

        if (appended)
        {
            builder.Append("<br>");
        }
        builder.Append(row);
        appended = true;
    }

    private static bool AppendTableRow(
        StringBuilder builder,
        IReadOnlyList<MarkdownTableCell> cells,
        string pathPrefix,
        MarkdownSelectionFormatContext context)
    {
        var appended = false;
        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            var cell = new StringBuilder();
            if (!AppendInlineFragment(cell, cells[cellIndex].Inlines, $"{pathPrefix}{cellIndex}", context))
            {
                continue;
            }

            if (appended)
            {
                builder.Append(" &middot; ");
            }
            builder.Append(cell);
            appended = true;
        }

        return appended;
    }

    private static bool AppendInlineFragment(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        string path,
        MarkdownSelectionFormatContext context)
    {
        if (!context.TryGetFragmentLocalRange(path, out var localRange))
        {
            return false;
        }

        var before = builder.Length;
        AppendInlines(builder, inlines, localRange);
        return builder.Length > before;
    }

    private static void AppendInlines(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        DocumentTextRange? selectedRange)
    {
        var offset = 0;
        foreach (var inline in inlines)
        {
            var length = MarkdownClipboardTextHelpers.GetPlainTextLength(inline);
            if (length == 0)
            {
                continue;
            }

            var inlineRange = new DocumentTextRange(offset, offset + length);
            var localRange = selectedRange.HasValue
                ? selectedRange.Value.Intersection(inlineRange)
                : inlineRange;

            if (!localRange.IsEmpty)
            {
                AppendInline(
                    builder,
                    inline,
                    new DocumentTextRange(
                        localRange.Start - inlineRange.Start,
                        localRange.End - inlineRange.Start));
            }

            offset += length;
        }
    }

    private static void AppendInline(StringBuilder builder, MarkdownInline inline, DocumentTextRange selectedRange)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                AppendEscapedSlice(builder, text.Text, selectedRange);
                return;

            case MarkdownStrongInline strong:
                AppendWrappedInlines(builder, strong.Inlines, selectedRange, "strong");
                return;

            case MarkdownEmphasisInline emphasis:
                AppendWrappedInlines(builder, emphasis.Inlines, selectedRange, "em");
                return;

            case MarkdownCodeInline code:
                AppendCodeInline(builder, code.Code, selectedRange);
                return;

            case MarkdownImageInline image:
                AppendImageInline(builder, image, selectedRange);
                return;

            case MarkdownLinkInline link:
                AppendLinkInline(builder, link, selectedRange);
                return;

            case MarkdownLineBreakInline:
                builder.Append("<br>");
                return;
        }
    }

    private static void AppendWrappedInlines(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        DocumentTextRange selectedRange,
        string tagName)
    {
        var content = new StringBuilder();
        AppendInlines(content, inlines, selectedRange);
        if (content.Length == 0)
        {
            return;
        }

        AppendTag(builder, tagName, content.ToString());
    }

    private static void AppendCodeInline(StringBuilder builder, string code, DocumentTextRange selectedRange)
    {
        var selected = MarkdownClipboardTextHelpers.Slice(code, selectedRange);
        if (selected.Length == 0)
        {
            return;
        }

        builder.Append("<code>");
        builder.Append(HtmlClipboardEscaper.Encode(selected));
        builder.Append("</code>");
    }

    private static void AppendLinkInline(StringBuilder builder, MarkdownLinkInline link, DocumentTextRange selectedRange)
    {
        var label = link.Inlines.Count > 0
            ? MarkdownClipboardTextHelpers.ExtractPlainText(link.Inlines)
            : link.Url;
        var selectedLabel = MarkdownClipboardTextHelpers.Slice(label, selectedRange);
        if (selectedLabel.Length == 0)
        {
            return;
        }

        AppendLink(builder, selectedLabel, link.Url);
    }

    private static void AppendImageInline(StringBuilder builder, MarkdownImageInline image, DocumentTextRange selectedRange)
    {
        var label = MarkdownClipboardTextHelpers.GetImageInlinePlainText(image);
        var selectedLabel = MarkdownClipboardTextHelpers.Slice(label, selectedRange);
        if (selectedLabel.Length == 0)
        {
            return;
        }

        AppendLink(builder, selectedLabel, image.Url);
    }

    private static bool AppendTextFragment(
        StringBuilder builder,
        string path,
        string text,
        MarkdownSelectionFormatContext context)
    {
        if (!context.TryGetFragmentText(path, text, out var selectedText))
        {
            return false;
        }

        builder.Append(HtmlClipboardEscaper.Encode(selectedText));
        return true;
    }

    private static void AppendEscapedSlice(StringBuilder builder, string text, DocumentTextRange selectedRange)
    {
        var selected = MarkdownClipboardTextHelpers.Slice(text, selectedRange);
        if (selected.Length > 0)
        {
            builder.Append(HtmlClipboardEscaper.Encode(selected));
        }
    }

    private static void AppendLink(StringBuilder builder, string label, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            builder.Append(HtmlClipboardEscaper.Encode(label));
            return;
        }

        builder.Append("<a href=\"");
        builder.Append(HtmlClipboardEscaper.AttributeEncode(url));
        builder.Append("\">");
        builder.Append(HtmlClipboardEscaper.Encode(label));
        builder.Append("</a>");
    }

    private static void AppendTag(StringBuilder builder, string tagName, string content)
    {
        builder.Append('<');
        builder.Append(tagName);
        builder.Append('>');
        builder.Append(content);
        builder.Append("</");
        builder.Append(tagName);
        builder.Append('>');
    }
}
