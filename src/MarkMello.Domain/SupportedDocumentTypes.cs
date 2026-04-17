namespace MarkMello.Domain;

/// <summary>
/// Единый список поддерживаемых document types для file-first сценариев M2.
/// Используется всеми путями открытия: command line, picker, drag &amp; drop.
/// </summary>
public static class SupportedDocumentTypes
{
    public static IReadOnlyList<string> Extensions { get; } = new[]
    {
        ".md",
        ".markdown",
        ".txt"
    };

    public static bool IsSupportedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        foreach (var candidate in Extensions)
        {
            if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
