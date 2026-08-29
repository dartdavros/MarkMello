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
    private bool _isRestoringSession;

    /// <summary>
    /// Собирает и сохраняет снимок. Вызывается на изменение состава вкладок и активной
    /// вкладки — записывается маленький json, отдельного дебаунса не требуется.
    /// </summary>
    private async Task PersistSessionAsync()
    {
        if (_isRestoringSession || Workspace is not { } workspace)
        {
            return;
        }

        var session = new WorkspaceSessionState(
            workspace.Folder.RootPath,
            OpenDocuments.Tabs.Select(static tab => tab.Path).OfType<string>().ToList(),
            OpenDocuments.ActiveTab?.Path,
            workspace.GetExpandedDirectories());

        await _settings.SaveSessionAsync(session).ConfigureAwait(true);
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
