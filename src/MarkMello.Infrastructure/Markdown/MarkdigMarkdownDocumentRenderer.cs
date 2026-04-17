using Markdig;
using MarkdigMarkdown = Markdig.Markdown;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using CodeBlock = Markdig.Syntax.CodeBlock;

namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Markdig-based parse layer for M3.
/// Даёт устойчивый AST CommonMark + common extensions, затем переводит его
/// в UI-agnostic document model.
/// </summary>
public sealed class MarkdigMarkdownDocumentRenderer : IMarkdownDocumentRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public RenderedMarkdownDocument Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return RenderedMarkdownDocument.Empty;
        }

        var document = MarkdigMarkdown.Parse(markdown, Pipeline);
        var blocks = ConvertBlocks(document);
        return new RenderedMarkdownDocument(blocks);
    }

    private static List<MarkdownBlock> ConvertBlocks(ContainerBlock container)
    {
        var result = new List<MarkdownBlock>(container.Count);

        foreach (var block in container)
        {
            AddConvertedBlock(block, result);
        }

        return result;
    }

    private static void AddConvertedBlock(Block block, List<MarkdownBlock> target)
    {
        switch (block)
        {
            case HeadingBlock heading:
                target.Add(new MarkdownHeadingBlock(
                    Math.Clamp(heading.Level, 1, 6),
                    ConvertInlines(heading.Inline)));
                return;

            case ParagraphBlock paragraph:
                target.Add(new MarkdownParagraphBlock(ConvertInlines(paragraph.Inline)));
                return;

            case QuoteBlock quote:
                target.Add(new MarkdownQuoteBlock(ConvertBlocks(quote)));
                return;

            case ListBlock list:
                target.Add(ConvertList(list));
                return;

            case ThematicBreakBlock:
                target.Add(new MarkdownHorizontalRuleBlock());
                return;

            case FencedCodeBlock fencedCode:
                target.Add(new MarkdownCodeBlock(
                    NormalizeNullable(fencedCode.Info?.ToString()),
                    ExtractCode(fencedCode)));
                return;

            case CodeBlock codeBlock:
                target.Add(new MarkdownCodeBlock(null, ExtractCode(codeBlock)));
                return;

            case Table table:
                target.Add(ConvertTable(table));
                return;

            case HtmlBlock htmlBlock:
                var htmlText = ExtractLeafText(htmlBlock);
                if (!string.IsNullOrWhiteSpace(htmlText))
                {
                    target.Add(new MarkdownCodeBlock("html", htmlText));
                }
                return;

            case ContainerBlock nested:
                foreach (var nestedBlock in ConvertBlocks(nested))
                {
                    target.Add(nestedBlock);
                }
                return;

            case LeafBlock leaf:
                var leafText = ExtractLeafText(leaf);
                if (!string.IsNullOrWhiteSpace(leafText))
                {
                    target.Add(new MarkdownParagraphBlock([
                        new MarkdownTextInline(leafText)
                    ]));
                }
                return;
        }
    }

    private static MarkdownListBlock ConvertList(ListBlock list)
    {
        var items = new List<MarkdownListItem>(list.Count);

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
            {
                continue;
            }

            items.Add(new MarkdownListItem(ConvertBlocks(item)));
        }

        return new MarkdownListBlock(list.IsOrdered, items);
    }

    private static MarkdownTableBlock ConvertTable(Table table)
    {
        var header = new List<MarkdownTableCell>();
        var rows = new List<IReadOnlyList<MarkdownTableCell>>();

        foreach (var child in table)
        {
            if (child is not TableRow row)
            {
                continue;
            }

            var cells = new List<MarkdownTableCell>(row.Count);
            foreach (var rowChild in row)
            {
                if (rowChild is not TableCell cell)
                {
                    continue;
                }

                cells.Add(new MarkdownTableCell(ConvertBlocksToInlines(cell)));
            }

            if (row.IsHeader)
            {
                header.AddRange(cells);
            }
            else
            {
                rows.Add(cells);
            }
        }

        return new MarkdownTableBlock(header, rows);
    }

    private static IReadOnlyList<MarkdownInline> ConvertBlocksToInlines(ContainerBlock container)
    {
        var blocks = ConvertBlocks(container);
        if (blocks.Count == 0)
        {
            return Array.Empty<MarkdownInline>();
        }

        var result = new List<MarkdownInline>();
        var first = true;

        foreach (var block in blocks)
        {
            if (!first)
            {
                result.Add(new MarkdownLineBreakInline());
            }
            first = false;

            switch (block)
            {
                case MarkdownParagraphBlock paragraph:
                    AddInlineRange(result, paragraph.Inlines);
                    break;

                case MarkdownHeadingBlock heading:
                    AddInlineRange(result, heading.Inlines);
                    break;

                case MarkdownCodeBlock code:
                    result.Add(new MarkdownCodeInline(code.Code));
                    break;

                default:
                    var text = ExtractPlainText(block);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(new MarkdownTextInline(text));
                    }
                    break;
            }
        }

        return result;
    }

    private static IReadOnlyList<MarkdownInline> ConvertInlines(ContainerInline? container)
    {
        if (container is null)
        {
            return Array.Empty<MarkdownInline>();
        }

        var result = new List<MarkdownInline>();

        foreach (var inline in container)
        {
            AddConvertedInline(inline, result);
        }

        return result;
    }

    private static void AddConvertedInline(Inline inline, List<MarkdownInline> target)
    {
        switch (inline)
        {
            case LiteralInline literal:
                var text = literal.Content.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    target.Add(new MarkdownTextInline(text));
                }
                return;

            case LineBreakInline:
                target.Add(new MarkdownLineBreakInline());
                return;

            case CodeInline code:
                target.Add(new MarkdownCodeInline(code.Content.ToString()));
                return;

            case LinkInline link when !link.IsImage:
                var linkText = ConvertInlines(link);
                target.Add(new MarkdownLinkInline(
                    ExtractPlainText(linkText),
                    NormalizeNullable(link.Url) ?? string.Empty,
                    NormalizeNullable(link.Title)));
                return;

            case EmphasisInline emphasis:
                var children = ConvertInlines(emphasis);
                if (emphasis.DelimiterCount >= 2)
                {
                    target.Add(new MarkdownStrongInline(children));
                }
                else
                {
                    target.Add(new MarkdownEmphasisInline(children));
                }
                return;

            case ContainerInline nested:
                foreach (var child in ConvertInlines(nested))
                {
                    target.Add(child);
                }
                return;
        }
    }

    private static string ExtractCode(CodeBlock codeBlock)
    {
        var text = codeBlock.Lines.ToString();
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    private static string ExtractLeafText(LeafBlock block)
    {
        if (block.Inline is not null)
        {
            return ExtractPlainText(ConvertInlines(block.Inline));
        }

        return block.Lines.ToString();
    }

    private static void AddInlineRange(List<MarkdownInline> target, IReadOnlyList<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            target.Add(inline);
        }
    }

    private static string ExtractPlainText(MarkdownBlock block) => block switch
    {
        MarkdownParagraphBlock paragraph => ExtractPlainText(paragraph.Inlines),
        MarkdownHeadingBlock heading => ExtractPlainText(heading.Inlines),
        MarkdownCodeBlock code => code.Code,
        MarkdownQuoteBlock quote => string.Join(Environment.NewLine, quote.Blocks.Select(ExtractPlainText)),
        MarkdownListBlock list => string.Join(Environment.NewLine, list.Items.Select(item => string.Join(" ", item.Blocks.Select(ExtractPlainText)))),
        MarkdownTableBlock table => string.Join(Environment.NewLine, table.Rows.Select(row => string.Join(" | ", row.Select(cell => ExtractPlainText(cell.Inlines))))),
        _ => string.Empty
    };

    private static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        if (inlines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            AppendPlainText(inline, builder);
        }
        return builder.ToString();
    }

    private static void AppendPlainText(MarkdownInline inline, System.Text.StringBuilder builder)
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
            case MarkdownLinkInline link:
                builder.Append(string.IsNullOrWhiteSpace(link.Text) ? link.Url : link.Text);
                break;
            case MarkdownLineBreakInline:
                builder.AppendLine();
                break;
        }
    }

    private static void AppendPlainText(IReadOnlyList<MarkdownInline> inlines, System.Text.StringBuilder builder)
    {
        foreach (var inline in inlines)
        {
            AppendPlainText(inline, builder);
        }
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
