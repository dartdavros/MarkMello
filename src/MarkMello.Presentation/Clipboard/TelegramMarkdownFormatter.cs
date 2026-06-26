using System.Globalization;
using System.Net;
using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Clipboard;

public static class TelegramMarkdownFormatter
{
    public static string Format(RenderedMarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Blocks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendTopLevelBlocks(builder, document.Blocks, FormatContext.ForDocument());
        return builder.ToString();
    }

    public static string FormatSelection(RenderedMarkdownDocument document, DocumentTextRange selectionRange)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Blocks.Count == 0 || selectionRange.IsEmpty)
        {
            return string.Empty;
        }

        var textMap = MarkdownDocumentTextMap.Create(document);
        if (textMap.Text.Length == 0)
        {
            return string.Empty;
        }

        var start = Math.Clamp(selectionRange.Start, 0, textMap.Text.Length);
        var end = Math.Clamp(selectionRange.End, start, textMap.Text.Length);
        if (end <= start)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendTopLevelBlocks(builder, document.Blocks, FormatContext.ForSelection(textMap, new DocumentTextRange(start, end)));
        return builder.ToString();
    }

    public static string FormatSelectionHtml(RenderedMarkdownDocument document, DocumentTextRange selectionRange)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Blocks.Count == 0 || selectionRange.IsEmpty)
        {
            return string.Empty;
        }

        var textMap = MarkdownDocumentTextMap.Create(document);
        if (textMap.Text.Length == 0)
        {
            return string.Empty;
        }

        var start = Math.Clamp(selectionRange.Start, 0, textMap.Text.Length);
        var end = Math.Clamp(selectionRange.End, start, textMap.Text.Length);
        if (end <= start)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendTopLevelHtmlBlocks(builder, document.Blocks, FormatContext.ForSelection(textMap, new DocumentTextRange(start, end)));
        return builder.ToString();
    }

    private static bool AppendTopLevelBlocks(
        StringBuilder builder,
        IReadOnlyList<MarkdownBlock> blocks,
        FormatContext context)
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
        FormatContext context)
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
        FormatContext context,
        ref bool appended,
        string separator)
    {
        var blockBuilder = new StringBuilder();
        if (!AppendBlock(blockBuilder, block, path, context))
        {
            return;
        }

        TrimTrailingLineBreaks(blockBuilder, maxAllowed: 0);
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
        FormatContext context)
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
        FormatContext context)
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
        FormatContext context)
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
        FormatContext context)
    {
        var appended = false;
        for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
        {
            var itemBuilder = new StringBuilder();
            var marker = GetListMarkerText(list, itemIndex);
            AppendTextFragment(itemBuilder, $"{path}.i{itemIndex}.m", marker, context, EscapeText);

            var content = new StringBuilder();
            AppendNestedBlocks(content, list.Items[itemIndex].Blocks, $"{path}.i{itemIndex}", context);
            TrimTrailingLineBreaks(content, maxAllowed: 0);

            if (content.Length > 0)
            {
                itemBuilder.Append(content);
            }

            TrimTrailingLineBreaks(itemBuilder, maxAllowed: 0);
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
        FormatContext context)
    {
        if (!TryGetFragmentText(path, block.Code, context, out var code))
        {
            return false;
        }

        builder.Append("```");
        builder.Append(GetCodeLanguage(block.Info));
        builder.Append('\n');
        builder.Append(EscapeCode(code));
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
        FormatContext context)
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
        FormatContext context,
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
        FormatContext context)
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

    private static bool AppendImageBlock(StringBuilder builder, MarkdownImageBlock image, FormatContext context)
    {
        if (context.IsSelection)
        {
            return false;
        }

        return AppendImage(builder, image.Url, image.AltText, image.Title, null);
    }

    private static bool AppendDiagramBlock(StringBuilder builder, MarkdownDiagramBlock diagram, FormatContext context)
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
        builder.Append(EscapeCode(diagram.Source));
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
        FormatContext context)
        => AppendTextFragment(builder, path, MarkdownDocumentTextMap.ExtractPlainText(block), context, EscapeText);

    private static bool AppendInlineFragment(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        string path,
        FormatContext context)
    {
        if (!TryGetFragmentLocalRange(path, context, out var localRange))
        {
            return false;
        }

        var before = builder.Length;
        AppendInlines(builder, inlines, localRange);
        return builder.Length > before;
    }

    private static bool AppendTopLevelHtmlBlocks(
        StringBuilder builder,
        IReadOnlyList<MarkdownBlock> blocks,
        FormatContext context)
    {
        var appended = false;
        for (var index = 0; index < blocks.Count; index++)
        {
            AppendHtmlBlockWithSeparator(builder, blocks[index], $"b{index}", context, ref appended, "<br><br>");
        }

        return appended;
    }

    private static bool AppendNestedHtmlBlocks(
        StringBuilder builder,
        IReadOnlyList<MarkdownBlock> blocks,
        string path,
        FormatContext context)
    {
        var appended = false;
        for (var index = 0; index < blocks.Count; index++)
        {
            AppendHtmlBlockWithSeparator(builder, blocks[index], $"{path}.b{index}", context, ref appended, "<br><br>");
        }

        return appended;
    }

    private static void AppendHtmlBlockWithSeparator(
        StringBuilder builder,
        MarkdownBlock block,
        string path,
        FormatContext context,
        ref bool appended,
        string separator)
    {
        var blockBuilder = new StringBuilder();
        if (!AppendHtmlBlock(blockBuilder, block, path, context) || blockBuilder.Length == 0)
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

    private static bool AppendHtmlBlock(
        StringBuilder builder,
        MarkdownBlock block,
        string path,
        FormatContext context)
        => block switch
        {
            MarkdownHeadingBlock heading => AppendHtmlWrappedInlineFragment(builder, heading.Inlines, path, context, "strong"),
            MarkdownParagraphBlock paragraph => AppendHtmlInlineFragment(builder, paragraph.Inlines, path, context),
            MarkdownQuoteBlock quote => AppendHtmlQuote(builder, quote, path, context),
            MarkdownListBlock list => AppendHtmlList(builder, list, path, context),
            MarkdownCodeBlock code => AppendHtmlCodeBlock(builder, code, path, context),
            MarkdownTableBlock table => AppendHtmlTable(builder, table, path, context),
            _ => AppendHtmlTextFragment(builder, path, MarkdownDocumentTextMap.ExtractPlainText(block), context)
        };

    private static bool AppendHtmlWrappedInlineFragment(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        string path,
        FormatContext context,
        string tagName)
    {
        var content = new StringBuilder();
        if (!AppendHtmlInlineFragment(content, inlines, path, context))
        {
            return false;
        }

        builder.Append('<');
        builder.Append(tagName);
        builder.Append('>');
        builder.Append(content);
        builder.Append("</");
        builder.Append(tagName);
        builder.Append('>');
        return true;
    }

    private static bool AppendHtmlQuote(
        StringBuilder builder,
        MarkdownQuoteBlock quote,
        string path,
        FormatContext context)
    {
        var inner = new StringBuilder();
        if (!AppendNestedHtmlBlocks(inner, quote.Blocks, path, context))
        {
            return false;
        }

        builder.Append("&gt; ");
        builder.Append(inner.Replace("<br>", "<br>&gt; "));
        return true;
    }

    private static bool AppendHtmlList(
        StringBuilder builder,
        MarkdownListBlock list,
        string path,
        FormatContext context)
    {
        var appended = false;
        for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
        {
            var itemBuilder = new StringBuilder();
            AppendHtmlTextFragment(itemBuilder, $"{path}.i{itemIndex}.m", GetListMarkerText(list, itemIndex), context);
            AppendNestedHtmlBlocks(itemBuilder, list.Items[itemIndex].Blocks, $"{path}.i{itemIndex}", context);
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

    private static bool AppendHtmlCodeBlock(
        StringBuilder builder,
        MarkdownCodeBlock block,
        string path,
        FormatContext context)
    {
        if (!TryGetFragmentText(path, block.Code, context, out var code))
        {
            return false;
        }

        builder.Append("<pre><code>");
        builder.Append(HtmlEncode(code));
        builder.Append("</code></pre>");
        return true;
    }

    private static bool AppendHtmlTable(
        StringBuilder builder,
        MarkdownTableBlock table,
        string path,
        FormatContext context)
    {
        var appended = false;
        if (table.Header.Count > 0)
        {
            AppendHtmlTableRowWithSeparator(builder, table.Header, $"{path}.h", context, ref appended);
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            AppendHtmlTableRowWithSeparator(builder, table.Rows[rowIndex], $"{path}.r{rowIndex}.c", context, ref appended);
        }

        return appended;
    }

    private static void AppendHtmlTableRowWithSeparator(
        StringBuilder builder,
        IReadOnlyList<MarkdownTableCell> cells,
        string pathPrefix,
        FormatContext context,
        ref bool appended)
    {
        var row = new StringBuilder();
        if (!AppendHtmlTableRow(row, cells, pathPrefix, context))
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

    private static bool AppendHtmlTableRow(
        StringBuilder builder,
        IReadOnlyList<MarkdownTableCell> cells,
        string pathPrefix,
        FormatContext context)
    {
        var appended = false;
        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            var cell = new StringBuilder();
            if (!AppendHtmlInlineFragment(cell, cells[cellIndex].Inlines, $"{pathPrefix}{cellIndex}", context))
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

    private static bool AppendHtmlInlineFragment(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        string path,
        FormatContext context)
    {
        if (!TryGetFragmentLocalRange(path, context, out var localRange))
        {
            return false;
        }

        var before = builder.Length;
        AppendHtmlInlines(builder, inlines, localRange);
        return builder.Length > before;
    }

    private static void AppendHtmlInlines(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        DocumentTextRange? selectedRange)
    {
        var offset = 0;
        foreach (var inline in inlines)
        {
            var length = GetPlainTextLength(inline);
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
                AppendHtmlInline(
                    builder,
                    inline,
                    new DocumentTextRange(
                        localRange.Start - inlineRange.Start,
                        localRange.End - inlineRange.Start));
            }

            offset += length;
        }
    }

    private static void AppendHtmlInline(StringBuilder builder, MarkdownInline inline, DocumentTextRange selectedRange)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                AppendHtmlEscapedSlice(builder, text.Text, selectedRange);
                return;

            case MarkdownStrongInline strong:
                AppendHtmlWrappedInlines(builder, strong.Inlines, selectedRange, "strong");
                return;

            case MarkdownEmphasisInline emphasis:
                AppendHtmlWrappedInlines(builder, emphasis.Inlines, selectedRange, "em");
                return;

            case MarkdownCodeInline code:
                AppendHtmlCodeInline(builder, code.Code, selectedRange);
                return;

            case MarkdownImageInline image:
                AppendHtmlImageInline(builder, image, selectedRange);
                return;

            case MarkdownLinkInline link:
                AppendHtmlLinkInline(builder, link, selectedRange);
                return;

            case MarkdownLineBreakInline:
                builder.Append("<br>");
                return;
        }
    }

    private static void AppendHtmlWrappedInlines(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        DocumentTextRange selectedRange,
        string tagName)
    {
        var content = new StringBuilder();
        AppendHtmlInlines(content, inlines, selectedRange);
        if (content.Length == 0)
        {
            return;
        }

        builder.Append('<');
        builder.Append(tagName);
        builder.Append('>');
        builder.Append(content);
        builder.Append("</");
        builder.Append(tagName);
        builder.Append('>');
    }

    private static void AppendHtmlCodeInline(StringBuilder builder, string code, DocumentTextRange selectedRange)
    {
        var selected = Slice(code, selectedRange);
        if (selected.Length == 0)
        {
            return;
        }

        builder.Append("<code>");
        builder.Append(HtmlEncode(selected));
        builder.Append("</code>");
    }

    private static void AppendHtmlLinkInline(StringBuilder builder, MarkdownLinkInline link, DocumentTextRange selectedRange)
    {
        var label = link.Inlines.Count > 0
            ? ExtractPlainText(link.Inlines)
            : link.Url;
        var selectedLabel = Slice(label, selectedRange);
        if (selectedLabel.Length == 0)
        {
            return;
        }

        AppendHtmlLink(builder, selectedLabel, link.Url);
    }

    private static void AppendHtmlImageInline(StringBuilder builder, MarkdownImageInline image, DocumentTextRange selectedRange)
    {
        var label = GetImageInlinePlainText(image);
        var selectedLabel = Slice(label, selectedRange);
        if (selectedLabel.Length == 0)
        {
            return;
        }

        AppendHtmlLink(builder, selectedLabel, image.Url);
    }

    private static bool AppendHtmlTextFragment(
        StringBuilder builder,
        string path,
        string text,
        FormatContext context)
    {
        if (!TryGetFragmentText(path, text, context, out var selectedText))
        {
            return false;
        }

        builder.Append(HtmlEncode(selectedText));
        return true;
    }

    private static void AppendHtmlEscapedSlice(StringBuilder builder, string text, DocumentTextRange selectedRange)
    {
        var selected = Slice(text, selectedRange);
        if (selected.Length > 0)
        {
            builder.Append(HtmlEncode(selected));
        }
    }

    private static void AppendHtmlLink(StringBuilder builder, string label, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            builder.Append(HtmlEncode(label));
            return;
        }

        builder.Append("<a href=\"");
        builder.Append(HtmlAttributeEncode(url));
        builder.Append("\">");
        builder.Append(HtmlEncode(label));
        builder.Append("</a>");
    }

    private static void AppendInlines(
        StringBuilder builder,
        IReadOnlyList<MarkdownInline> inlines,
        DocumentTextRange? selectedRange)
    {
        var offset = 0;
        foreach (var inline in inlines)
        {
            var length = GetPlainTextLength(inline);
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
                AppendEscapedSlice(builder, text.Text, selectedRange, EscapeText);
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
        var selected = Slice(code, selectedRange);
        if (selected.Length == 0)
        {
            return;
        }

        builder.Append('`');
        builder.Append(EscapeCode(selected));
        builder.Append('`');
    }

    private static void AppendLinkInline(StringBuilder builder, MarkdownLinkInline link, DocumentTextRange selectedRange)
    {
        var label = link.Inlines.Count > 0
            ? ExtractPlainText(link.Inlines)
            : link.Url;
        var selectedLabel = Slice(label, selectedRange);
        if (selectedLabel.Length == 0)
        {
            return;
        }

        AppendLink(builder, selectedLabel, link.Url);
    }

    private static void AppendImageInline(StringBuilder builder, MarkdownImageInline image, DocumentTextRange selectedRange)
    {
        var label = GetImageInlinePlainText(image);
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
                : url;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var selectedLabel = selectedRange.HasValue ? Slice(label, selectedRange.Value) : label;
        if (selectedLabel.Length == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            builder.Append(EscapeText(selectedLabel));
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
            builder.Append(EscapeText(label));
            return;
        }

        builder.Append('[');
        builder.Append(EscapeText(label));
        builder.Append("](");
        builder.Append(EscapeUrl(url));
        builder.Append(')');
    }

    private static bool AppendTextFragment(
        StringBuilder builder,
        string path,
        string text,
        FormatContext context,
        Func<string, string> escape)
    {
        if (!TryGetFragmentText(path, text, context, out var selectedText))
        {
            return false;
        }

        builder.Append(escape(selectedText));
        return true;
    }

    private static bool TryGetFragmentLocalRange(
        string path,
        FormatContext context,
        out DocumentTextRange? localRange)
    {
        if (!context.IsSelection)
        {
            localRange = null;
            return true;
        }

        if (context.TryGetLocalSelection(path, out _, out var selectedRange))
        {
            localRange = selectedRange;
            return true;
        }

        localRange = null;
        return false;
    }

    private static bool TryGetFragmentText(
        string path,
        string text,
        FormatContext context,
        out string selectedText)
    {
        if (!context.IsSelection)
        {
            selectedText = text;
            return selectedText.Length > 0;
        }

        if (!context.TryGetLocalSelection(path, out var fragment, out var selectedRange))
        {
            selectedText = string.Empty;
            return false;
        }

        selectedText = Slice(fragment.Text, selectedRange);
        return selectedText.Length > 0;
    }

    private static void AppendEscapedSlice(
        StringBuilder builder,
        string text,
        DocumentTextRange selectedRange,
        Func<string, string> escape)
    {
        var selected = Slice(text, selectedRange);
        if (selected.Length > 0)
        {
            builder.Append(escape(selected));
        }
    }

    private static string Slice(string text, DocumentTextRange range)
    {
        if (text.Length == 0 || range.IsEmpty)
        {
            return string.Empty;
        }

        var start = Math.Clamp(range.Start, 0, text.Length);
        var end = Math.Clamp(range.End, start, text.Length);
        return end <= start ? string.Empty : text[start..end];
    }

    private static string GetListMarkerText(MarkdownListBlock list, int itemIndex)
        => list.IsOrdered
            ? string.Create(CultureInfo.InvariantCulture, $"{itemIndex + 1}. ")
            : "• ";

    private static string GetCodeLanguage(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var character in info.TrimStart())
        {
            if (char.IsWhiteSpace(character))
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(character)
                || character == '_'
                || character == '+'
                || character == '-'
                || character == '#')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static int GetPlainTextLength(MarkdownInline inline)
        => inline switch
        {
            MarkdownTextInline text => text.Text.Length,
            MarkdownStrongInline strong => GetPlainTextLength(strong.Inlines),
            MarkdownEmphasisInline emphasis => GetPlainTextLength(emphasis.Inlines),
            MarkdownCodeInline code => code.Code.Length,
            MarkdownImageInline image => GetImageInlinePlainText(image).Length,
            MarkdownLinkInline link => link.Inlines.Count > 0
                ? GetPlainTextLength(link.Inlines)
                : link.Url.Length,
            MarkdownLineBreakInline => 1,
            _ => 0
        };

    private static int GetPlainTextLength(IReadOnlyList<MarkdownInline> inlines)
    {
        var length = 0;
        foreach (var inline in inlines)
        {
            length += GetPlainTextLength(inline);
        }

        return length;
    }

    private static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        if (inlines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            AppendPlainText(builder, inline);
        }

        return builder.ToString();
    }

    private static void AppendPlainText(StringBuilder builder, MarkdownInline inline)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                builder.Append(text.Text);
                break;

            case MarkdownStrongInline strong:
                builder.Append(ExtractPlainText(strong.Inlines));
                break;

            case MarkdownEmphasisInline emphasis:
                builder.Append(ExtractPlainText(emphasis.Inlines));
                break;

            case MarkdownCodeInline code:
                builder.Append(code.Code);
                break;

            case MarkdownImageInline image:
                builder.Append(GetImageInlinePlainText(image));
                break;

            case MarkdownLinkInline link:
                builder.Append(link.Inlines.Count > 0 ? ExtractPlainText(link.Inlines) : link.Url);
                break;

            case MarkdownLineBreakInline:
                builder.Append('\n');
                break;
        }
    }

    private static string GetImageInlinePlainText(MarkdownImageInline image)
    {
        if (!string.IsNullOrWhiteSpace(image.AltText))
        {
            return image.AltText;
        }

        if (!string.IsNullOrWhiteSpace(image.Title))
        {
            return image.Title;
        }

        return string.IsNullOrWhiteSpace(image.Url) ? "image" : image.Url;
    }

    private static string EscapeText(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (IsMarkdownV2Special(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool IsMarkdownV2Special(char character)
        => character is '\\' or '_' or '*' or '[' or ']' or '(' or ')' or '~' or '`' or '>' or '#'
            or '+' or '-' or '=' or '|' or '{' or '}' or '.' or '!';

    private static string EscapeUrl(string url)
        => url.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static string EscapeCode(string code)
        => code.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);

    private static string HtmlEncode(string value)
        => WebUtility.HtmlEncode(value);

    private static string HtmlAttributeEncode(string value)
        => WebUtility.HtmlEncode(value).Replace("\"", "&quot;", StringComparison.Ordinal);

    private static void TrimTrailingLineBreaks(StringBuilder builder, int maxAllowed)
    {
        var trailing = 0;
        for (var index = builder.Length - 1; index >= 0 && builder[index] == '\n'; index--)
        {
            trailing++;
        }

        while (trailing > maxAllowed)
        {
            builder.Length--;
            trailing--;
        }
    }

    private sealed class FormatContext
    {
        private readonly MarkdownDocumentTextMap? _textMap;
        private readonly DocumentTextRange _selectionRange;

        private FormatContext(MarkdownDocumentTextMap? textMap, DocumentTextRange selectionRange)
        {
            _textMap = textMap;
            _selectionRange = selectionRange;
        }

        public bool IsSelection => _textMap is not null;

        public static FormatContext ForDocument() => new(null, DocumentTextRange.Empty);

        public static FormatContext ForSelection(MarkdownDocumentTextMap textMap, DocumentTextRange selectionRange)
            => new(textMap, selectionRange);

        public bool TryGetLocalSelection(
            string path,
            out MarkdownDocumentTextFragment fragment,
            out DocumentTextRange localRange)
        {
            fragment = null!;
            localRange = DocumentTextRange.Empty;

            if (_textMap is null || !_textMap.TryGetFragment(path, out fragment))
            {
                return false;
            }

            var intersection = fragment.Range.Intersection(_selectionRange);
            if (intersection.IsEmpty)
            {
                return false;
            }

            localRange = new DocumentTextRange(
                intersection.Start - fragment.Range.Start,
                intersection.End - fragment.Range.Start);
            return true;
        }
    }
}
