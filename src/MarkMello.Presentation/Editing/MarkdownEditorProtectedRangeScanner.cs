using MarkMello.Domain;

namespace MarkMello.Presentation.Editing;

internal static class MarkdownEditorProtectedRangeScanner
{
    public static IReadOnlyList<DocumentTextRange> FindDataImageDefinitionRanges(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return Array.Empty<DocumentTextRange>();
        }

        var ranges = new List<DocumentTextRange>();
        var lineStart = 0;

        while (lineStart < source.Length)
        {
            var lineEnd = lineStart;
            while (lineEnd < source.Length && source[lineEnd] != '\r' && source[lineEnd] != '\n')
            {
                lineEnd++;
            }

            var nextLineStart = lineEnd;
            if (nextLineStart < source.Length && source[nextLineStart] == '\r')
            {
                nextLineStart++;
            }

            if (nextLineStart < source.Length && source[nextLineStart] == '\n')
            {
                nextLineStart++;
            }

            if (IsDataImageDefinitionLine(source.AsSpan(lineStart, lineEnd - lineStart)))
            {
                ranges.Add(new DocumentTextRange(lineStart, nextLineStart));
            }

            lineStart = nextLineStart;
        }

        return ranges;
    }

    public static bool IsUnsafeEdit(string? source, DocumentTextRange editRange)
    {
        foreach (var protectedRange in FindDataImageDefinitionRanges(source))
        {
            var touchesProtectedRange = editRange.IsEmpty
                ? protectedRange.Contains(editRange.Start)
                : protectedRange.Intersects(editRange);
            if (!touchesProtectedRange)
            {
                continue;
            }

            if (editRange.Start <= protectedRange.Start && editRange.End >= protectedRange.End)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsDataImageDefinitionLine(ReadOnlySpan<char> line)
    {
        var trimmed = TrimAsciiWhitespace(line);
        if (trimmed.Length == 0 || trimmed[0] != '[')
        {
            return false;
        }

        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 2)
        {
            return false;
        }

        var labelAndSeparator = trimmed[..colonIndex];
        if (labelAndSeparator[^1] != ']')
        {
            return false;
        }

        var target = TrimAsciiWhitespace(trimmed[(colonIndex + 1)..]);
        return target.StartsWith("<data:image/", StringComparison.OrdinalIgnoreCase)
            && target.EndsWith(">", StringComparison.Ordinal)
            && target.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static ReadOnlySpan<char> TrimAsciiWhitespace(ReadOnlySpan<char> value)
    {
        var start = 0;
        var end = value.Length;

        while (start < end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return value[start..end];
    }
}
