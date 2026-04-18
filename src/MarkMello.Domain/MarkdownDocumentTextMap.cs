using System.Text;

namespace MarkMello.Domain;

/// <summary>
/// Канонический plain-text view документа для document-wide selection и copy semantics.
/// В текущей итерации покрывает весь документ как текстовый поток и даёт стабильные offset-ы
/// для simple blocks; advanced fragments будут дорабатываться в следующих slice.
/// </summary>
public sealed class MarkdownDocumentTextMap
{
    private readonly Dictionary<string, MarkdownDocumentTextFragment> _fragmentsByKey;

    private MarkdownDocumentTextMap(string text, IReadOnlyList<MarkdownDocumentTextFragment> fragments)
    {
        Text = text;
        Fragments = fragments;
        _fragmentsByKey = fragments.ToDictionary(static fragment => fragment.Key, StringComparer.Ordinal);
    }

    public static MarkdownDocumentTextMap Empty { get; } = new(string.Empty, Array.Empty<MarkdownDocumentTextFragment>());

    public string Text { get; }

    public IReadOnlyList<MarkdownDocumentTextFragment> Fragments { get; }

    public bool TryGetFragment(string key, out MarkdownDocumentTextFragment fragment)
        => _fragmentsByKey.TryGetValue(key, out fragment!);

    public string GetText(DocumentTextRange range)
    {
        if (Text.Length == 0 || range.IsEmpty)
        {
            return string.Empty;
        }

        var start = Math.Clamp(range.Start, 0, Text.Length);
        var end = Math.Clamp(range.End, start, Text.Length);
        return end <= start ? string.Empty : Text[start..end];
    }

    public static MarkdownDocumentTextMap Create(RenderedMarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Blocks.Count == 0)
        {
            return Empty;
        }

        var builder = new Builder();
        for (var index = 0; index < document.Blocks.Count; index++)
        {
            builder.AppendBlock(document.Blocks[index], $"b{index}", isTopLevel: true);
        }

