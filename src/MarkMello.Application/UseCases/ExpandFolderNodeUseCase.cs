using MarkMello.Application.Abstractions;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Ленивое раскрытие каталога: читает ровно один уровень. Вызывается при первом
/// раскрытии узла, дальше дерево использует уже загруженных детей.
/// </summary>
public sealed class ExpandFolderNodeUseCase
{
    private readonly IWorkspaceFileSystem _fileSystem;

    public ExpandFolderNodeUseCase(IWorkspaceFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public async Task<ExpandFolderNodeResult> ExecuteAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return new ExpandFolderNodeResult.NotFound(directoryPath ?? string.Empty);
        }

        if (!_fileSystem.DirectoryExists(directoryPath))
        {
            return new ExpandFolderNodeResult.NotFound(directoryPath);
        }

        try
        {
            var children = await _fileSystem
                .EnumerateChildrenAsync(directoryPath, cancellationToken)
                .ConfigureAwait(false);

            return new ExpandFolderNodeResult.Success(children);
        }
        catch (DirectoryNotFoundException)
        {
            return new ExpandFolderNodeResult.NotFound(directoryPath);
        }
        catch (UnauthorizedAccessException)
        {
            return new ExpandFolderNodeResult.AccessDenied(directoryPath);
        }
        catch (IOException ex)
        {
            return new ExpandFolderNodeResult.ReadError(directoryPath, ex.Message);
        }
    }
}
