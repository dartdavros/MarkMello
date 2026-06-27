using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal static class MarkdownLocalFileLinkResolver
{
    public static bool TryResolve(string? linkUrl, string? baseDirectory, out string targetPath)
    {
        targetPath = string.Empty;
        if (string.IsNullOrWhiteSpace(linkUrl) || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return false;
        }

        var pathPart = GetPathPart(linkUrl.Trim());
        if (string.IsNullOrWhiteSpace(pathPart)
            || Uri.TryCreate(pathPart, UriKind.Absolute, out _)
            || Path.IsPathRooted(pathPart))
        {
            return false;
        }

        string resolvedPath;
        try
        {
            var decodedPath = Uri.UnescapeDataString(pathPart);
            resolvedPath = Path.GetFullPath(Path.Combine(baseDirectory, decodedPath));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }

        if (!SupportedDocumentTypes.IsSupportedPath(resolvedPath) || !File.Exists(resolvedPath))
        {
            return false;
        }

        targetPath = resolvedPath;
        return true;
    }

    private static string GetPathPart(string linkUrl)
    {
        var end = linkUrl.Length;
        var fragmentIndex = linkUrl.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex >= 0)
        {
            end = Math.Min(end, fragmentIndex);
        }

        var queryIndex = linkUrl.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            end = Math.Min(end, queryIndex);
        }

        return linkUrl[..end];
    }
}
