using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Состояние сессии окна: какие документы были открыты в папке и какие узлы раскрыты.
///
/// Восстанавливается не при старте, а при повторном открытии той же папки: холодный
/// старт без аргументов обязан остаться single-file (ADR-0007 Rule 10). Так сохранённое
/// состояние приносит пользу и при этом не превращается в автозапуск workspace.
/// </summary>
public partial class ShellViewModel
{
    /// <summary>
    /// Пауза перед записью. Открытие папки меняет состав вкладок несколько раз подряд;
    /// без паузы каждая правка превращалась бы в отдельную запись файла.
    /// </summary>
    private static readonly TimeSpan SessionWriteDelay = TimeSpan.FromMilliseconds(400);

    private bool _isRestoringSession;
    private CancellationTokenSource? _sessionWriteCancellation;

    /// <summary>
    /// Собирает снимок и планирует запись. Запись уходит с UI-потока и коалесцируется:
    /// <c>JsonSettingsStore</c> пишет файл синхронно, и на замере это стоило 50 мс
    /// прямо посреди раскрытия узла дерева.
    /// </summary>
    private Task PersistSessionAsync()
    {
        if (_isRestoringSession || Workspace is not { } workspace)
        {
            return Task.CompletedTask;
        }

        var session = new WorkspaceSessionState(
            workspace.Folder.RootPath,
            OpenDocuments.Tabs.Select(static tab => tab.Path).OfType<string>().ToList(),
            OpenDocuments.ActiveTab?.Path,
            workspace.GetExpandedDirectories());

        _sessionWriteCancellation?.Cancel();
        _sessionWriteCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _sessionWriteCancellation = cancellation;

        return WriteSessionAfterDelayAsync(session, cancellation.Token);
    }

    private async Task WriteSessionAfterDelayAsync(WorkspaceSessionState session, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SessionWriteDelay, cancellationToken).ConfigureAwait(false);
            await Task
                .Run(() => _settings.SaveSessionAsync(session, cancellationToken).AsTask(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Состояние успело измениться — пишет уже следующий вызов.
        }
    }

    /// <summary>
    /// Восстанавливает вкладки и раскрытые узлы, если открыта та же папка, что в прошлый раз.
    /// Пути, которых больше нет, отбрасываются: сессия старше файловой системы.
    /// </summary>
    private async Task TryRestoreSessionAsync(WorkspaceViewModel workspace)
    {
        var stored = await _settings.LoadSessionAsync().ConfigureAwait(true);
        if (stored.FolderPath is null || !PathsMatch(stored.FolderPath, workspace.Folder.RootPath))
        {
            return;
        }

        var session = stored.WithExistingPaths(_fileExists);
        if (session.OpenDocumentPaths.Count == 0 && session.ExpandedDirectories.Count == 0)
        {
            return;
        }

        _isRestoringSession = true;
        try
        {
            foreach (var directory in session.ExpandedDirectories)
            {
                await workspace.RevealAsync(directory).ConfigureAwait(true);
            }

            foreach (var path in session.OpenDocumentPaths)
            {
                await LoadDocumentAsync(path, preserveEditModeAfterLoad: false).ConfigureAwait(true);
            }

            if (session.ActiveDocumentPath is { } activePath
                && OpenDocuments.FindByPath(activePath) is { } activeTab
                && !ReferenceEquals(OpenDocuments.ActiveTab, activeTab))
            {
                await RestoreTabAsync(activeTab).ConfigureAwait(true);
            }
        }
        finally
        {
            _isRestoringSession = false;
        }
    }
}
