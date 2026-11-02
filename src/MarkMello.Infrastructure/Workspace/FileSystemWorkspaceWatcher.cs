using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Infrastructure.Workspace;

/// <summary>
/// Слежение за папкой через <see cref="FileSystemWatcher"/> с дебаунсом
/// (<see cref="ChangeBatcher"/>): дерево перечитывается пачкой, а не на каждое событие.
/// </summary>
public sealed class FileSystemWorkspaceWatcher : IWorkspaceWatcher
{
    private readonly ChangeBatcher _batcher;

    private FileSystemWatcher? _watcher;
    private bool _isDisposed;

    public FileSystemWorkspaceWatcher()
    {
        _batcher = new ChangeBatcher(batch => Changed?.Invoke(this, batch));
    }

    public event EventHandler<IReadOnlyList<WorkspaceChange>>? Changed;

    public void Start(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        StopWatching();

        if (!Directory.Exists(rootPath))
        {
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(rootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
            };

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;

            // Переполнение буфера — не отказ: подписчику уходит запрос на полное обновление.
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;

            _watcher = watcher;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Сетевой путь или нет прав: дерево просто не будет обновляться само.
            _watcher = null;
        }
    }

    public void StopWatching()
    {
        _batcher.Clear();

        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnCreated;
        _watcher.Changed -= OnChanged;
        _watcher.Deleted -= OnDeleted;
        _watcher.Renamed -= OnRenamed;
        _watcher.Error -= OnError;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
        => _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Created, e.FullPath));

    private void OnChanged(object sender, FileSystemEventArgs e)
        => _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Changed, e.FullPath));

    private void OnDeleted(object sender, FileSystemEventArgs e)
        => _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Deleted, e.FullPath));

    private void OnRenamed(object sender, RenamedEventArgs e)
        => _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Renamed, e.FullPath, e.OldFullPath));

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Буфер переполнен — часть событий потеряна. Честнее сообщить об изменении корня,
        // чем делать вид, что ничего не было.
        if (_watcher is { Path: { Length: > 0 } root })
        {
            _batcher.Add(new WorkspaceChange(WorkspaceChangeKind.Changed, root));
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopWatching();
        _batcher.Dispose();
    }
}
