using System.Text;

namespace MarkMello.Presentation.Clipboard;

internal static class MarkdownV2Escaper
{
    public static string EscapeText(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (IsMarkdownV2Special(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    public static string EscapeUrl(string url)
        => url.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    public static string EscapeCode(string code)
        => code.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);

    private static bool IsMarkdownV2Special(char character)
        => character is '\\' or '_' or '*' or '[' or ']' or '(' or ')' or '~' or '`' or '>' or '#'
            or '+' or '-' or '=' or '|' or '{' or '}' or '.' or '!';
}
