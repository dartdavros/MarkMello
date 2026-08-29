using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Каталоги в памяти: тесты дерева не должны трогать диск, иначе они начинают
/// зависеть от прав, регистра имён и мусора в temp. Файловые операции меняют
/// ту же карту, поэтому дерево после них обновляется как в жизни.
/// </summary>
internal sealed class FakeWorkspaceFileSystem : IWorkspaceFileSystem
{
    private readonly Dictionary<string, List<WorkspaceEntry>> _directories =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);

    public List<string> EnumeratedPaths { get; } = [];

    /// <summary>Заранее подготовленный ответ поиска: обход диска в тестах не нужен.</summary>
    public WorkspaceSearchResult SearchResult { get; set; } = WorkspaceSearchResult.Empty;

    public List<string> SearchQueries { get; } = [];

    public List<string> DeletedPaths { get; } = [];

    public void AddDirectory(string path, params WorkspaceEntry[] children)
        => _directories[path] = [.. children];

    public void FailWith(string path, Exception exception)
    {
        _directories[path] = [];
        _failures[path] = exception;
    }

    public bool DirectoryExists(string directoryPath) => _directories.ContainsKey(directoryPath);

    public bool Exists(string path)
        => _directories.ContainsKey(path)
            || _directories.Values.Any(children =>
                children.Any(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase)));

    public ValueTask<IReadOnlyList<WorkspaceEntry>> EnumerateChildrenAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        EnumeratedPaths.Add(directoryPath);

        if (_failures.TryGetValue(directoryPath, out var failure))
        {
            throw failure;
        }

        IReadOnlyList<WorkspaceEntry> children = _directories.TryGetValue(directoryPath, out var entries)
            ? [.. entries]
            : [];

        return ValueTask.FromResult(children);
    }

    public ValueTask<WorkspaceEntry> CreateFileAsync(
        string directoryPath,
        string name,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Add(directoryPath, WorkspaceEntry.ForFile(Path.Combine(directoryPath, name), name)));

    public ValueTask<WorkspaceEntry> CreateDirectoryAsync(
        string directoryPath,
        string name,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(directoryPath, name);
        _directories[path] = [];
        return ValueTask.FromResult(Add(directoryPath, WorkspaceEntry.ForDirectory(path, name)));
    }

    public ValueTask<WorkspaceEntry> RenameAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var existing = Find(path) ?? throw new FileNotFoundException("Entry was not found.", path);
        var target = Path.Combine(directory, newName);

        Remove(path);

        var renamed = existing.IsDirectory
            ? WorkspaceEntry.ForDirectory(target, newName)
            : WorkspaceEntry.ForFile(target, newName);

        if (existing.IsDirectory && _directories.Remove(path, out var children))
        {
            _directories[target] = children;
        }

        return ValueTask.FromResult(Add(directory, renamed));
    }

    public ValueTask<WorkspaceEntry> DuplicateAsync(
        string path,
        string duplicateName,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var existing = Find(path) ?? throw new FileNotFoundException("Entry was not found.", path);
        var target = Path.Combine(directory, duplicateName);

        var copy = existing.IsDirectory
            ? WorkspaceEntry.ForDirectory(target, duplicateName)
            : WorkspaceEntry.ForFile(target, duplicateName);

        return ValueTask.FromResult(Add(directory, copy));
    }

    public ValueTask DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        DeletedPaths.Add(path);
        Remove(path);
        _directories.Remove(path);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<string>> GetChildNamesAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> names = _directories.TryGetValue(directoryPath, out var entries)
            ? entries.Select(entry => entry.Name).ToList()
            : [];

        return ValueTask.FromResult(names);
    }

    public ValueTask<int> CountChildrenAsync(string directoryPath, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_directories.TryGetValue(directoryPath, out var entries) ? entries.Count : 0);

    public ValueTask<WorkspaceSearchResult> SearchByNameAsync(
        string rootPath,
        string query,
        WorkspaceSearchLimits limits,
        CancellationToken cancellationToken = default)
    {
        SearchQueries.Add(query);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SearchResult);
    }

    private WorkspaceEntry Add(string directoryPath, WorkspaceEntry entry)
    {
        if (!_directories.TryGetValue(directoryPath, out var children))
        {
            children = [];
            _directories[directoryPath] = children;
        }

        children.Add(entry);
        children.Sort(WorkspaceEntryOrdering.Instance);
        return entry;
    }

    private WorkspaceEntry? Find(string path)
        => _directories.Values
            .SelectMany(static children => children)
            .FirstOrDefault(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));

    private void Remove(string path)
    {
        foreach (var children in _directories.Values)
        {
            children.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
        }
    }
}

/// <summary>
/// Платформа в тестах: корзина по умолчанию доступна, но её можно выключить,
/// чтобы проверить второе подтверждение про безвозвратное удаление.
/// </summary>
internal sealed class FakePlatformServices : IPlatformServices
{
    private readonly FakeWorkspaceFileSystem? _fileSystem;

    public FakePlatformServices(FakeWorkspaceFileSystem? fileSystem = null) => _fileSystem = fileSystem;

    public string PlatformName => "Windows";

    public TrashResult TrashResult { get; set; } = TrashResult.Trashed;

    public List<string> TrashedPaths { get; } = [];

    public List<string> RevealedPaths { get; } = [];

    public async ValueTask<TrashResult> MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        if (TrashResult != TrashResult.Trashed)
        {
            return TrashResult;
        }

        TrashedPaths.Add(path);

        // Корзина ОС убирает элемент с исходного места — фейк обязан делать то же,
        // иначе дерево после удаления выглядит нетронутым только в тестах.
        if (_fileSystem is not null)
        {
            await _fileSystem.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            _fileSystem.DeletedPaths.Remove(path);
        }

        return TrashResult;
    }

    public ValueTask RevealInFileManagerAsync(string path, CancellationToken cancellationToken = default)
    {
        RevealedPaths.Add(path);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Загрузчик, считающий обращения: переключение вкладок не должно читать файл заново.
/// </summary>
internal sealed class CountingDocumentLoader : IDocumentLoader
{
    public Dictionary<string, MarkMello.Domain.MarkdownSource> Sources { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int LoadCount { get; private set; }

    public Task<MarkMello.Domain.MarkdownSource> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        LoadCount++;

        return Sources.TryGetValue(path, out var source)
            ? Task.FromResult(source)
            : Task.FromException<MarkMello.Domain.MarkdownSource>(
                new FileNotFoundException("Document was not found.", path));
    }
}
