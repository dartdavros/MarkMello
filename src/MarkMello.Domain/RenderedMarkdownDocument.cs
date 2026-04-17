namespace MarkMello.Domain;

/// <summary>
/// Результат markdown parse/render pipeline для native viewer M3.
/// Содержит устойчивую block/inline модель, независимую от UI framework.
/// </summary>
public sealed record RenderedMarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks)
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

public abstract record MarkdownInline;

public sealed record MarkdownTextInline(string Text) : MarkdownInline;

public sealed record MarkdownStrongInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownEmphasisInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownCodeInline(string Code) : MarkdownInline;

public sealed record MarkdownLinkInline(string Text, string Url, string? Title) : MarkdownInline;

public sealed record MarkdownLineBreakInline() : MarkdownInline;
