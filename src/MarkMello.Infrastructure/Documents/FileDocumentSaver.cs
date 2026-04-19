using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Documents;

/// <summary>
/// Записывает markdown-файл на диск через временный файл в целевой директории,
/// чтобы не оставлять частично переписанный документ при сбое.
/// </summary>
public sealed class FileDocumentSaver : IDocumentSaver
{
    public async Task SaveAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Document path must contain a target directory.", nameof(path));
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
