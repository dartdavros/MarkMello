using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Открытие папки как рабочего контекста: валидация пути и чтение только корневого уровня.
/// Рекурсивного обхода нет — вложенные каталоги читаются при раскрытии
/// через <see cref="ExpandFolderNodeUseCase"/>.
/// </summary>
public sealed class OpenFolderUseCase
{
    private readonly IWorkspaceFileSystem _fileSystem;

    public OpenFolderUseCase(IWorkspaceFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public async Task<OpenFolderResult> ExecuteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new OpenFolderResult.NotFound(path ?? string.Empty);
        }

        if (!_fileSystem.DirectoryExists(path))
        {
            return new OpenFolderResult.NotFound(path);
        }

        try
        {
            var children = await _fileSystem
                .EnumerateChildrenAsync(path, cancellationToken)
                .ConfigureAwait(false);

            return new OpenFolderResult.Success(WorkspaceFolder.Create(path), children);
        }
        catch (DirectoryNotFoundException)
        {
            return new OpenFolderResult.NotFound(path);
        }
        catch (UnauthorizedAccessException)
        {
            return new OpenFolderResult.AccessDenied(path);
        }
        catch (IOException ex)
        {
            return new OpenFolderResult.ReadError(path, ex.Message);
        }
    }
}
