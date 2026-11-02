using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Infrastructure.Workspace;

/// <summary>
/// Слежение за отдельными файлами: по одному <see cref="FileSystemWatcher"/> на каталог,
/// без подкаталогов. Файл может лежать в «Загрузках» или в корне диска — рекурсивное
/// наблюдение за таким каталогом стоило бы несоразмерно дорого.
///
/// Из событий каталога остаются только касающиеся отслеживаемых файлов, включая
/// переименование временного файла поверх отслеживаемого: так сохраняют многие
/// редакторы и агенты.
/// </summary>
public sealed class FileSystemDocumentWatcher : IDocumentWatcher
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(PathComparer);
    private readonly ChangeBatcher _batcher;
    private HashSet<string> _files = new(PathComparer);
    private bool _isDisposed;

    public FileSystemDocumentWatcher()
    {
        _batcher = new ChangeBatcher(batch => Changed?.Invoke(this, batch));
    }

    public event EventHandler<IReadOnlyList<WorkspaceChange>>? Changed;

    public void Watch(IReadOnlyCollection<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var files = new HashSet<string>(filePaths.Select(Path.GetFullPath), PathComparer);
        var directories = new HashSet<string>(
            files.Select(Path.GetDirectoryName).OfType<string>(),
            PathComparer);

        lock (_gate)
        {
            _files = files;

            foreach (var stale in _watchers.Keys.Where(directory => !directories.Contains(directory)).ToList())
            {
                DisposeWatcher(_watchers[stale]);
                _watchers.Remove(stale);
            }

            foreach (var directory in directories.Where(directory => !_watchers.ContainsKey(directory)))
            {
                if (TryCreateWatcher(directory) is { } watcher)
                {
                    _watchers[directory] = watcher;
                }
            }
        }

        if (files.Count == 0)
        {
            _batcher.Clear();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        lock (_gate)
        {
            foreach (var watcher in _watchers.Values)
            {
                DisposeWatcher(watcher);
            }

            _watchers.Clear();
            _files = new HashSet<string>(PathComparer);
        }

        _batcher.Dispose();
    }

    private FileSystemWatcher? TryCreateWatcher(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Сетевой путь или нет прав: документ просто не будет обновляться сам.
            return null;
        }
    }

    private void DisposeWatcher(FileSystemWatcher watcher)
    {
        watcher.EnableRaisingEvents = false;
        watcher.Created -= OnCreated;
        watcher.Changed -= OnChanged;
        watcher.Deleted -= OnDeleted;
        watcher.Renamed -= OnRenamed;
        watcher.Error -= OnError;
        watcher.Dispose();
    }

    private bool IsWatched(string? path)
    {
        if (path is null)
        {
            return false;
        }

        lock (_gate)
        {
            return _files.Contains(path);
        }
    }

    /// <summary>Файл создан заново (удалён и записан) — для вкладки это новое содержимое.</summary>
    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsWatched(e.FullPath))
        {
            _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Changed, e.FullPath));
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsWatched(e.FullPath))
        {
            _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Changed, e.FullPath));
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsWatched(e.FullPath))
        {
            _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Deleted, e.FullPath));
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsWatched(e.FullPath) || IsWatched(e.OldFullPath))
        {
            _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Renamed, e.FullPath, e.OldFullPath));
        }
    }

    /// <summary>
    /// Буфер переполнен — часть событий потеряна. Честнее считать изменёнными все файлы
    /// каталога, чем делать вид, что ничего не было.
    /// </summary>
    private void OnError(object sender, ErrorEventArgs e)
    {
        if (sender is not FileSystemWatcher { Path: { Length: > 0 } directory })
        {
            return;
        }

        List<string> affected;
        lock (_gate)
        {
            affected = _files
                .Where(file => PathComparer.Equals(Path.GetDirectoryName(file), directory))
                .ToList();
        }

        foreach (var file in affected)
        {
            _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Changed, file));
        }
    }
}
