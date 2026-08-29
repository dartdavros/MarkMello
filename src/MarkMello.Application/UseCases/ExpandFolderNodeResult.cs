using MarkMello.Domain.Workspace;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Результат раскрытия узла дерева. Ошибка локальна для узла: остальное дерево
/// и открытый документ продолжают работать.
/// </summary>
public abstract record ExpandFolderNodeResult
{
    private ExpandFolderNodeResult() { }

    public sealed record Success(IReadOnlyList<WorkspaceEntry> Children) : ExpandFolderNodeResult;
    public sealed record NotFound(string Path) : ExpandFolderNodeResult;
    public sealed record AccessDenied(string Path) : ExpandFolderNodeResult;
    public sealed record ReadError(string Path, string Message) : ExpandFolderNodeResult;
}
