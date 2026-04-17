using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal static class MarkdownWordNavigator
{
    public static DocumentTextRange GetWordRange(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return DocumentTextRange.Empty;
        }

        var normalizedOffset = Math.Clamp(offset, 0, text.Length - 1);
        if (!IsWordCharacter(text[normalizedOffset]))
        {
            return DocumentTextRange.Empty;
        }

        var start = normalizedOffset;
        while (start > 0 && IsWordCharacter(text[start - 1]))
        {
            start--;
        }

        var end = normalizedOffset + 1;
        while (end < text.Length && IsWordCharacter(text[end]))
        {
            end++;
        }

        return start >= end
            ? DocumentTextRange.Empty
            : new DocumentTextRange(start, end);
    }

    private static bool IsWordCharacter(char value)
        => char.IsLetterOrDigit(value) || value == '_';
}
