using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed record MarkdownStyledText(string Text, IReadOnlyList<MarkdownTextStyleSpan> Spans)
{
    public static MarkdownStyledText Empty { get; } = new(string.Empty, Array.Empty<MarkdownTextStyleSpan>());

    public static MarkdownStyledText FromInlines(IReadOnlyList<MarkdownInline> inlines)
    {
        ArgumentNullException.ThrowIfNull(inlines);

        if (inlines.Count == 0)
        {
            return Empty;
        }

        var builder = new StringBuilder();
        var spans = new List<MarkdownTextStyleSpan>();
        AppendInlines(inlines, builder, spans, MarkdownInlineStyleState.Default);
        return builder.Length == 0
            ? Empty
            : new MarkdownStyledText(builder.ToString(), spans);
    }

    private static void AppendInlines(
        IReadOnlyList<MarkdownInline> inlines,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        MarkdownInlineStyleState style)
    {
        foreach (var inline in inlines)
        {
            AppendInline(inline, builder, spans, style);
        }
    }

    private static void AppendInline(
        MarkdownInline inline,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        MarkdownInlineStyleState style)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                AppendStyledText(text.Text, builder, spans, style);
                return;

            case MarkdownStrongInline strong:
                AppendInlines(strong.Inlines, builder, spans, style with { IsBold = true });
                return;

            case MarkdownEmphasisInline emphasis:
                AppendInlines(emphasis.Inlines, builder, spans, style with { IsItalic = true });
                return;

            case MarkdownCodeInline code:
                AppendStyledText(code.Code, builder, spans, style with { IsCode = true });
                return;

            case MarkdownLinkInline link:
                var linkStyle = style with { IsLink = true };
                if (link.Inlines.Count > 0)
                {
                    AppendInlines(link.Inlines, builder, spans, linkStyle);
                }
                else if (!string.IsNullOrWhiteSpace(link.Url))
                {
                    AppendStyledText(link.Url, builder, spans, linkStyle);
                }
                return;

            case MarkdownLineBreakInline:
                AppendStyledText("\n", builder, spans, style);
                return;
        }
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

internal readonly record struct MarkdownInlineStyleState(bool IsBold, bool IsItalic, bool IsCode, bool IsLink)
{
    public static MarkdownInlineStyleState Default { get; } = new(false, false, false, false);
}
