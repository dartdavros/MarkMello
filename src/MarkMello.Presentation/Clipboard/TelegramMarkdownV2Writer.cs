using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Clipboard;

internal static class TelegramMarkdownV2Writer
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
            AppendBlockWithSeparator(builder, blocks[index], $"b{index}", context, ref appended, "\n\n");
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
            AppendBlockWithSeparator(builder, blocks[index], $"{path}.b{index}", context, ref appended, "\n\n");
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
        if (!AppendBlock(blockBuilder, block, path, context))
        {
            return;
        }

        MarkdownClipboardTextHelpers.TrimTrailingLineBreaks(blockBuilder, maxAllowed: 0);
        if (blockBuilder.Length == 0)
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
            MarkdownHeadingBlock heading => AppendHeading(builder, heading, path, context),
            MarkdownParagraphBlock paragraph => AppendInlineFragment(builder, paragraph.Inlines, path, context),
            MarkdownQuoteBlock quote => AppendQuote(builder, quote, path, context),
            MarkdownListBlock list => AppendList(builder, list, path, context),
            MarkdownCodeBlock code => AppendCodeBlock(builder, code, path, context),
            MarkdownTableBlock table => AppendTable(builder, table, path, context),
            MarkdownImageBlock image => AppendImageBlock(builder, image, context),
            MarkdownDiagramBlock diagram => AppendDiagramBlock(builder, diagram, context),
            MarkdownHorizontalRuleBlock => false,
            _ => AppendFallbackBlock(builder, block, path, context)
        };

    private static bool AppendHeading(
        StringBuilder builder,
        MarkdownHeadingBlock heading,
        string path,
        MarkdownSelectionFormatContext context)
    {
        var content = new StringBuilder();
        if (!AppendInlineFragment(content, heading.Inlines, path, context))
        {
            return false;
        }

        builder.Append('*');
        builder.Append(content);
        builder.Append('*');
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

        var lines = inner.ToString().Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append('>');
            if (lines[index].Length > 0)
            {
                builder.Append(' ');
                builder.Append(lines[index]);
            }
        }

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
            var marker = MarkdownClipboardTextHelpers.GetListMarkerText(list, itemIndex);
            AppendTextFragment(itemBuilder, $"{path}.i{itemIndex}.m", marker, context, MarkdownV2Escaper.EscapeText);

            var content = new StringBuilder();
            AppendNestedBlocks(content, list.Items[itemIndex].Blocks, $"{path}.i{itemIndex}", context);
            MarkdownClipboardTextHelpers.TrimTrailingLineBreaks(content, maxAllowed: 0);

            if (content.Length > 0)
            {
                itemBuilder.Append(content);
            }

            MarkdownClipboardTextHelpers.TrimTrailingLineBreaks(itemBuilder, maxAllowed: 0);
            if (itemBuilder.Length == 0)
            {
                continue;
            }

            if (appended)
            {
                builder.Append('\n');
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

        builder.Append("```");
        builder.Append(MarkdownClipboardTextHelpers.GetCodeLanguage(block.Info));
        builder.Append('\n');
        builder.Append(MarkdownV2Escaper.EscapeCode(code));
        if (!code.EndsWith('\n'))
        {
            builder.Append('\n');
        }
        builder.Append("```");
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
            builder.Append('\n');
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
                builder.Append(" · ");
            }
            builder.Append(cell);
            appended = true;
        }

        return appended;
    }

    private static bool AppendImageBlock(StringBuilder builder, MarkdownImageBlock image, MarkdownSelectionFormatContext context)
    {
        if (context.IsSelection)
        {
            return false;
        }

        return AppendImage(builder, image.Url, image.AltText, image.Title, null);
    }

    private static bool AppendDiagramBlock(StringBuilder builder, MarkdownDiagramBlock diagram, MarkdownSelectionFormatContext context)
    {
        if (context.IsSelection || string.IsNullOrEmpty(diagram.Source))
        {
            return false;
        }

        var language = diagram.Kind == MarkdownDiagramKind.Mermaid
            ? "mermaid"
            : diagram.Kind.ToString().ToLowerInvariant();

        builder.Append("```");
        builder.Append(language);
        builder.Append('\n');
        builder.Append(MarkdownV2Escaper.EscapeCode(diagram.Source));
        if (!diagram.Source.EndsWith('\n'))
        {
            builder.Append('\n');
        }
        builder.Append("```");
        return true;
    }

    private static bool AppendFallbackBlock(
        StringBuilder builder,
        MarkdownBlock block,
        string path,
        MarkdownSelectionFormatContext context)
        => AppendTextFragment(
            builder,
            path,
            MarkdownDocumentTextMap.ExtractPlainText(block),
            context,
            MarkdownV2Escaper.EscapeText);

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
                AppendEscapedSlice(builder, text.Text, selectedRange, MarkdownV2Escaper.EscapeText);
                return;

            case MarkdownStrongInline strong:
                AppendWrappedInlines(builder, strong.Inlines, selectedRange, '*', '*');
                return;

            case MarkdownEmphasisInline emphasis:
                AppendWrappedInlines(builder, emphasis.Inlines, selectedRange, '_', '_');
                return;

            case MarkdownCodeInline code:
                AppendInlineCode(builder, code.Code, selectedRange);
                return;

            case MarkdownImageInline image:
                AppendImageInline(builder, image, selectedRange);
                return;

            case MarkdownLinkInline link:
                AppendLinkInline(builder, link, selectedRange);
                return;

            case MarkdownLineBreakInline:
                builder.Append('\n');
                return;
        }
    }

    private static void AppendWrappedInlines(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        DocumentTextRange selectedRange,
        char prefix,
        char suffix)
    {
        var content = new StringBuilder();
        AppendInlines(content, inlines, selectedRange);
        if (content.Length == 0)
        {
            return;
        }

        builder.Append(prefix);
        builder.Append(content);
        builder.Append(suffix);
    }

    private static void AppendInlineCode(StringBuilder builder, string code, DocumentTextRange selectedRange)
    {
        var selected = MarkdownClipboardTextHelpers.Slice(code, selectedRange);
        if (selected.Length == 0)
        {
            return;
        }

        builder.Append('`');
        builder.Append(MarkdownV2Escaper.EscapeCode(selected));
        builder.Append('`');
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
        if (label.Length == 0)
        {
            return;
        }

        AppendImage(builder, image.Url, label, image.Title, selectedRange);
    }

    private static bool AppendImage(
        StringBuilder builder,
        string url,
        string? altText,
        string? title,
        DocumentTextRange? selectedRange)
    {
        var label = !string.IsNullOrWhiteSpace(altText)
            ? altText
            : !string.IsNullOrWhiteSpace(title)
                ? title
                : MarkdownClipboardTextHelpers.IsDataUri(url) ? "image" : url;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var selectedLabel = selectedRange.HasValue ? MarkdownClipboardTextHelpers.Slice(label, selectedRange.Value) : label;
        if (selectedLabel.Length == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(url) || MarkdownClipboardTextHelpers.IsDataUri(url))
        {
            builder.Append(MarkdownV2Escaper.EscapeText(selectedLabel));
        }
        else
        {
            AppendLink(builder, selectedLabel, url);
        }

        return true;
    }

    private static void AppendLink(StringBuilder builder, string label, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            builder.Append(MarkdownV2Escaper.EscapeText(label));
            return;
        }

        builder.Append('[');
        builder.Append(MarkdownV2Escaper.EscapeText(label));
        builder.Append("](");
        builder.Append(MarkdownV2Escaper.EscapeUrl(url));
        builder.Append(')');
    }

    private static bool AppendTextFragment(
        StringBuilder builder,
        string path,
        string text,
        MarkdownSelectionFormatContext context,
        Func<string, string> escape)
    {
        if (!context.TryGetFragmentText(path, text, out var selectedText))
        {
            return false;
        }

        builder.Append(escape(selectedText));
        return true;
    }

    private static void AppendEscapedSlice(
        StringBuilder builder,
        string text,
        DocumentTextRange selectedRange,
        Func<string, string> escape)
    {
        var selected = MarkdownClipboardTextHelpers.Slice(text, selectedRange);
        if (selected.Length > 0)
        {
            builder.Append(escape(selected));
        }
    }
}
