namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Раскраска diff/patch построчно: добавленное — как строка, удалённое — как ключевое
/// слово, заголовки ханков — как число, служебные строки — как комментарий.
/// </summary>
internal static class DiffTokenizer
{
    public static IReadOnlyList<SyntaxToken> Tokenize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var tokens = new List<SyntaxToken>();
        var lineStart = 0;
        while (lineStart < code.Length)
        {
            var newline = code.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? code.Length : newline;
            var kind = Classify(code.AsSpan(lineStart, lineEnd - lineStart));
            if (kind != SyntaxTokenKind.None && lineEnd > lineStart)
            {
                tokens.Add(new SyntaxToken(lineStart, lineEnd - lineStart, kind));
            }

            lineStart = lineEnd + 1;
        }

        return tokens;
    }

    private static SyntaxTokenKind Classify(ReadOnlySpan<char> line)
    {
        if (line.StartsWith("@@", StringComparison.Ordinal))
        {
            return SyntaxTokenKind.Number;
        }

        if (line.StartsWith("+++", StringComparison.Ordinal)
            || line.StartsWith("---", StringComparison.Ordinal)
            || line.StartsWith("diff ", StringComparison.Ordinal)
            || line.StartsWith("index ", StringComparison.Ordinal))
        {
            return SyntaxTokenKind.Comment;
        }

        if (line.StartsWith("+", StringComparison.Ordinal))
        {
            return SyntaxTokenKind.String;
        }

        return line.StartsWith("-", StringComparison.Ordinal) ? SyntaxTokenKind.Keyword : SyntaxTokenKind.None;
    }
}
