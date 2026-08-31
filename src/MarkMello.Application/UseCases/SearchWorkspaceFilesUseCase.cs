using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Поиск файлов по имени в открытой папке. Содержимое файлов не читается —
/// полнотекстовый поиск остаётся non-goal (ADR-0007 Rule 8).
/// </summary>
public sealed class SearchWorkspaceFilesUseCase
{
    private readonly IWorkspaceFileSystem _fileSystem;

    public SearchWorkspaceFilesUseCase(IWorkspaceFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public async Task<WorkspaceSearchResult> ExecuteAsync(
        string rootPath,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(query))
        {
            return WorkspaceSearchResult.Empty;
        }

        try
        {
            return await _fileSystem
                .SearchByNameAsync(rootPath, query.Trim(), WorkspaceSearchLimits.Default, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Недоступный подкаталог не должен ронять поиск: показываем то, что успели найти.
            return WorkspaceSearchResult.Empty;
        }
    }
}
