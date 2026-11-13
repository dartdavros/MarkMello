using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using MarkdigMarkdown = Markdig.Markdown;
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
        MarkdownMathBlock math => math.Source,
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
            case MarkdownMathInline math:
                // Keep the TeX source in plain-text views (search, copy,
                // outline): the rendered glyphs are not text.
                builder.Append(math.Content);
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

        return string.IsNullOrWhiteSpace(image.Url) || image.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? "image"
            : image.Url;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
