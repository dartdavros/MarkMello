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

            case LineBreakInline lineBreak:
                if (lineBreak.IsHard || lineBreak.IsBackslash)
                {
                    target.Add(new MarkdownLineBreakInline());
                }
                else
                {
                    target.Add(new MarkdownTextInline(" "));
                }
                return;

            case CodeInline code:
                target.Add(new MarkdownCodeInline(code.Content.ToString()));
                return;

            case MathInline math:
                var mathContent = math.Content.ToString().Trim();
                if (!string.IsNullOrEmpty(mathContent))
                {
                    target.Add(new MarkdownMathInline(mathContent, math.DelimiterCount >= 2));
                }
                return;

            case AutolinkInline autolink:
                var autolinkText = NormalizeNullable(autolink.Url) ?? string.Empty;
                if (autolinkText.Length == 0)
                {
                    return;
                }

                var autolinkUrl = autolink.IsEmail && !autolinkText.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    ? $"mailto:{autolinkText}"
                    : autolinkText;
                target.Add(new MarkdownLinkInline(
                    [new MarkdownTextInline(autolinkText)],
                    autolinkUrl,
                    null));
                return;

            case LinkInline link when link.IsImage:
                var altInlines = ConvertInlines(link);
                var altText = ExtractPlainText(altInlines);
                target.Add(new MarkdownImageInline(
                    NormalizeNullable(link.Url) ?? string.Empty,
                    string.IsNullOrWhiteSpace(altText) ? null : altText,
                    NormalizeNullable(link.Title)));
                return;

            case LinkInline link when !link.IsImage:
                var linkText = ConvertInlines(link);
                target.Add(new MarkdownLinkInline(
                    linkText,
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

            case HtmlEntityInline entityInline:
                var decoded = entityInline.Transcoded.ToString();
                if (!string.IsNullOrEmpty(decoded))
                {
                    target.Add(new MarkdownTextInline(decoded));
                }
                return;

            case HtmlInline htmlInline:
                HandleInlineHtmlTag(htmlInline.Tag, target);
                return;

            case ContainerInline nested:
                foreach (var child in ConvertInlines(nested))
                {
                    target.Add(child);
                }
                return;
        }
    }
}
