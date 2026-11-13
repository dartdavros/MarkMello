using Avalonia.Threading;
using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Внешние изменения файлов, открытых без папки: двойным кликом, через «Открыть файл»,
/// перетаскиванием. Watcher папки их не видит, поэтому за ними следит отдельный,
/// а реакция вкладок та же, что в папке: чистая перечитывается, грязная спрашивает.
/// </summary>
public partial class ShellViewModel
{
    private IDocumentWatcher? _documentWatcher;

    /// <summary>
    /// Окно закрыто. Обработчик, уже стоящий в UI-очереди или ждущий после await,
    /// не должен ни менять вкладки, ни запускать наблюдение заново.
    /// </summary>
    private bool _isWatchingStopped;

    /// <summary>
    /// Пересобирает набор отслеживаемых файлов по вкладкам. Файлы внутри открытой
    /// папки сюда не попадают — их уже видит watcher папки.
    /// </summary>
    private void SyncDocumentWatch()
    {
        if (_documentWatcherFactory is null || _isWatchingStopped)
        {
            return;
        }

        var workspace = Workspace;
        var paths = OpenDocuments.Tabs
            .Select(static tab => tab.Path)
            .OfType<string>()
            .Where(path => workspace is null || !IsInsideWorkspace(path, workspace.Folder))
            .Distinct(PathComparer)
            .ToList();

        // Watcher появляется с первым отдельным файлом, а не на старте.
        if (_documentWatcher is null)
        {
            if (paths.Count == 0)
            {
                return;
            }

            _documentWatcher = _documentWatcherFactory();
            _documentWatcher.Changed += OnDocumentFilesChanged;
        }

        _documentWatcher.Watch(paths);
    }

    /// <summary>Окно закрыто: наблюдение за файлами больше никому не нужно.</summary>
    public void StopWatchingFiles()
    {
        _isWatchingStopped = true;
        StopWatching();

        if (_documentWatcher is null)
        {
            return;
        }

        _documentWatcher.Changed -= OnDocumentFilesChanged;
        _documentWatcher.Dispose();
        _documentWatcher = null;
    }

    /// <summary>События приходят из потока файловой системы — переносим их в UI-поток.</summary>
    private void OnDocumentFilesChanged(object? sender, IReadOnlyList<WorkspaceChange> changes)
        => Dispatcher.UIThread.Post(() => _ = ApplyDocumentChangesAsync(changes));

    internal async Task ApplyDocumentChangesAsync(IReadOnlyList<WorkspaceChange> changes)
    {
        if (_isWatchingStopped)
        {
            return;
        }

        await ApplyTabChangesAsync(changes).ConfigureAwait(true);

        // Переименование уводит вкладку на новый путь — следить надо уже за ним.
        SyncDocumentWatch();
    }
}
