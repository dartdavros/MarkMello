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

    /// <summary>
    /// Поиск по подстроке имени внутри открытой папки. Обход ограничен лимитами и
    /// прерывается токеном: каждое нажатие клавиши отменяет предыдущий запрос.
    /// </summary>
    /// <summary>Создаёт пустой файл. Возвращает запись дерева для только что созданного элемента.</summary>
    ValueTask<WorkspaceEntry> CreateFileAsync(string directoryPath, string name, CancellationToken cancellationToken = default);

    ValueTask<WorkspaceEntry> CreateDirectoryAsync(string directoryPath, string name, CancellationToken cancellationToken = default);

    ValueTask<WorkspaceEntry> RenameAsync(string path, string newName, CancellationToken cancellationToken = default);

    /// <summary>Копия рядом с оригиналом; имя копии выбирает вызывающая сторона.</summary>
    ValueTask<WorkspaceEntry> DuplicateAsync(string path, string duplicateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Безвозвратное удаление. Обычный путь — корзина через <c>IPlatformServices</c>;
    /// этот метод нужен там, где корзины нет и пользователь согласился на потерю.
    /// </summary>
    ValueTask DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Существует ли файл или каталог по этому пути — проверка занятого имени.</summary>
    bool Exists(string path);

    /// <summary>Имена внутри каталога: нужны, чтобы подобрать свободное имя копии.</summary>
    ValueTask<IReadOnlyList<string>> GetChildNamesAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>Число элементов верхнего уровня — для текста подтверждения удаления, без рекурсии.</summary>
    ValueTask<int> CountChildrenAsync(string directoryPath, CancellationToken cancellationToken = default);

    ValueTask<WorkspaceSearchResult> SearchByNameAsync(
        string rootPath,
        string query,
        WorkspaceSearchLimits limits,
        CancellationToken cancellationToken = default);
}
