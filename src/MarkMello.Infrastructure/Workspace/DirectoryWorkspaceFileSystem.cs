using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Infrastructure.Workspace;

/// <summary>
/// Чтение каталога через <see cref="Directory.EnumerateFileSystemEntries(string)"/>:
/// для локальных путей это дешевле, чем <c>IStorageFolder.GetItemsAsync</c>, и не тянет
/// Avalonia в инфраструктуру. Обход уходит в пул потоков, чтобы медленный или сетевой
/// путь не блокировал UI-поток.
/// </summary>
public sealed class DirectoryWorkspaceFileSystem : IWorkspaceFileSystem
{
    public bool DirectoryExists(string directoryPath)
        => !string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath);

    public async ValueTask<IReadOnlyList<WorkspaceEntry>> EnumerateChildrenAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        return await Task
            .Run(() => EnumerateChildrenCore(directoryPath, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<WorkspaceSearchResult> SearchByNameAsync(
        string rootPath,
        string query,
        WorkspaceSearchLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(limits);
        cancellationToken.ThrowIfCancellationRequested();

        return await Task
            .Run(() => SearchCore(rootPath, query, limits, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Обход в ширину с тремя ограничителями: глубина, число просмотренных элементов
    /// и число совпадений. Первый достигнутый лимит помечает выдачу неполной.
    /// </summary>
    private static WorkspaceSearchResult SearchCore(
        string rootPath,
        string query,
        WorkspaceSearchLimits limits,
        CancellationToken cancellationToken)
    {
        var hits = new List<WorkspaceSearchHit>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootPath, 0));

        var scanned = 0;
        var truncated = false;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (directory, depth) = queue.Dequeue();

            List<WorkspaceEntry> children;
            try
            {
                children = EnumerateChildrenCore(directory, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Недоступный подкаталог пропускаем: остальная выдача остаётся полезной.
                continue;
            }

            foreach (var entry in children)
            {
                scanned++;
                if (scanned > limits.MaxScannedEntries)
                {
                    return new WorkspaceSearchResult(hits, IsTruncated: true);
                }

                var matchStart = entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (matchStart >= 0 && (entry.IsDirectory || entry.IsSupportedDocument))
                {
                    // Не-документы в выдачу не попадают: открыть их всё равно нельзя.
                    if (hits.Count == limits.MaxMatches)
                    {
                        return new WorkspaceSearchResult(hits, IsTruncated: true);
                    }

                    hits.Add(new WorkspaceSearchHit(
                        entry,
                        BuildRelativeDirectory(rootPath, entry.Path),
                        matchStart,
                        query.Length));
                }

                if (entry.IsDirectory)
                {
                    if (depth + 1 <= limits.MaxDepth)
                    {
                        queue.Enqueue((entry.Path, depth + 1));
                    }
                    else
                    {
                        truncated = true;
                    }
                }
            }
        }

        return new WorkspaceSearchResult(hits, truncated);
    }

    /// <summary>Путь родителя относительно корня — подпись под строкой результата («docs/»).</summary>
    private static string BuildRelativeDirectory(string rootPath, string entryPath)
    {
        var directory = Path.GetDirectoryName(entryPath);
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        var root = Path.TrimEndingDirectorySeparator(rootPath);
        if (string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return directory;
        }

        var relative = directory[(root.Length + 1)..];
        return relative.Replace(Path.DirectorySeparatorChar, '/') + "/";
    }

    private static List<WorkspaceEntry> EnumerateChildrenCore(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var entries = new List<WorkspaceEntry>();

        foreach (var path in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name) || WorkspaceEntryFilter.IsDotPrefixedName(name))
            {
                continue;
            }

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Элемент исчез или недоступен между перечислением и чтением атрибутов:
                // пропускаем его, а не роняем весь узел.
                continue;
            }

            if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System))
            {
                continue;
            }

            var isDirectory = attributes.HasFlag(FileAttributes.Directory);
            if (isDirectory)
            {
                if (WorkspaceEntryFilter.IsIgnoredDirectoryName(name))
                {
                    continue;
                }

                entries.Add(WorkspaceEntry.ForDirectory(path, name));
            }
            else
            {
                entries.Add(WorkspaceEntry.ForFile(path, name));
            }
        }

        entries.Sort(WorkspaceEntryOrdering.Instance);
        return entries;
    }
}
