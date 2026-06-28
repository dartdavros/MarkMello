using System.Globalization;
using System.Text;
using MarkMello.Domain;

namespace MarkMello.Presentation.Clipboard;

internal static class MarkdownClipboardTextHelpers
{
    public static string Slice(string text, DocumentTextRange range)
    {
        if (text.Length == 0 || range.IsEmpty)
        {
            return string.Empty;
        }

        var start = Math.Clamp(range.Start, 0, text.Length);
        var end = Math.Clamp(range.End, start, text.Length);
        return end <= start ? string.Empty : text[start..end];
    }

    public static string GetListMarkerText(MarkdownListBlock list, int itemIndex)
        => list.IsOrdered
            ? string.Create(CultureInfo.InvariantCulture, $"{itemIndex + 1}. ")
            : "• ";

    public static string GetCodeLanguage(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var character in info.TrimStart())
        {
            if (char.IsWhiteSpace(character))
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(character)
                || character == '_'
                || character == '+'
                || character == '-'
                || character == '#')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static int GetPlainTextLength(MarkdownInline inline)
        => inline switch
        {
            MarkdownTextInline text => text.Text.Length,
            MarkdownStrongInline strong => GetPlainTextLength(strong.Inlines),
            MarkdownEmphasisInline emphasis => GetPlainTextLength(emphasis.Inlines),
            MarkdownCodeInline code => code.Code.Length,
            MarkdownImageInline image => GetImageInlinePlainText(image).Length,
            MarkdownLinkInline link => link.Inlines.Count > 0
                ? GetPlainTextLength(link.Inlines)
                : link.Url.Length,
            MarkdownLineBreakInline => 1,
            _ => 0
        };

    public static int GetPlainTextLength(IReadOnlyList<MarkdownInline> inlines)
    {
        var length = 0;
        foreach (var inline in inlines)
        {
            length += GetPlainTextLength(inline);
        }

        return length;
    }

    public static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        if (inlines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            AppendPlainText(builder, inline);
        }

        return builder.ToString();
    }

    public static string GetImageInlinePlainText(MarkdownImageInline image)
    {
        if (!string.IsNullOrWhiteSpace(image.AltText))
        {
            return image.AltText;
        }

        if (!string.IsNullOrWhiteSpace(image.Title))
        {
            return image.Title;
        }

        return string.IsNullOrWhiteSpace(image.Url) ? "image" : image.Url;
    }

    public static void TrimTrailingLineBreaks(StringBuilder builder, int maxAllowed)
    {
        var trailing = 0;
        for (var index = builder.Length - 1; index >= 0 && builder[index] == '\n'; index--)
        {
            trailing++;
        }

        while (trailing > maxAllowed)
        {
            builder.Length--;
            trailing--;
        }
    }

    private static void AppendPlainText(StringBuilder builder, MarkdownInline inline)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                builder.Append(text.Text);
                break;

            case MarkdownStrongInline strong:
                builder.Append(ExtractPlainText(strong.Inlines));
                break;

            case MarkdownEmphasisInline emphasis:
                builder.Append(ExtractPlainText(emphasis.Inlines));
                break;

            case MarkdownCodeInline code:
                builder.Append(code.Code);
                break;

            case MarkdownImageInline image:
                builder.Append(GetImageInlinePlainText(image));
                break;

            case MarkdownLinkInline link:
                builder.Append(link.Inlines.Count > 0 ? ExtractPlainText(link.Inlines) : link.Url);
                break;

            case MarkdownLineBreakInline:
                builder.Append('\n');
                break;
        }
    }
}
