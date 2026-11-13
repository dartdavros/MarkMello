using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using MarkdigMarkdown = Markdig.Markdown;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkMello.Application.Abstractions;
using MarkMello.Application.Diagrams;
using MarkMello.Domain;
using CodeBlock = Markdig.Syntax.CodeBlock;

namespace MarkMello.Infrastructure.Markdown;


public sealed partial class MarkdigMarkdownDocumentRenderer
{
    private static List<MarkdownBlock> ConvertBlocks(ContainerBlock container, string source)
    {
        var result = new List<MarkdownBlock>(container.Count);

        foreach (var block in container)
        {
            AddConvertedBlock(block, result, source);
        }

        return result;
    }

    private static void AddConvertedBlock(Block block, List<MarkdownBlock> target, string source)
    {
        switch (block)
        {
            case HeadingBlock heading:
                target.Add(WithSourceSpan(
                    new MarkdownHeadingBlock(
                        Math.Clamp(heading.Level, 1, 6),
                        ConvertInlines(heading.Inline)),
                    heading,
                    source));
                return;

            case ParagraphBlock paragraph:
                // A paragraph whose only meaningful inline node is an image
                // becomes a block-level image. This matches how authors write
                // "figure" style images as a standalone paragraph.
                if (TryExtractStandaloneImage(paragraph.Inline, out var standaloneImage))
                {
                    target.Add(WithSourceSpan(standaloneImage, paragraph, source));
                    return;
                }

                // A display formula standing on its own line is promoted to a
                // block: Markdig reports $$...$$ as MathInline, and as a text run
                // it occupies one line while painting a much taller fraction.
                if (TrySplitDisplayMathParagraph(paragraph, source, target))
                {
                    return;
                }

                target.Add(WithSourceSpan(
                    new MarkdownParagraphBlock(ConvertInlines(paragraph.Inline)),
                    paragraph,
                    source));
                return;

            case QuoteBlock quote:
                target.Add(WithSourceSpan(
                    ConvertQuote(quote, source),
                    quote,
                    source));
                return;

            case ListBlock list:
                target.Add(WithSourceSpan(ConvertList(list, source), list, source));
                return;

            case ThematicBreakBlock thematicBreak:
                target.Add(WithSourceSpan(new MarkdownHorizontalRuleBlock(), thematicBreak, source));
                return;

            case MathBlock mathBlock:
                // $$ ... $$ display-math block: rendered as a centered
                // formula, never silently dropped (see math diagnosis).
                // Must come before FencedCodeBlock: MathBlock derives from it.
                var mathSource = mathBlock.Lines.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
                if (!string.IsNullOrWhiteSpace(mathSource))
                {
                    target.Add(WithSourceSpan(
                        new MarkdownMathBlock(mathSource),
                        mathBlock,
                        source));
                }
                return;

            case FencedCodeBlock fencedCode:
                target.Add(WithSourceSpan(
                    ConvertFencedCodeBlock(fencedCode),
                    fencedCode,
                    source));
                return;

            case CodeBlock codeBlock:
                target.Add(WithSourceSpan(
                    new MarkdownCodeBlock(null, ExtractCode(codeBlock)),
                    codeBlock,
                    source));
                return;

            case Table table:
                target.Add(WithSourceSpan(ConvertTable(table, source), table, source));
                return;

            case HtmlBlock htmlBlock:
                // We intentionally do NOT switch on htmlBlock.Type here --
                // Markdig's HtmlBlockType enum has changed names across
                // versions (ScriptBlock, ScriptTag, ScriptPreOrStyle...).
                // Instead we strip scripts/styles/comments/CDATA by content,
                // which is stable regardless of Markdig's internal classification.
                AppendHtmlBlock(htmlBlock.Lines.ToString(), target, CreateSourceSpan(htmlBlock, source));
                return;

            case ContainerBlock nested:
                foreach (var nestedBlock in ConvertBlocks(nested, source))
                {
                    target.Add(nestedBlock);
                }
                return;

            case LeafBlock leaf:
                var leafText = ExtractLeafText(leaf);
                if (!string.IsNullOrWhiteSpace(leafText))
                {
                    target.Add(WithSourceSpan(
                        new MarkdownParagraphBlock([
                            new MarkdownTextInline(leafText)
                        ]),
                        leaf,
                        source));
                }
                return;
        }
    }

    private static MarkdownListBlock ConvertList(ListBlock list, string source)
    {
        var items = new List<MarkdownListItem>(list.Count);

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
            {
                continue;
            }

            items.Add(new MarkdownListItem(ConvertBlocks(item, source)));
        }

        return new MarkdownListBlock(list.IsOrdered, items);
    }

    private static MarkdownTableBlock ConvertTable(Table table, string source)
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

                cells.Add(new MarkdownTableCell(ConvertBlocksToInlines(cell, source)));
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

    private static IReadOnlyList<MarkdownInline> ConvertBlocksToInlines(ContainerBlock container, string source)
    {
        var blocks = ConvertBlocks(container, source);
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

    private static string ExtractCode(CodeBlock codeBlock)
    {
        var text = codeBlock.Lines.ToString();
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    private static MarkdownBlock ConvertFencedCodeBlock(FencedCodeBlock fencedCode)
    {
        // Markdig already splits the info line: Info is the first token
        // (the language/dialect), Arguments is the trimmed remainder.
        var token = NormalizeNullable(fencedCode.Info?.ToString());
        var arguments = NormalizeNullable(fencedCode.Arguments?.ToString());
        var code = ExtractCode(fencedCode);

        if (SupportedDiagramDialects.TryParseFenceToken(token, out var kind))
        {
            return new MarkdownDiagramBlock(kind, code, arguments);
        }

        // Preserve the legacy code-block info shape: keep dialect token plus
        // arguments together so existing code-block consumers don't lose the
        // remainder of an unknown info line.
        var legacyInfo = arguments is null
            ? token
            : token is null ? arguments : $"{token} {arguments}";

        return new MarkdownCodeBlock(legacyInfo, code);
    }
}
