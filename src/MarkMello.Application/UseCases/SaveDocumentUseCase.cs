using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Сохраняет markdown-документ по пути и маппит ошибки файловой системы
/// в типизированный <see cref="SaveDocumentResult"/>.
/// </summary>
public sealed class SaveDocumentUseCase
{
    private readonly IDocumentSaver _saver;

    public SaveDocumentUseCase(IDocumentSaver saver)
    {
        ArgumentNullException.ThrowIfNull(saver);
        _saver = saver;
    }

    public async Task<SaveDocumentResult> ExecuteAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null)
        {
            return new SaveDocumentResult.InvalidPath(path ?? string.Empty);
        }

        try
        {
            await _saver.SaveAsync(normalizedPath, content, cancellationToken).ConfigureAwait(false);

            return new SaveDocumentResult.Success(
                new MarkdownSource(
                    Path: normalizedPath,
                    FileName: Path.GetFileName(normalizedPath),
                    Content: content));
        }
        catch (UnauthorizedAccessException)
        {
            return new SaveDocumentResult.AccessDenied(normalizedPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            return new SaveDocumentResult.WriteError(normalizedPath, ex.Message);
        }
        catch (IOException ex)
        {
            return new SaveDocumentResult.WriteError(normalizedPath, ex.Message);
        }
        catch (ArgumentException)
        {
            return new SaveDocumentResult.InvalidPath(path ?? string.Empty);
        }
        catch (NotSupportedException)
        {
            return new SaveDocumentResult.InvalidPath(path ?? string.Empty);
        }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var normalized = Path.GetFullPath(path.Trim());
            if (string.IsNullOrWhiteSpace(Path.GetExtension(normalized)))
            {
                normalized += ".md";
            }

            return SupportedDocumentTypes.IsSupportedPath(normalized)
                ? normalized
                : null;
        }
        catch
        {
            return null;
        }
    }
}
