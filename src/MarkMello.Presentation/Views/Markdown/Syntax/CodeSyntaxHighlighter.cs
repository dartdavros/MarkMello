namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Подсветка блока кода по его info string (```python). Неизвестный язык или блок без
/// языка остаются одноцветными — угадывать язык по содержимому не стоит: ошибка
/// раскрасит текст хуже, чем отсутствие подсветки.
/// </summary>
internal static class CodeSyntaxHighlighter
{
    /// <summary>Огромный блок (дамп, лог) не раскрашиваем: открытие документа важнее.</summary>
    private const int MaxHighlightedLength = 200_000;

    private static readonly HashSet<string> MarkupLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "xml", "html", "htm", "xhtml", "svg", "xaml", "axaml", "xsd", "xsl", "xslt", "plist", "csproj", "props", "targets", "vue"
    };

    public static IReadOnlyList<SyntaxToken> Tokenize(string code, string? info)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (code.Length == 0 || code.Length > MaxHighlightedLength || GetLanguageName(info) is not { } name)
        {
            return [];
        }

        if (name.Equals("diff", StringComparison.OrdinalIgnoreCase) || name.Equals("patch", StringComparison.OrdinalIgnoreCase))
        {
            return DiffTokenizer.Tokenize(code);
        }

        if (MarkupLanguages.Contains(name))
        {
            return MarkupTokenizer.Tokenize(code);
        }

        return SyntaxLanguages.Find(name) is { } language ? SyntaxTokenizer.Tokenize(code, language) : [];
    }

    /// <summary>
    /// Имя языка — первое слово info string: «python title="x.py"», «{.python}» (pandoc)
    /// и «language-python» сводятся к «python».
    /// </summary>
    internal static string? GetLanguageName(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return null;
        }

        var name = info.Trim().Split([' ', '\t', ',', '{', '}'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (name is null)
        {
            return null;
        }

        name = name.TrimStart('.');
        if (name.StartsWith("language-", StringComparison.OrdinalIgnoreCase))
        {
            name = name["language-".Length..];
        }

        return name.Length == 0 ? null : name;
    }
}
