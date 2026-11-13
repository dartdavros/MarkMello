namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Раскраска разметки (XML, HTML, SVG, XAML): имена тегов, атрибуты, значения,
/// комментарии и сущности. Встроенные &lt;script&gt; и &lt;style&gt; не разбираются.
/// </summary>
internal static class MarkupTokenizer
{
    public static IReadOnlyList<SyntaxToken> Tokenize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var tokens = new List<SyntaxToken>();
        var position = 0;
        while (position < code.Length)
        {
            if (code.AsSpan(position).StartsWith("<!--", StringComparison.Ordinal))
            {
                var closeAt = code.IndexOf("-->", position + 4, StringComparison.Ordinal);
                var end = closeAt < 0 ? code.Length : closeAt + 3;
                tokens.Add(new SyntaxToken(position, end - position, SyntaxTokenKind.Comment));
                position = end;
            }
            else if (code[position] == '<' && position + 1 < code.Length && IsTagOpener(code[position + 1]))
            {
                position = ReadTag(code, position + 1, tokens);
            }
            else if (code[position] == '&')
            {
                position = ReadEntity(code, position, tokens);
            }
            else
            {
                position++;
            }
        }

        return tokens;
    }

    private static bool IsTagOpener(char value) => char.IsLetter(value) || value is '/' or '?' or '!';

    private static int ReadTag(string code, int position, List<SyntaxToken> tokens)
    {
        if (code[position] is '/' or '?' or '!')
        {
            position++;
        }

        var nameEnd = ReadName(code, position);
        if (nameEnd > position)
        {
            tokens.Add(new SyntaxToken(position, nameEnd - position, SyntaxTokenKind.Keyword));
        }

        position = nameEnd;
        while (position < code.Length && code[position] != '>')
        {
            var current = code[position];
            if (current is '"' or '\'')
            {
                var closeAt = code.IndexOf(current, position + 1);
                var end = closeAt < 0 ? code.Length : closeAt + 1;
                tokens.Add(new SyntaxToken(position, end - position, SyntaxTokenKind.String));
                position = end;
            }
            else if (IsNameStart(current))
            {
                var end = ReadName(code, position);
                tokens.Add(new SyntaxToken(position, end - position, SyntaxTokenKind.Type));
                position = end;
            }
            else if (current == '<')
            {
                // Незакрытый тег: следующий «<» начинает новый.
                return position;
            }
            else
            {
                position++;
            }
        }

        return Math.Min(position + 1, code.Length);
    }

    private static int ReadEntity(string code, int position, List<SyntaxToken> tokens)
    {
        var limit = Math.Min(code.Length, position + 12);
        for (var index = position + 1; index < limit; index++)
        {
            if (code[index] == ';')
            {
                tokens.Add(new SyntaxToken(position, index + 1 - position, SyntaxTokenKind.Number));
                return index + 1;
            }

            if (!char.IsLetterOrDigit(code[index]) && code[index] != '#')
            {
                break;
            }
        }

        return position + 1;
    }

    private static bool IsNameStart(char value) => char.IsLetter(value) || value is '_' or ':' or '@';

    private static int ReadName(string code, int position)
    {
        while (position < code.Length && (char.IsLetterOrDigit(code[position]) || code[position] is '_' or ':' or '-' or '.' or '@'))
        {
            position++;
        }

        return position;
    }
}
