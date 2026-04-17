using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Открытие markdown-документа по пути. Превращает исключения IO в типизированный
/// <see cref="OpenDocumentResult"/>. Не зависит от UI framework.
/// </summary>
public sealed class OpenDocumentUseCase
{
    private readonly IDocumentLoader _loader;

    public OpenDocumentUseCase(IDocumentLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = loader;
    }

    public async Task<OpenDocumentResult> ExecuteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new OpenDocumentResult.NotFound(path ?? string.Empty);
        }

        if (!SupportedDocumentTypes.IsSupportedPath(path))
        {
            return new OpenDocumentResult.UnsupportedType(path);
        }

        try
        {
            var source = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
            return new OpenDocumentResult.Success(source);
        }
        catch (FileNotFoundException)
        {
            return new OpenDocumentResult.NotFound(path);
        }
        catch (DirectoryNotFoundException)
        {
            return new OpenDocumentResult.NotFound(path);
        }
        catch (UnauthorizedAccessException)
        {
            return new OpenDocumentResult.AccessDenied(path);
        }
        catch (IOException ex)
        {
            return new OpenDocumentResult.ReadError(path, ex.Message);
        }
    }
}
