using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal static class MarkdownHeadingAnchorSlugger
{
    public static string CreateAnchor(IReadOnlyList<MarkdownInline> inlines)
        => CreateAnchor(ExtractPlainText(inlines));

    public static string CreateAnchor(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormKC).Trim();
        var builder = new StringBuilder(normalized.Length);
        var previousWasHyphen = false;

        foreach (var rawChar in normalized)
        {
            var c = char.ToLowerInvariant(rawChar);
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousWasHyphen = false;
                continue;
            }

            if (char.IsWhiteSpace(c) || c == '-' || c == '_')
            {
                if (builder.Length > 0 && !previousWasHyphen)
                {
                    builder.Append('-');
                    previousWasHyphen = true;
                }
            }
        }

        if (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    public static bool TryNormalizeFragment(string linkTarget, out string anchor)
    {
        anchor = string.Empty;
        if (string.IsNullOrWhiteSpace(linkTarget))
        {
            return false;
        }

        var trimmed = linkTarget.Trim();
        if (!trimmed.StartsWith('#') || trimmed.Length == 1)
        {
            return false;
        }

        var rawAnchor = trimmed[1..];
        try
        {
            rawAnchor = Uri.UnescapeDataString(rawAnchor);
        }
        catch (UriFormatException)
        {
            return false;
        }

        anchor = CreateAnchor(rawAnchor);
        return anchor.Length > 0;
    }

    private static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        if (inlines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            AppendPlainText(inline, builder);
        }

        return builder.ToString();
    }

    private static void AppendPlainText(MarkdownInline inline, StringBuilder builder)
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
            case MarkdownImageInline image:
                builder.Append(GetImagePlainText(image));
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
                builder.Append(' ');
                break;
        }
    }

    private static void AppendPlainText(IReadOnlyList<MarkdownInline> inlines, StringBuilder builder)
    {
        foreach (var inline in inlines)
        {
            AppendPlainText(inline, builder);
        }
    }

    private static string GetImagePlainText(MarkdownImageInline image)
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
}
