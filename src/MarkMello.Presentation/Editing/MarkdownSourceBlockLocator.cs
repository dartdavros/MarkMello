using System.Text;

namespace MarkMello.Presentation.Editing;

/// <summary>
/// Позволяет извлекать строки блока из исходного Markdown и заменять их
/// с сохранением корректных переводов строк документа.
/// </summary>
public static class MarkdownSourceBlockLocator
{
    /// <summary>
    /// Извлекает диапазон строк [startLine, endLine] (0-based) из исходного текста
    /// и возвращает текст блока и его символьные границы [charStart, charEnd].
    /// </summary>
    public static bool TryExtractLines(
        string source,
        int startLine,
        int endLine,
        out string blockText,
        out int charStart,
        out int charEnd)
    {
        blockText = string.Empty;
        charStart = 0;
        charEnd = 0;

        if (string.IsNullOrEmpty(source) || startLine < 0 || endLine < startLine)
        {
            return false;
        }

        var currentLine = 0;
        var startOffset = -1;
        var endOffset = source.Length;

        for (var i = 0; i < source.Length; i++)
        {
            if (currentLine == startLine && startOffset < 0)
            {
                startOffset = i;
            }

            if (source[i] == '\n')
            {
                if (currentLine == endLine)
                {
                    endOffset = (i > 0 && source[i - 1] == '\r') ? i - 1 : i;
                    break;
                }
                currentLine++;
            }
        }

        if (startOffset < 0)
        {
            if (currentLine == startLine)
            {
                startOffset = source.Length;
            }
            else
            {
                return false;
            }
        }

        if (endOffset < startOffset)
        {
            endOffset = startOffset;
        }

        charStart = startOffset;
        charEnd = endOffset;
        blockText = source[startOffset..endOffset];
        return true;
    }

    /// <summary>
    /// Заменяет диапазон символов [charStart, charEnd] на новый текст,
    /// согласуя переводы строк с исходным документом.
    /// </summary>
    public static string ReplaceRange(string source, int charStart, int charEnd, string newText)
    {
        ArgumentNullException.ThrowIfNull(source);
        newText ??= string.Empty;

        var lineEnding = MarkdownLineEndings.Detect(source);
        var normalizedNewText = NormalizeLineEndings(newText, lineEnding);

        var clampedStart = Math.Clamp(charStart, 0, source.Length);
        var clampedEnd = Math.Clamp(charEnd, clampedStart, source.Length);

        return string.Concat(
            source.AsSpan(0, clampedStart),
            normalizedNewText,
            source.AsSpan(clampedEnd));
    }

    private static string NormalizeLineEndings(string text, string lineEnding)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var unified = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return lineEnding == "\n" ? unified : unified.Replace("\n", lineEnding);
    }
}
