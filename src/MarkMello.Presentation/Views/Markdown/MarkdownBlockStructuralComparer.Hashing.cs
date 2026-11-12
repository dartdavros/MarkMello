using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed partial class MarkdownBlockStructuralComparer
{
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
                case MarkdownMathInline math:
                    hash.Add(math.Content, StringComparer.Ordinal);
                    hash.Add(math.IsDisplay);
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
