using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Сравнивает блоки по отрисовываемому содержимому, игнорируя
/// <see cref="MarkdownBlock.SourceSpan"/>.
///
/// Записи модели содержат <see cref="IReadOnlyList{T}"/>, для которых
/// сгенерированное record-равенство сводится к сравнению ссылок, поэтому каждый
/// новый parse давал бы «всё изменилось». Компаратор нужен, чтобы preview
/// пересобирал только те блоки, которые действительно поменялись.
///
/// SourceSpan исключён намеренно: вставка строки выше сдвигает координаты всех
/// последующих блоков, но не их визуальное представление — сдвиг переносится на
/// готовые контролы отдельно.
/// </summary>
internal sealed class MarkdownBlockStructuralComparer : IEqualityComparer<MarkdownBlock>
{
    public static MarkdownBlockStructuralComparer Instance { get; } = new();

    private MarkdownBlockStructuralComparer()
    {
    }

    public bool Equals(MarkdownBlock? x, MarkdownBlock? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.GetType() != y.GetType())
        {
            return false;
        }

        return x switch
        {
            MarkdownHeadingBlock heading => y is MarkdownHeadingBlock other
                && heading.Level == other.Level
                && InlinesEqual(heading.Inlines, other.Inlines),
            MarkdownParagraphBlock paragraph => y is MarkdownParagraphBlock other
                && InlinesEqual(paragraph.Inlines, other.Inlines),
            MarkdownQuoteBlock quote => y is MarkdownQuoteBlock other
                && BlocksEqual(quote.Blocks, other.Blocks),
            MarkdownListBlock list => y is MarkdownListBlock other
                && list.IsOrdered == other.IsOrdered
                && ItemsEqual(list.Items, other.Items),
            MarkdownHorizontalRuleBlock => y is MarkdownHorizontalRuleBlock,
            MarkdownCodeBlock code => y is MarkdownCodeBlock other
                && string.Equals(code.Info, other.Info, StringComparison.Ordinal)
                && string.Equals(code.Code, other.Code, StringComparison.Ordinal),
            MarkdownTableBlock table => y is MarkdownTableBlock other
                && CellsEqual(table.Header, other.Header)
                && RowsEqual(table.Rows, other.Rows),
            MarkdownImageBlock image => y is MarkdownImageBlock other
                && string.Equals(image.Url, other.Url, StringComparison.Ordinal)
                && string.Equals(image.AltText, other.AltText, StringComparison.Ordinal)
                && string.Equals(image.Title, other.Title, StringComparison.Ordinal)
                && image.Width == other.Width
                && image.Height == other.Height,
            MarkdownDiagramBlock diagram => y is MarkdownDiagramBlock other
                && diagram.Kind == other.Kind
                && string.Equals(diagram.Source, other.Source, StringComparison.Ordinal)
                && string.Equals(diagram.Info, other.Info, StringComparison.Ordinal)
                && string.Equals(diagram.Title, other.Title, StringComparison.Ordinal)
                && Equals(diagram.RenderResult, other.RenderResult),

            // Неизвестный тип блока: считаем изменившимся, чтобы новый рендерер
            // не начал молча переиспользовать чужой контрол.
            _ => false
        };
    }

    public int GetHashCode(MarkdownBlock obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var hash = new HashCode();
        hash.Add(obj.GetType());

        switch (obj)
        {
            case MarkdownHeadingBlock heading:
                hash.Add(heading.Level);
                AddInlines(ref hash, heading.Inlines);
                break;
            case MarkdownParagraphBlock paragraph:
                AddInlines(ref hash, paragraph.Inlines);
                break;
            case MarkdownQuoteBlock quote:
                AddBlocks(ref hash, quote.Blocks);
                break;
            case MarkdownListBlock list:
                hash.Add(list.IsOrdered);
                hash.Add(list.Items.Count);
                foreach (var item in list.Items)
                {
                    AddBlocks(ref hash, item.Blocks);
                }

                break;
            case MarkdownCodeBlock code:
                hash.Add(code.Info, StringComparer.Ordinal);
                hash.Add(code.Code, StringComparer.Ordinal);
                break;
            case MarkdownTableBlock table:
                AddCells(ref hash, table.Header);
                hash.Add(table.Rows.Count);
                foreach (var row in table.Rows)
                {
                    AddCells(ref hash, row);
                }

                break;
            case MarkdownImageBlock image:
                hash.Add(image.Url, StringComparer.Ordinal);
                hash.Add(image.AltText, StringComparer.Ordinal);
                hash.Add(image.Title, StringComparer.Ordinal);
                hash.Add(image.Width);
                hash.Add(image.Height);
                break;
            case MarkdownDiagramBlock diagram:
                hash.Add(diagram.Kind);
                hash.Add(diagram.Source, StringComparer.Ordinal);
                hash.Add(diagram.Info, StringComparer.Ordinal);
                hash.Add(diagram.Title, StringComparer.Ordinal);
                break;
            default:
                break;
        }

        return hash.ToHashCode();
    }

    private static bool BlocksEqual(IReadOnlyList<MarkdownBlock> left, IReadOnlyList<MarkdownBlock> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!Instance.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ItemsEqual(IReadOnlyList<MarkdownListItem> left, IReadOnlyList<MarkdownListItem> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!BlocksEqual(left[index].Blocks, right[index].Blocks))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RowsEqual(
        IReadOnlyList<IReadOnlyList<MarkdownTableCell>> left,
        IReadOnlyList<IReadOnlyList<MarkdownTableCell>> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!CellsEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CellsEqual(IReadOnlyList<MarkdownTableCell> left, IReadOnlyList<MarkdownTableCell> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!InlinesEqual(left[index].Inlines, right[index].Inlines))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InlinesEqual(IReadOnlyList<MarkdownInline> left, IReadOnlyList<MarkdownInline> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!InlineEquals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InlineEquals(MarkdownInline left, MarkdownInline right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return (left, right) switch
        {
            (MarkdownTextInline a, MarkdownTextInline b) => string.Equals(a.Text, b.Text, StringComparison.Ordinal),
            (MarkdownCodeInline a, MarkdownCodeInline b) => string.Equals(a.Code, b.Code, StringComparison.Ordinal),
            (MarkdownStrongInline a, MarkdownStrongInline b) => InlinesEqual(a.Inlines, b.Inlines),
            (MarkdownEmphasisInline a, MarkdownEmphasisInline b) => InlinesEqual(a.Inlines, b.Inlines),
            (MarkdownLineBreakInline, MarkdownLineBreakInline) => true,
            (MarkdownImageInline a, MarkdownImageInline b) =>
                string.Equals(a.Url, b.Url, StringComparison.Ordinal)
                && string.Equals(a.AltText, b.AltText, StringComparison.Ordinal)
                && string.Equals(a.Title, b.Title, StringComparison.Ordinal),
            (MarkdownLinkInline a, MarkdownLinkInline b) =>
                string.Equals(a.Url, b.Url, StringComparison.Ordinal)
                && string.Equals(a.Title, b.Title, StringComparison.Ordinal)
                && InlinesEqual(a.Inlines, b.Inlines),
            _ => false
        };
    }

    private static void AddBlocks(ref HashCode hash, IReadOnlyList<MarkdownBlock> blocks)
    {
        hash.Add(blocks.Count);
        foreach (var block in blocks)
        {
            hash.Add(Instance.GetHashCode(block));
        }
    }

    private static void AddCells(ref HashCode hash, IReadOnlyList<MarkdownTableCell> cells)
    {
        hash.Add(cells.Count);
        foreach (var cell in cells)
        {
            AddInlines(ref hash, cell.Inlines);
        }
    }

    private static void AddInlines(ref HashCode hash, IReadOnlyList<MarkdownInline> inlines)
    {
        hash.Add(inlines.Count);
        foreach (var inline in inlines)
        {
            hash.Add(inline.GetType());
            switch (inline)
            {
                case MarkdownTextInline text:
                    hash.Add(text.Text, StringComparer.Ordinal);
                    break;
                case MarkdownCodeInline code:
                    hash.Add(code.Code, StringComparer.Ordinal);
                    break;
                case MarkdownStrongInline strong:
                    AddInlines(ref hash, strong.Inlines);
                    break;
                case MarkdownEmphasisInline emphasis:
                    AddInlines(ref hash, emphasis.Inlines);
                    break;
                case MarkdownImageInline image:
                    hash.Add(image.Url, StringComparer.Ordinal);
                    hash.Add(image.AltText, StringComparer.Ordinal);
                    break;
                case MarkdownLinkInline link:
                    hash.Add(link.Url, StringComparer.Ordinal);
                    AddInlines(ref hash, link.Inlines);
                    break;
                default:
                    break;
            }
        }
    }
}
