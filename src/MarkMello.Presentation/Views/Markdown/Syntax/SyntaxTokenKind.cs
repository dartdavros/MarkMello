namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Роль фрагмента кода для подсветки. Ролей намеренно немного: подсветка в читалке
/// должна помогать глазу, а не превращать блок кода в радугу (#46).
/// </summary>
internal enum SyntaxTokenKind
{
    None,
    Keyword,
    String,
    Comment,

    /// <summary>Числа и литералы-константы: true, null, None.</summary>
    Number,

    /// <summary>Типы, а также ключи конфигов, атрибуты разметки и переменные оболочек.</summary>
    Type,

    Function
}

/// <summary>Фрагмент исходного кода с ролью. Позиции — в символах исходной строки.</summary>
internal readonly record struct SyntaxToken(int Start, int Length, SyntaxTokenKind Kind);
