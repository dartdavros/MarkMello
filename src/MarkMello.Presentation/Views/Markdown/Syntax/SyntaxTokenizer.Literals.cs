namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Литералы и служебные конструкции: числа, переменные оболочек, параметры команд,
/// директивы препроцессора, секции конфигов, цвета CSS и аннотации.
/// </summary>
internal sealed partial class SyntaxTokenizer
{
    private bool TryDirective()
    {
        if (!_language.HashDirectives || _code[_position] != '#' || !IsLineStart(_position))
        {
            return false;
        }

        var end = _position + 1;
        while (end < _code.Length && char.IsLetter(_code[end]))
        {
            end++;
        }

        return Add(_position, end, SyntaxTokenKind.Keyword);
    }

    private bool TrySectionHeader()
    {
        if (!_language.SectionHeaders || _code[_position] != '[' || !IsLineStart(_position))
        {
            return false;
        }

        var lineEnd = LineEnd(_position);
        var closeAt = _code.IndexOf(']', _position, lineEnd - _position);
        return Add(_position, closeAt < 0 ? lineEnd : closeAt + 1, SyntaxTokenKind.Keyword);
    }

    private bool TryNumber()
    {
        var current = _code[_position];
        var startsWithDot = current == '.' && _position + 1 < _code.Length && char.IsAsciiDigit(_code[_position + 1]);
        if (!char.IsAsciiDigit(current) && !startsWithDot)
        {
            return false;
        }

        var end = _position + 1;
        while (end < _code.Length)
        {
            var next = _code[end];
            var isExponentSign = (next is '+' or '-') && _code[end - 1] is 'e' or 'E' && !IsHexLiteral();
            var isFraction = next == '.' && end + 1 < _code.Length && char.IsAsciiDigit(_code[end + 1]);
            if (char.IsAsciiLetterOrDigit(next) || next == '_' || isExponentSign || isFraction)
            {
                end++;
                continue;
            }

            break;
        }

        return Add(_position, end, SyntaxTokenKind.Number);
    }

    private bool TryVariable()
    {
        if (_language.VariableSigil is not { } sigil || _code[_position] != sigil)
        {
            return false;
        }

        var end = _position + 1;
        if (end < _code.Length && _code[end] == '{')
        {
            var closeAt = _code.IndexOf('}', end, LineEnd(end) - end);
            end = closeAt < 0 ? end : closeAt + 1;
        }
        else
        {
            while (end < _code.Length && (IsIdentifierPart(_code[end]) || (end == _position + 1 && "?!#@*".Contains(_code[end], StringComparison.Ordinal))))
            {
                end++;
            }
        }

        if (end <= _position + 1)
        {
            return false;
        }

        // $true, $null в PowerShell — константы, остальное — переменные.
        var kind = _language.IsConstant(_code[_position..end]) ? SyntaxTokenKind.Number : SyntaxTokenKind.Type;
        return Add(_position, end, kind);
    }

    private bool TryParameter()
    {
        if (!_language.DashParameters || _code[_position] != '-' || !IsAfterSeparator(_position))
        {
            return false;
        }

        var nameAt = _position + (_position + 1 < _code.Length && _code[_position + 1] == '-' ? 2 : 1);
        if (nameAt >= _code.Length || !char.IsLetter(_code[nameAt]))
        {
            return false;
        }

        return Add(_position, ReadIdentifier(nameAt), SyntaxTokenKind.Type);
    }

    private bool TryHashColor()
    {
        if (!_language.HashColors || _code[_position] != '#')
        {
            return false;
        }

        var end = _position + 1;
        while (end < _code.Length && char.IsAsciiHexDigit(_code[end]))
        {
            end++;
        }

        var digits = end - _position - 1;
        return digits is 3 or 4 or 6 or 8 && (end == _code.Length || !IsIdentifierPart(_code[end]))
            && Add(_position, end, SyntaxTokenKind.Number);
    }

    /// <summary>Декораторы и аннотации: @property, @Override, @media.</summary>
    private bool TryAnnotation()
    {
        if (_code[_position] != '@' || _position + 1 >= _code.Length || !IsIdentifierStart(_code[_position + 1]))
        {
            return false;
        }

        return Add(_position, ReadIdentifier(_position + 1), SyntaxTokenKind.Type);
    }

    private bool IsHexLiteral()
        => _code.AsSpan(_position).StartsWith("0x", StringComparison.OrdinalIgnoreCase);
}
