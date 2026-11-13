namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Однопроходный лексер для раскраски. Он не строит дерево и не проверяет синтаксис:
/// распознаёт комментарии, строки, числа и слова по описанию <see cref="SyntaxLanguage"/>.
/// Ошибиться он может только в цвете, а не в тексте — текст блока не меняется.
/// </summary>
internal sealed partial class SyntaxTokenizer
{
    private readonly string _code;
    private readonly SyntaxLanguage _language;
    private readonly List<SyntaxToken> _tokens = [];
    private int _position;

    private SyntaxTokenizer(string code, SyntaxLanguage language)
    {
        _code = code;
        _language = language;
    }

    public static IReadOnlyList<SyntaxToken> Tokenize(string code, SyntaxLanguage language)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(language);

        var tokenizer = new SyntaxTokenizer(code, language);
        tokenizer.Run();
        return tokenizer._tokens;
    }

    private void Run()
    {
        while (_position < _code.Length)
        {
            if (char.IsWhiteSpace(_code[_position]))
            {
                _position++;
                continue;
            }

            if (TryDirective() || TryComment() || TryString() || TrySectionHeader() || TryNumber()
                || TryVariable() || TryParameter() || TryHashColor() || TryAnnotation() || TryWord())
            {
                continue;
            }

            _position++;
        }
    }

    private bool TryComment()
    {
        foreach (var (open, close) in _language.BlockComments)
        {
            if (StartsWith(open))
            {
                var closeAt = _code.IndexOf(close, _position + open.Length, StringComparison.Ordinal);
                return Add(_position, closeAt < 0 ? _code.Length : closeAt + close.Length, SyntaxTokenKind.Comment);
            }
        }

        foreach (var marker in _language.LineComments)
        {
            var needsSeparator = marker == "#" && _language.HashCommentNeedsSeparator;
            if (StartsWith(marker) && (!needsSeparator || IsAfterSeparator(_position)))
            {
                return Add(_position, LineEnd(_position), SyntaxTokenKind.Comment);
            }
        }

        return false;
    }

    private bool TryString()
    {
        var start = _position;
        var quoteAt = SkipStringPrefix(start);
        if (quoteAt >= _code.Length || !_language.Quotes.Contains(_code[quoteAt], StringComparison.Ordinal))
        {
            return false;
        }

        var quote = _code[quoteAt];
        int end;
        if (_language.TripleQuotes && _code.AsSpan(quoteAt).StartsWith(new string(quote, 3)))
        {
            var closeAt = _code.IndexOf(new string(quote, 3), quoteAt + 3, StringComparison.Ordinal);
            end = closeAt < 0 ? _code.Length : closeAt + 3;
        }
        else
        {
            // Verbatim-строка C# (@"…"), «сырые» одинарные кавычки и raw-строки Go не знают экранирования.
            var verbatim = _code.AsSpan(start, quoteAt - start).Contains('@');
            var raw = verbatim
                || (quote == '\'' && _language.RawSingleQuotes)
                || (quote == '`' && _language.RawBackticks);
            var escape = raw ? null : _language.EscapeChar;
            var doubled = verbatim || _language.DoubledQuoteEscapes;
            var multiline = verbatim || (quote == '`' && _language.MultilineBacktick);
            end = FindStringEnd(quoteAt, quote, escape, doubled, multiline);
        }

        // Строка-ключ в JSON окрашивается как ключ, а не как значение.
        var kind = _language.HighlightKeys && NextNonSpace(end) == ':' ? SyntaxTokenKind.Type : SyntaxTokenKind.String;
        return Add(start, end, kind);
    }

    /// <summary>Префиксы строк: f"…", r"…", b"…" в Python, @"…" и $"…" в C#.</summary>
    private int SkipStringPrefix(int start)
    {
        if (start > 0 && IsIdentifierPart(_code[start - 1]))
        {
            return start;
        }

        var index = start;
        while (index < _code.Length && index - start < 2 && "fFrRbBuU@$".Contains(_code[index], StringComparison.Ordinal))
        {
            index++;
        }

        return index < _code.Length && _language.Quotes.Contains(_code[index], StringComparison.Ordinal) ? index : start;
    }

    private int FindStringEnd(int quoteAt, char quote, char? escape, bool doubled, bool multiline)
    {
        for (var index = quoteAt + 1; index < _code.Length; index++)
        {
            var current = _code[index];
            if (current == escape)
            {
                index++;
                continue;
            }

            if (current == quote)
            {
                // Удвоенная кавычка ('it''s', @"a ""b""") — это символ, а не конец строки.
                if (doubled && index + 1 < _code.Length && _code[index + 1] == quote)
                {
                    index++;
                    continue;
                }

                return index + 1;
            }

            if (current == '\n' && !multiline)
            {
                return index;
            }
        }

        return _code.Length;
    }

    private bool TryWord()
    {
        if (!IsIdentifierStart(_code[_position]))
        {
            return false;
        }

        var start = _position;
        var end = ReadIdentifier(start);
        _position = end;

        var kind = Classify(_code[start..end], start, end);
        return kind == SyntaxTokenKind.None || Add(start, end, kind);
    }

    private SyntaxTokenKind Classify(string word, int start, int end)
    {
        if (_language.IsKeyword(word))
        {
            return SyntaxTokenKind.Keyword;
        }

        if (_language.IsConstant(word))
        {
            return SyntaxTokenKind.Number;
        }

        if (_language.IsType(word))
        {
            return SyntaxTokenKind.Type;
        }

        var next = NextNonSpace(end);
        if (_language.HighlightKeys && next is ':' or '=' && IsKeyPosition(start))
        {
            return SyntaxTokenKind.Type;
        }

        if (next == '(')
        {
            return SyntaxTokenKind.Function;
        }

        // Командлеты PowerShell: Get-ChildItem.
        if (_language.DashParameters && char.IsUpper(word[0]) && word.Contains('-', StringComparison.Ordinal))
        {
            return SyntaxTokenKind.Function;
        }

        // Заглавная первая буква и есть строчные — класс; ВСЕ_ЗАГЛАВНЫЕ — константа, не тип.
        return _language.CapitalizedIsType && char.IsUpper(word[0]) && word.Any(char.IsLower)
            ? SyntaxTokenKind.Type
            : SyntaxTokenKind.None;
    }

    private int ReadIdentifier(int start)
    {
        var end = start;
        while (end < _code.Length)
        {
            var current = _code[end];
            var isExtra = _language.IdentifierExtras.Contains(current, StringComparison.Ordinal)
                && end + 1 < _code.Length
                && char.IsLetterOrDigit(_code[end + 1]);
            if (!IsIdentifierPart(current) && !isExtra)
            {
                break;
            }

            end++;
        }

        return end;
    }

    /// <summary>Ключ конфига стоит первым в строке, допускается маркер списка YAML.</summary>
    private bool IsKeyPosition(int start)
    {
        var index = start - 1;
        while (index >= 0 && _code[index] is ' ' or '\t' or '-')
        {
            index--;
        }

        return index < 0 || _code[index] == '\n';
    }

    private bool IsLineStart(int position)
    {
        var index = position - 1;
        while (index >= 0 && _code[index] is ' ' or '\t')
        {
            index--;
        }

        return index < 0 || _code[index] == '\n';
    }

    private bool IsAfterSeparator(int position)
        => position == 0 || char.IsWhiteSpace(_code[position - 1]) || _code[position - 1] is ';' or '(' or '|';

    private char NextNonSpace(int position)
    {
        while (position < _code.Length && _code[position] is ' ' or '\t')
        {
            position++;
        }

        return position < _code.Length ? _code[position] : '\0';
    }

    private int LineEnd(int position)
    {
        var newline = _code.IndexOf('\n', position);
        return newline < 0 ? _code.Length : newline;
    }

    private bool StartsWith(string value) => _code.AsSpan(_position).StartsWith(value, StringComparison.Ordinal);

    private bool Add(int start, int end, SyntaxTokenKind kind)
    {
        _position = Math.Max(end, start + 1);
        if (end > start)
        {
            _tokens.Add(new SyntaxToken(start, end - start, kind));
        }

        return true;
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';
}
