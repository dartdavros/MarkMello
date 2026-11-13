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

/// <summary>
/// Zero-based source line span for a rendered markdown block.
/// Used by edit-mode scroll synchronization to map source lines to preview blocks.
/// </summary>
public readonly record struct MarkdownSourceSpan
{
    public MarkdownSourceSpan(int startLine, int endLine)
    {
        StartLine = Math.Max(0, startLine);
        EndLine = Math.Max(StartLine, endLine);
    }

    public MarkdownSourceSpan(int line)
        : this(line, line)
    {
    }

    public int StartLine { get; }

    public int EndLine { get; }
}

public abstract record MarkdownBlock
{
    public MarkdownSourceSpan? SourceSpan { get; init; }
}

public sealed record MarkdownHeadingBlock(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownParagraphBlock(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownQuoteBlock(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock
{
    public MarkdownAlertKind? AlertKind { get; init; }
}

public sealed record MarkdownListBlock(bool IsOrdered, IReadOnlyList<MarkdownListItem> Items) : MarkdownBlock;

public sealed record MarkdownListItem(IReadOnlyList<MarkdownBlock> Blocks);

public sealed record MarkdownHorizontalRuleBlock() : MarkdownBlock;

public sealed record MarkdownCodeBlock(string? Info, string Code) : MarkdownBlock;

/// <summary>
/// Block-level math formula parsed from a <c>$$</c> fenced block (Markdig
/// <c>MathBlock</c>). <see cref="Source"/> is the raw LaTeX source.
/// Rendered as a centered display formula; on parse failure the source is
/// shown as a code fallback so no content is silently lost.
/// </summary>
public sealed record MarkdownMathBlock(string Source) : MarkdownBlock;

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
public sealed record MarkdownImageBlock(
    string Url,
    string? AltText,
    string? Title,
    double? Width = null,
    double? Height = null) : MarkdownBlock;

/// <summary>
/// Diagram dialects MarkMello may emit as <see cref="MarkdownDiagramBlock"/>.
/// Membership in this enum does not imply runtime support — that is decided
/// by <c>SupportedDiagramDialects</c> in the Application layer plus mandatory
/// renderer composition validation.
/// </summary>
public enum MarkdownDiagramKind
{
    /// <summary>Mermaid diagrams (first supported dialect, see ADR-0005).</summary>
    Mermaid,

    /// <summary>
    /// PlantUML diagrams. Reserved in the model for future support; not
    /// emitted by the parser and not declared as a supported dialect until
    /// a renderer backend is selected via a follow-up ADR.
    /// </summary>
    PlantUml,
}

/// <summary>
/// Block-level diagram. Emitted by the markdown pipeline when a fenced code
/// block declares a recognized diagram dialect (currently only
/// <c>mermaid</c>). The block carries the original diagram source so it can
/// be rendered later by an <c>IDiagramRenderService</c>, and optionally a
/// runtime <see cref="RenderResult"/> populated by that service.
/// </summary>
/// <param name="Kind">Diagram dialect, decided by the fence info string.</param>
/// <param name="Source">
/// Raw diagram source as written in the markdown file, with line breaks
/// preserved and trailing newline trimmed (matches <see cref="MarkdownCodeBlock"/>
/// behavior).
/// </param>
/// <param name="Info">
/// Remainder of the fence info string after the dialect token, or <c>null</c>
/// when the fence carried only the dialect name. Preserved for diagnostics
/// and future title/attribute parsing.
/// </param>
/// <param name="Title">
/// Optional human-readable title for the diagram. Reserved for future fence
/// attribute parsing; left <c>null</c> by the current parser.
/// </param>
public sealed record MarkdownDiagramBlock(
    MarkdownDiagramKind Kind,
    string Source,
    string? Info = null,
    string? Title = null) : MarkdownBlock
{
    /// <summary>
    /// Result of rendering <see cref="Source"/> through the diagram service.
    /// Null until a render pass populates it; never set by the markdown
    /// parser itself.
    /// </summary>
    public DiagramRenderResult? RenderResult { get; init; }
}

public abstract record MarkdownInline;

public sealed record MarkdownTextInline(string Text) : MarkdownInline;

public sealed record MarkdownStrongInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownEmphasisInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownCodeInline(string Code) : MarkdownInline;

/// <summary>
/// Inline math formula parsed from <c>$...$</c> (or <c>$$...$$</c> inside a
/// paragraph). <see cref="Content"/> is the raw LaTeX source without the
/// dollar delimiters; <see cref="IsDisplay"/> is true for the double-dollar
/// form, which renders larger and centered within the text flow.
/// </summary>
public sealed record MarkdownMathInline(string Content, bool IsDisplay) : MarkdownInline;

public sealed record MarkdownImageInline(string Url, string? AltText, string? Title) : MarkdownInline;

public sealed record MarkdownLinkInline(IReadOnlyList<MarkdownInline> Inlines, string Url, string? Title) : MarkdownInline;

public sealed record MarkdownLineBreakInline() : MarkdownInline;
