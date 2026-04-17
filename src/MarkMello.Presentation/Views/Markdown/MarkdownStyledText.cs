using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed record MarkdownStyledText(
    string Text,
    IReadOnlyList<MarkdownTextStyleSpan> Spans,
    IReadOnlyList<MarkdownLinkSpan> Links)
{
    public static MarkdownStyledText Empty { get; } = new(
        string.Empty,
        Array.Empty<MarkdownTextStyleSpan>(),
        Array.Empty<MarkdownLinkSpan>());

    public static MarkdownStyledText FromInlines(IReadOnlyList<MarkdownInline> inlines)
    {
        ArgumentNullException.ThrowIfNull(inlines);

        if (inlines.Count == 0)
        {
            return Empty;
        }

        var builder = new StringBuilder();
        var spans = new List<MarkdownTextStyleSpan>();
        var links = new List<MarkdownLinkSpan>();
        AppendInlines(inlines, builder, spans, links, MarkdownInlineStyleState.Default);
        return builder.Length == 0
            ? Empty
            : new MarkdownStyledText(builder.ToString(), spans, links);
    }

    private static void AppendInlines(
        IReadOnlyList<MarkdownInline> inlines,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownLinkSpan> links,
        MarkdownInlineStyleState style)
    {
        foreach (var inline in inlines)
        {
            AppendInline(inline, builder, spans, links, style);
        }
    }

    private static void AppendInline(
        MarkdownInline inline,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownLinkSpan> links,
        MarkdownInlineStyleState style)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                AppendStyledText(text.Text, builder, spans, style);
                return;

            case MarkdownStrongInline strong:
                AppendInlines(strong.Inlines, builder, spans, links, style with { IsBold = true });
                return;

            case MarkdownEmphasisInline emphasis:
                AppendInlines(emphasis.Inlines, builder, spans, links, style with { IsItalic = true });
                return;

            case MarkdownCodeInline code:
                AppendStyledText(code.Code, builder, spans, style with { IsCode = true });
                return;

            case MarkdownLinkInline link:
                AppendLink(link, builder, spans, links, style with { IsLink = true });
                return;

            case MarkdownLineBreakInline:
                AppendStyledText("\n", builder, spans, style);
                return;
        }
    }

    private static void AppendLink(
        MarkdownLinkInline link,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownLinkSpan> links,
        MarkdownInlineStyleState style)
    {
        var start = builder.Length;

        if (link.Inlines.Count > 0)
        {
            AppendInlines(link.Inlines, builder, spans, links, style);
        }
        else if (!string.IsNullOrWhiteSpace(link.Url))
        {
            AppendStyledText(link.Url, builder, spans, style);
        }

        var end = builder.Length;
        if (end <= start || string.IsNullOrWhiteSpace(link.Url))
        {
            return;
        }

        var range = new DocumentTextRange(start, end);
        if (links.Count > 0)
        {
            var last = links[^1];
            if (last.Range.End == range.Start
                && string.Equals(last.Url, link.Url, StringComparison.Ordinal)
                && string.Equals(last.Title, link.Title, StringComparison.Ordinal))
            {
                links[^1] = last with { Range = new DocumentTextRange(last.Range.Start, range.End) };
                return;
            }
        }

        links.Add(new MarkdownLinkSpan(range, link.Url, link.Title));
    }

    private static void AppendStyledText(
        string text,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        MarkdownInlineStyleState style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var start = builder.Length;
        builder.Append(text);
        var length = builder.Length - start;
        if (length == 0 || style == MarkdownInlineStyleState.Default)
        {
            return;
        }

        var range = new DocumentTextRange(start, start + length);
        if (spans.Count > 0)
        {
            var last = spans[^1];
            if (last.Style == style && last.Range.End == range.Start)
            {
                spans[^1] = last with { Range = new DocumentTextRange(last.Range.Start, range.End) };
                return;
            }
        }

        spans.Add(new MarkdownTextStyleSpan(range, style));
    }
}

internal readonly record struct MarkdownTextStyleSpan(DocumentTextRange Range, MarkdownInlineStyleState Style);

internal readonly record struct MarkdownLinkSpan(DocumentTextRange Range, string Url, string? Title);

internal readonly record struct MarkdownInlineStyleState(bool IsBold, bool IsItalic, bool IsCode, bool IsLink)
{
    public static MarkdownInlineStyleState Default { get; } = new(false, false, false, false);
}
