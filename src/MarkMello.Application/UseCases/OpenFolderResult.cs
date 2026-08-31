using MarkMello.Domain.Workspace;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Результат открытия папки. Как и <see cref="OpenDocumentResult"/> — sealed union
/// вместо исключений: ошибка папки не должна ломать viewer (ADR-0007 Rule 14).
/// </summary>
public abstract record OpenFolderResult
{
    private OpenFolderResult() { }

    public sealed record Success(WorkspaceFolder Folder, IReadOnlyList<WorkspaceEntry> Children) : OpenFolderResult;
    public sealed record NotFound(string Path) : OpenFolderResult;
    public sealed record AccessDenied(string Path) : OpenFolderResult;
    public sealed record ReadError(string Path, string Message) : OpenFolderResult;
}
