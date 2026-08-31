using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Infrastructure.Workspace;

/// <summary>
/// Слежение за папкой через <see cref="FileSystemWatcher"/> с дебаунсом.
///
/// Сырые события приходят пачками: одно сохранение файла редактором даёт три-четыре
/// уведомления, а распаковка архива — сотни. Поэтому события копятся 250 мс и уходят
/// подписчику одним списком; без этого дерево перечитывалось бы на каждый чих.
/// </summary>
public sealed class FileSystemWorkspaceWatcher : IWorkspaceWatcher
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(250);

    private readonly Lock _gate = new();
    private readonly List<WorkspaceChange> _pending = [];

    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _isDisposed;

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
        lock (_gate)
        {
            _pending.Clear();
        }

        _debounce?.Dispose();
        _debounce = null;

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
        => Enqueue(new WorkspaceChange(WorkspaceChangeKind.Created, e.FullPath));

    private void OnChanged(object sender, FileSystemEventArgs e)
        => Enqueue(new WorkspaceChange(WorkspaceChangeKind.Changed, e.FullPath));

    private void OnDeleted(object sender, FileSystemEventArgs e)
        => Enqueue(new WorkspaceChange(WorkspaceChangeKind.Deleted, e.FullPath));

    private void OnRenamed(object sender, RenamedEventArgs e)
        => Enqueue(new WorkspaceChange(WorkspaceChangeKind.Renamed, e.FullPath, e.OldFullPath));

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Буфер переполнен — часть событий потеряна. Честнее сообщить об изменении корня,
        // чем делать вид, что ничего не было.
        if (_watcher is { Path: { Length: > 0 } root })
        {
            Enqueue(new WorkspaceChange(WorkspaceChangeKind.Changed, root));
        }
    }

    private void Enqueue(WorkspaceChange change)
    {
        lock (_gate)
        {
            _pending.Add(change);
            _debounce ??= new Timer(Flush, state: null, DebounceInterval, Timeout.InfiniteTimeSpan);
            _debounce.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush(object? state)
    {
        List<WorkspaceChange> batch;

        lock (_gate)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            batch = [.. _pending];
            _pending.Clear();
        }

        Changed?.Invoke(this, batch);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopWatching();
    }
}
