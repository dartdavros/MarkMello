using System.Collections.Frozen;

namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Описание языка для <see cref="SyntaxTokenizer"/>: какие у него комментарии, строки
/// и слова. Это не грамматика — лексер знает только то, что нужно для раскраски.
/// </summary>
internal sealed class SyntaxLanguage
{
    private static readonly FrozenSet<string> NoWords = FrozenSet<string>.Empty;

    public required IReadOnlyList<string> Aliases { get; init; }

    public FrozenSet<string> Keywords { get; init; } = NoWords;

    /// <summary>Встроенные типы: int, string, bool и т.п.</summary>
    public FrozenSet<string> Types { get; init; } = NoWords;

    /// <summary>Литералы-константы: true, false, null, None, undefined.</summary>
    public FrozenSet<string> Constants { get; init; } = NoWords;

    public IReadOnlyList<string> LineComments { get; init; } = [];

    public IReadOnlyList<(string Open, string Close)> BlockComments { get; init; } = [];

    /// <summary>Символы, открывающие строку.</summary>
    public string Quotes { get; init; } = "\"'";

    /// <summary>Строки в тройных кавычках (Python, Kotlin, Swift) — многострочные.</summary>
    public bool TripleQuotes { get; init; }

    /// <summary>
    /// Экранирующий символ внутри строк: «\» почти везде, «`» в PowerShell,
    /// null — экранирования нет (SQL).
    /// </summary>
    public char? EscapeChar { get; init; } = '\\';

    /// <summary>Строки в одинарных кавычках без экранирования (Bash, PowerShell, YAML, TOML).</summary>
    public bool RawSingleQuotes { get; init; }

    /// <summary>Удвоенная кавычка внутри строки — символ, а не конец (SQL, PowerShell, YAML).</summary>
    public bool DoubledQuoteEscapes { get; init; }

    /// <summary>
    /// «#»-комментарий начинается только после пробела: в оболочках и YAML «#» бывает
    /// внутри слова ($#, a#b, build#1). В Python и Ruby комментарий может идти сразу за кодом.
    /// </summary>
    public bool HashCommentNeedsSeparator { get; init; }

    /// <summary>Кавычка-обратный апостроф допускает перевод строки (шаблоны JS, Go raw strings).</summary>
    public bool MultilineBacktick { get; init; }

    /// <summary>Строки в обратных апострофах без экранирования (raw strings в Go).</summary>
    public bool RawBackticks { get; init; }

    public bool CaseInsensitive { get; init; }

    /// <summary>Слово с заглавной буквы — тип (классы в C#, Java, TS, Python).</summary>
    public bool CapitalizedIsType { get; init; }

    /// <summary>Символ переменной ($ в оболочках и PHP) — переменная красится как тип.</summary>
    public char? VariableSigil { get; init; }

    /// <summary>Символы, допустимые внутри слова кроме букв, цифр и «_»: «-» в CSS и PowerShell.</summary>
    public string IdentifierExtras { get; init; } = string.Empty;

    /// <summary>Ключ в начале строки перед «:» или «=» — ключ конфига (YAML, TOML, JSON).</summary>
    public bool HighlightKeys { get; init; }

    /// <summary>«[секция]» в начале строки (TOML, INI).</summary>
    public bool SectionHeaders { get; init; }

    /// <summary>«#» в начале строки открывает директиву препроцессора (C, C++, C#).</summary>
    public bool HashDirectives { get; init; }

    /// <summary>«-ключ» после пробела — параметр команды (оболочки).</summary>
    public bool DashParameters { get; init; }

    /// <summary>«#» перед шестнадцатеричными цифрами — цвет (CSS).</summary>
    public bool HashColors { get; init; }

    public bool IsKeyword(string word)
        => Keywords.Contains(CaseInsensitive ? word.ToLowerInvariant() : word);

    public bool IsType(string word)
        => Types.Contains(CaseInsensitive ? word.ToLowerInvariant() : word);

    public bool IsConstant(string word)
        => Constants.Contains(CaseInsensitive ? word.ToLowerInvariant() : word);

    public static FrozenSet<string> Words(string spaceSeparated)
        => spaceSeparated
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToFrozenSet(StringComparer.Ordinal);
}
