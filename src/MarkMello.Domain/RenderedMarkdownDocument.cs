namespace MarkMello.Domain;

/// <summary>
/// Результат markdown parse/render pipeline для native viewer M3.
/// Содержит устойчивую block/inline модель, независимую от UI framework.
/// </summary>
/// <param name="Blocks">Плоский список блоков документа.</param>
/// <param name="BaseDirectory">
/// Директория исходного .md-файла. Используется для разрешения относительных
/// путей ресурсов (изображений). Null когда источник не имеет файловой
/// локации (например, при рендере plain-text fallback или в тестах).
/// </param>
public sealed record RenderedMarkdownDocument(
    IReadOnlyList<MarkdownBlock> Blocks,
    string? BaseDirectory = null)
{
    public static RenderedMarkdownDocument Empty { get; } = new(Array.Empty<MarkdownBlock>());

    public static RenderedMarkdownDocument PlainText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        return new RenderedMarkdownDocument(
        [
            new MarkdownParagraphBlock(
            [
                new MarkdownTextInline(text)
            ])
        ]);
    }
}

public abstract record MarkdownBlock;

public sealed record MarkdownHeadingBlock(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownParagraphBlock(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownQuoteBlock(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

public sealed record MarkdownListBlock(bool IsOrdered, IReadOnlyList<MarkdownListItem> Items) : MarkdownBlock;

public sealed record MarkdownListItem(IReadOnlyList<MarkdownBlock> Blocks);

public sealed record MarkdownHorizontalRuleBlock() : MarkdownBlock;

public sealed record MarkdownCodeBlock(string? Info, string Code) : MarkdownBlock;

public sealed record MarkdownTableBlock(
    IReadOnlyList<MarkdownTableCell> Header,
    IReadOnlyList<IReadOnlyList<MarkdownTableCell>> Rows) : MarkdownBlock;

public sealed record MarkdownTableCell(IReadOnlyList<MarkdownInline> Inlines);

/// <summary>
/// Block-level image. Emitted when a markdown source paragraph contains
/// exactly one image node (e.g. a standalone ![alt](url) line) or a block
/// of HTML whose sole meaningful content is a &lt;img&gt; tag. Rendered as
/// an own non-selectable visual, outside the document text flow and text
/// map. Alt text is shown as a caption below the image, or as a
/// placeholder when the image cannot be loaded.
/// </summary>
public sealed record MarkdownImageBlock(string Url, string? AltText, string? Title) : MarkdownBlock;

public abstract record MarkdownInline;

public sealed record MarkdownTextInline(string Text) : MarkdownInline;

public sealed record MarkdownStrongInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownEmphasisInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownCodeInline(string Code) : MarkdownInline;

public sealed record MarkdownLinkInline(IReadOnlyList<MarkdownInline> Inlines, string Url, string? Title) : MarkdownInline;

public sealed record MarkdownLineBreakInline() : MarkdownInline;
