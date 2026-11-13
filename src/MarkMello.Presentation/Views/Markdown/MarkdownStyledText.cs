using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed record MarkdownStyledText(
    string Text,
    IReadOnlyList<MarkdownTextStyleSpan> Spans,
    IReadOnlyList<MarkdownLinkSpan> Links,
    IReadOnlyList<MarkdownInlineImageSpan> Images,
    IReadOnlyList<MarkdownMathSpan> Math)
{
    public static MarkdownStyledText Empty { get; } = new(
        string.Empty,
        Array.Empty<MarkdownTextStyleSpan>(),
        Array.Empty<MarkdownLinkSpan>(),
        Array.Empty<MarkdownInlineImageSpan>(),
        Array.Empty<MarkdownMathSpan>());

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
        var images = new List<MarkdownInlineImageSpan>();
        var math = new List<MarkdownMathSpan>();
        AppendInlines(inlines, builder, spans, links, images, math, MarkdownInlineStyleState.Default);
        return builder.Length == 0
            ? Empty
            : new MarkdownStyledText(builder.ToString(), spans, links, images, math);
    }

    private static void AppendInlines(
        IReadOnlyList<MarkdownInline> inlines,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownLinkSpan> links,
        List<MarkdownInlineImageSpan> images,
        List<MarkdownMathSpan> math,
        MarkdownInlineStyleState style)
    {
        foreach (var inline in inlines)
        {
            AppendInline(inline, builder, spans, links, images, math, style);
        }
    }

    private static void AppendInline(
        MarkdownInline inline,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownLinkSpan> links,
        List<MarkdownInlineImageSpan> images,
        List<MarkdownMathSpan> math,
        MarkdownInlineStyleState style)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                AppendStyledText(text.Text, builder, spans, style);
                return;

            case MarkdownStrongInline strong:
                AppendInlines(strong.Inlines, builder, spans, links, images, math, style with { IsBold = true });
                return;

            case MarkdownEmphasisInline emphasis:
                AppendInlines(emphasis.Inlines, builder, spans, links, images, math, style with { IsItalic = true });
                return;

            case MarkdownCodeInline code:
                AppendStyledText(code.Code, builder, spans, style with { IsCode = true });
                return;

            case MarkdownMathInline mathInline:
                AppendMath(mathInline, builder, spans, math, style);
                return;

            case MarkdownImageInline image:
                AppendImage(image, builder, spans, images, style);
                return;

            case MarkdownLinkInline link:
                AppendLink(link, builder, spans, links, images, math, style with { IsLink = true });
                return;

            case MarkdownLineBreakInline:
                AppendStyledText("\n", builder, spans, style);
                return;
        }
    }

    /// <summary>
    /// Formulas occupy a single canonical character in the text stream (the
    /// placeholder glyph is never drawn by the text layer — the math run
    /// paints the real formula), so selection/copy/search keep a stable,
    /// contiguous offset model.
    /// </summary>
    private static void AppendMath(
        MarkdownMathInline mathInline,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownMathSpan> math,
        MarkdownInlineStyleState style)
    {
        if (string.IsNullOrWhiteSpace(mathInline.Content))
        {
            return;
        }

        var start = builder.Length;
        builder.Append(MathPlaceholder);
        var end = builder.Length;
        math.Add(new MarkdownMathSpan(
            math.Count,
            new DocumentTextRange(start, end),
            mathInline.Content,
            mathInline.IsDisplay,
            style));
    }

    private const char MathPlaceholder = '\uE002';

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

    private static void AppendImage(
        MarkdownImageInline image,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownInlineImageSpan> images,
        MarkdownInlineStyleState style)
    {
        var placeholderText = GetImageInlinePlainText(image);
        if (string.IsNullOrWhiteSpace(placeholderText))
        {
            placeholderText = "image";
        }

        var start = builder.Length;
        AppendStyledText(placeholderText, builder, spans, style);
        var end = builder.Length;
        if (end <= start)
        {
            return;
        }

        images.Add(new MarkdownInlineImageSpan(
            images.Count,
            new DocumentTextRange(start, end),
            image.Url,
            image.AltText,
            image.Title,
            placeholderText,
            style));
    }

    private static void AppendLink(
        MarkdownLinkInline link,
        StringBuilder builder,
        List<MarkdownTextStyleSpan> spans,
        List<MarkdownLinkSpan> links,
        List<MarkdownInlineImageSpan> images,
        List<MarkdownMathSpan> math,
        MarkdownInlineStyleState style)
    {
        var start = builder.Length;

        if (link.Inlines.Count > 0)
        {
            AppendInlines(link.Inlines, builder, spans, links, images, math, style);
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

internal readonly record struct MarkdownInlineImageSpan(
    int Index,
    DocumentTextRange Range,
    string Url,
    string? AltText,
    string? Title,
    string PlaceholderText,
    MarkdownInlineStyleState Style);

/// <summary>
/// A parsed formula inside a styled text run. The canonical range covers the
/// single placeholder character; the renderer paints the LaTeX there.
/// </summary>
internal readonly record struct MarkdownMathSpan(
    int Index,
    DocumentTextRange Range,
    string Latex,
    bool IsDisplay,
    MarkdownInlineStyleState Style);

internal readonly record struct MarkdownInlineStyleState(bool IsBold, bool IsItalic, bool IsCode, bool IsLink)
{
    public static MarkdownInlineStyleState Default { get; } = new(false, false, false, false);
}
