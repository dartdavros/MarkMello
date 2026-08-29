using MarkMello.Domain.Workspace;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Чтение содержимого каталога для дерева файлов. Отдельная абстракция от
/// <see cref="IDocumentLoader"/>: дерево знает про структуру, но не читает документы.
/// </summary>
public interface IWorkspaceFileSystem
{
    /// <summary>
    /// Дети одного каталога: каталоги, затем файлы. Служебные и скрытые элементы отфильтрованы.
    /// Рекурсии нет — уровень читается по требованию (ADR-0007 Rule 5).
    /// </summary>
    ValueTask<IReadOnlyList<WorkspaceEntry>> EnumerateChildrenAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>Существует ли каталог. Нужен, чтобы отличить «папку удалили» от ошибки чтения.</summary>
    bool DirectoryExists(string directoryPath);
}
