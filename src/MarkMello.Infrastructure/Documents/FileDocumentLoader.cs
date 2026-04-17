using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Infrastructure.Documents;

/// <summary>
/// Простой загрузчик файлов через <see cref="File.ReadAllTextAsync(string, CancellationToken)"/>.
/// В M0 не использует mmap или streaming — это вторичная оптимизация после бенчмарков (M3+).
/// </summary>
public sealed class FileDocumentLoader : IDocumentLoader
{
    public async Task<MarkdownSource> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Markdown file not found: {path}", path);
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return new MarkdownSource(
            Path: Path.GetFullPath(path),
            FileName: Path.GetFileName(path),
            Content: content);
    }
}