        return builder.Build();
    }

    public static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        ArgumentNullException.ThrowIfNull(inlines);

        if (inlines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendPlainText(inlines, builder);
        return builder.ToString();
    }

    public static string ExtractPlainText(MarkdownBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return block switch
        {
            MarkdownHeadingBlock heading => ExtractPlainText(heading.Inlines),
            MarkdownParagraphBlock paragraph => ExtractPlainText(paragraph.Inlines),
            MarkdownQuoteBlock quote => string.Join(Environment.NewLine, quote.Blocks.Select(ExtractPlainText)),
            MarkdownListBlock list => string.Join(Environment.NewLine, list.Items.Select(static item => string.Join(Environment.NewLine, item.Blocks.Select(ExtractPlainText)))),
            MarkdownHorizontalRuleBlock => string.Empty,
            MarkdownImageBlock => string.Empty,
            MarkdownCodeBlock code => code.Code,
            MarkdownTableBlock table => ExtractPlainText(table),
            _ => block.ToString() ?? string.Empty
        };
    }

    private static string ExtractPlainText(MarkdownTableBlock table)
    {
        var lines = new List<string>();

        if (table.Header.Count > 0)
        {
            lines.Add(string.Join("\t", table.Header.Select(static cell => ExtractPlainText(cell.Inlines))));
        }

        foreach (var row in table.Rows)
        {
            lines.Add(string.Join("\t", row.Select(static cell => ExtractPlainText(cell.Inlines))));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendPlainText(IReadOnlyList<MarkdownInline> inlines, StringBuilder builder)
    {
        foreach (var inline in inlines)
        {
            AppendPlainText(inline, builder);
        }
    }

    private static void AppendPlainText(MarkdownInline inline, StringBuilder builder)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                builder.Append(text.Text);
                break;

            case MarkdownStrongInline strong:
                AppendPlainText(strong.Inlines, builder);
                break;

            case MarkdownEmphasisInline emphasis:
                AppendPlainText(emphasis.Inlines, builder);
                break;

            case MarkdownCodeInline code:
                builder.Append(code.Code);
                break;

            case MarkdownImageInline image:
                builder.Append(GetImageInlinePlainText(image));
                break;

            case MarkdownLinkInline link:
                if (link.Inlines.Count > 0)
                {
                    AppendPlainText(link.Inlines, builder);
                }
                else if (!string.IsNullOrWhiteSpace(link.Url))
                {
                    builder.Append(link.Url);
                }
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

    private sealed class Builder
    {
        private readonly StringBuilder _text = new();
        private readonly List<MarkdownDocumentTextFragment> _fragments = new();

        public MarkdownDocumentTextMap Build()
            => _text.Length == 0 && _fragments.Count == 0
                ? Empty
                : new MarkdownDocumentTextMap(_text.ToString(), _fragments);

        public void AppendBlock(MarkdownBlock block, string path, bool isTopLevel)
        {
            switch (block)
            {
                case MarkdownHeadingBlock heading:
                    AppendInlineFragment(path, MarkdownDocumentTextFragmentKind.Heading, heading.Inlines);
                    AppendBlockSeparator(doubleBreak: true);
                    return;

                case MarkdownParagraphBlock paragraph:
                    AppendInlineFragment(path, MarkdownDocumentTextFragmentKind.Paragraph, paragraph.Inlines);
                    AppendBlockSeparator(doubleBreak: isTopLevel);
                    return;

                case MarkdownQuoteBlock quote:
                    for (var index = 0; index < quote.Blocks.Count; index++)
                    {
                        AppendBlock(quote.Blocks[index], $"{path}.b{index}", isTopLevel: false);
                    }

                    if (isTopLevel)
                    {
                        AppendBlockSeparator(doubleBreak: true);
                    }
                    else
                    {
                        EnsureSingleLineBreakAtEnd();
                    }
                    return;

                case MarkdownListBlock list:
                    for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                    {
                        var item = list.Items[itemIndex];
                        for (var blockIndex = 0; blockIndex < item.Blocks.Count; blockIndex++)
                        {
                            AppendBlock(item.Blocks[blockIndex], $"{path}.i{itemIndex}.b{blockIndex}", isTopLevel: false);
                        }

                        if (itemIndex < list.Items.Count - 1)
                        {
                            EnsureSingleLineBreakAtEnd();
                        }
                    }

                    AppendBlockSeparator(doubleBreak: true);
                    return;

                case MarkdownHorizontalRuleBlock:
                    AppendBlockSeparator(doubleBreak: true);
                    return;

                case MarkdownCodeBlock code:
                    AppendTextFragment(path, MarkdownDocumentTextFragmentKind.CodeBlock, code.Code);
                    AppendBlockSeparator(doubleBreak: true);
                    return;

                case MarkdownTableBlock table:
                    AppendTable(table, path);
                    AppendBlockSeparator(doubleBreak: true);
                    return;

                case MarkdownImageBlock:
                    // Image blocks participate in vertical rhythm but not in
                    // the text stream. Copying "para above, image, para below"
                    // yields the two paragraphs joined by a paragraph break.
                    AppendBlockSeparator(doubleBreak: true);
                    return;

                default:
                    var fallbackText = ExtractPlainText(block);
                    AppendTextFragment(path, MarkdownDocumentTextFragmentKind.Fallback, fallbackText);
                    AppendBlockSeparator(doubleBreak: true);
                    return;
            }
        }

        private void AppendTable(MarkdownTableBlock table, string path)
        {
            if (table.Header.Count > 0)
            {
                AppendTableRow(table.Header, $"{path}.h");
            }

            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                if (_text.Length > 0 && _text[^1] != '\n')
                {
                    _text.Append('\n');
                }

                AppendTableRow(table.Rows[rowIndex], $"{path}.r{rowIndex}.c");
            }
        }

        private void AppendTableRow(IReadOnlyList<MarkdownTableCell> cells, string pathPrefix)
        {
            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                if (cellIndex > 0)
                {
                    _text.Append('\t');
                }

                AppendInlineFragment($"{pathPrefix}{cellIndex}", MarkdownDocumentTextFragmentKind.TableCell, cells[cellIndex].Inlines);
            }
        }

        private void AppendInlineFragment(string key, MarkdownDocumentTextFragmentKind kind, IReadOnlyList<MarkdownInline> inlines)
            => AppendTextFragment(key, kind, ExtractPlainText(inlines));

        private void AppendTextFragment(string key, MarkdownDocumentTextFragmentKind kind, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var start = _text.Length;
            _text.Append(text);
            var range = new DocumentTextRange(start, _text.Length);
            _fragments.Add(new MarkdownDocumentTextFragment(key, kind, range, text));
        }

        private void AppendBlockSeparator(bool doubleBreak)
        {
            if (_text.Length == 0)
            {
                return;
            }

            if (doubleBreak)
            {
                TrimTrailingLineBreaks(maxAllowed: 0);
                _text.Append('\n');
                _text.Append('\n');
            }
            else
            {
                EnsureSingleLineBreakAtEnd();
            }
        }

        private void EnsureSingleLineBreakAtEnd()
        {
            if (_text.Length == 0)
            {
                return;
            }

            TrimTrailingLineBreaks(maxAllowed: 0);
            _text.Append('\n');
        }

        private void TrimTrailingLineBreaks(int maxAllowed)
        {
            var trailing = 0;
            for (var index = _text.Length - 1; index >= 0 && _text[index] == '\n'; index--)
            {
                trailing++;
            }

            while (trailing > maxAllowed)
            {
                _text.Length--;
                trailing--;
            }
        }
    }
}

public sealed record MarkdownDocumentTextFragment(
    string Key,
    MarkdownDocumentTextFragmentKind Kind,
    DocumentTextRange Range,
    string Text);

public enum MarkdownDocumentTextFragmentKind
{
    Heading,
    Paragraph,
    CodeBlock,
    TableCell,
    Fallback
}
