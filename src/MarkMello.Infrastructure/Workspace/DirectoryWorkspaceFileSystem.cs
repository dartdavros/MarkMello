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
