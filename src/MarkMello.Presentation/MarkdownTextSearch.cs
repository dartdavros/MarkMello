using MarkMello.Domain;

namespace MarkMello.Presentation;

/// <summary>
/// Plain-text match enumeration shared by the viewer (rendered text map)
/// and the editor (source text). Matches are case-insensitive and returned
/// in document order with wrap-around navigation semantics.
/// </summary>
public static class MarkdownTextSearch
{
    public static IReadOnlyList<DocumentTextRange> FindAll(string text, string? query)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
        {
            return Array.Empty<DocumentTextRange>();
        }

        var matches = new List<DocumentTextRange>();
        var searchStart = 0;
        while (searchStart < text.Length)
        {
            var index = text.IndexOf(query, searchStart, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            matches.Add(new DocumentTextRange(index, index + query.Length));
            searchStart = index + query.Length;
        }

        return matches;
    }

    public static int NextIndex(int currentIndex, int matchCount)
    {
        if (matchCount <= 0)
        {
            return -1;
        }

        return currentIndex < 0
            ? 0
            : (currentIndex + 1) % matchCount;
    }

    public static int PreviousIndex(int currentIndex, int matchCount)
    {
        if (matchCount <= 0)
        {
            return -1;
        }

        return currentIndex <= 0
            ? matchCount - 1
            : currentIndex - 1;
    }
}
