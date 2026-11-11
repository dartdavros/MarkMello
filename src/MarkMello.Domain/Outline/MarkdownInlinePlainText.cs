using System.Text;

namespace MarkMello.Domain;

/// <summary>Plain text заголовка для оглавления: разметка отбрасывается, пробелы схлопываются.</summary>
public static class MarkdownInlinePlainText
{
    public static string From(IReadOnlyList<MarkdownInline> inlines)
    {
        var builder = new StringBuilder();
        Append(builder, inlines);
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void Append(StringBuilder builder, IReadOnlyList<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case MarkdownTextInline text:
                    builder.Append(text.Text);
                    break;
                case MarkdownCodeInline code:
                    builder.Append(code.Code);
                    break;
                case MarkdownStrongInline strong:
                    Append(builder, strong.Inlines);
                    break;
                case MarkdownEmphasisInline emphasis:
                    Append(builder, emphasis.Inlines);
                    break;
                case MarkdownLinkInline link:
                    Append(builder, link.Inlines);
                    break;
                case MarkdownImageInline image:
                    builder.Append(image.AltText);
                    break;
                case MarkdownLineBreakInline:
                    builder.Append(' ');
                    break;
            }
        }
    }
}
