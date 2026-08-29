using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Каталоги в памяти: тесты дерева не должны трогать диск, иначе они начинают
/// зависеть от прав, регистра имён и мусора в temp.
/// </summary>
internal sealed class FakeWorkspaceFileSystem : IWorkspaceFileSystem
{
    private readonly Dictionary<string, IReadOnlyList<WorkspaceEntry>> _directories =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);

    public List<string> EnumeratedPaths { get; } = [];

    public void AddDirectory(string path, params WorkspaceEntry[] children)
        => _directories[path] = children;

    public void FailWith(string path, Exception exception)
    {
        _directories[path] = [];
        _failures[path] = exception;
    }

    public bool DirectoryExists(string directoryPath) => _directories.ContainsKey(directoryPath);

    public ValueTask<IReadOnlyList<WorkspaceEntry>> EnumerateChildrenAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        EnumeratedPaths.Add(directoryPath);

        if (_failures.TryGetValue(directoryPath, out var failure))
        {
            throw failure;
        }

        return ValueTask.FromResult(
            _directories.TryGetValue(directoryPath, out var children)
                ? children
                : []);
    }
}

/// <summary>
/// Загрузчик, считающий обращения: переключение вкладок не должно читать файл заново.
/// </summary>
internal sealed class CountingDocumentLoader : MarkMello.Application.Abstractions.IDocumentLoader
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
