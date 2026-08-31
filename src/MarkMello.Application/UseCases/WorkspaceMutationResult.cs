using MarkMello.Domain.Workspace;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Результат файловой операции в дереве. Как и при открытии — union вместо исключений:
/// ошибка операции показывается локально и не ломает ни дерево, ни документ (ADR-0007 Rule 7).
/// </summary>
public abstract record WorkspaceMutationResult
{
    private WorkspaceMutationResult() { }

    public sealed record Success(WorkspaceEntry Entry) : WorkspaceMutationResult;

    /// <summary>Удаление прошло: возвращать нечего, кроме самого факта.</summary>
    public sealed record Deleted(string Path, bool MovedToTrash) : WorkspaceMutationResult;

    /// <summary>
    /// Корзина недоступна, элемент на месте. Вызывающая сторона обязана переспросить
    /// с текстом про безвозвратное удаление, а не удалять молча.
    /// </summary>
    public sealed record TrashUnavailable(string Path) : WorkspaceMutationResult;

    public sealed record NameTaken(string Name, bool IsDirectory) : WorkspaceMutationResult;
    public sealed record InvalidName(WorkspaceNameProblem Problem) : WorkspaceMutationResult;
    public sealed record NotFound(string Path) : WorkspaceMutationResult;
    public sealed record AccessDenied(string Path) : WorkspaceMutationResult;
    public sealed record Failed(string Path, string Message) : WorkspaceMutationResult;
}
