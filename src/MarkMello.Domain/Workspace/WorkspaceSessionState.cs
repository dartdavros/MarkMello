namespace MarkMello.Domain.Workspace;

/// <summary>
/// Состояние окна, переживающее перезапуск: какая папка была открыта, какие документы
/// лежали во вкладках и какие узлы дерева были раскрыты.
///
/// Хранится в общих настройках, но восстанавливается только по явному запросу:
/// холодный старт без аргументов остаётся single-file и папку сам не открывает
/// (ADR-0007 Rule 10).
/// </summary>
public sealed record WorkspaceSessionState(
    string? FolderPath,
    IReadOnlyList<string> OpenDocumentPaths,
    string? ActiveDocumentPath,
    IReadOnlyList<string> ExpandedDirectories)
{
    public static WorkspaceSessionState Empty { get; } = new(null, [], null, []);

    /// <summary>Отбрасывает пути, которых больше нет: сессия старше файловой системы.</summary>
    public WorkspaceSessionState WithExistingPaths(Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);

        var folder = FolderPath is not null && exists(FolderPath) ? FolderPath : null;
        var documents = OpenDocumentPaths.Where(exists).ToList();
        var active = ActiveDocumentPath is not null && documents.Contains(ActiveDocumentPath)
            ? ActiveDocumentPath
            : documents.FirstOrDefault();

        return new WorkspaceSessionState(
            folder,
            documents,
            active,
            folder is null ? [] : ExpandedDirectories.Where(exists).ToList());
    }
}
