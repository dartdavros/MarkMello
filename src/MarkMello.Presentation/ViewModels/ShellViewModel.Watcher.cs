using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Реакция на внешние изменения в открытой папке. Дерево обновляется само,
/// а открытые вкладки ведут себя по-разному в зависимости от того, есть ли в них
/// несохранённые правки — молча перезагружать документ поверх правок нельзя (ADR-0007 Rule 9).
/// </summary>
public partial class ShellViewModel
{
    private IWorkspaceWatcher? _watcher;

    /// <summary>Активная вкладка изменилась на диске, а в ней есть несохранённые правки.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExternalChangeContent))]
    private bool _showsExternalChangeBanner;

    /// <summary>Плашка внешнего изменения появляется по событию, а не с запуска приложения.</summary>
    public object? ExternalChangeContent => ShowsExternalChangeBanner ? this : null;

    public string ExternalChangeTitle => _localization["ExternalChangeTitle"];

    public string ExternalChangeReload => _localization["ExternalChangeReload"];

    public string ExternalChangeKeep => _localization["ExternalChangeKeep"];

    /// <summary>Watcher создаётся вместе с папкой — в startup path его нет.</summary>
    private void StartWatching(string rootPath)
    {
        _watcher ??= _watcherFactory();
        _watcher.Changed -= OnWorkspaceFilesChanged;
        _watcher.Changed += OnWorkspaceFilesChanged;
        _watcher.Start(rootPath);
    }

    private void StopWatching()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Changed -= OnWorkspaceFilesChanged;
        _watcher.StopWatching();
    }

    /// <summary>События приходят из потока файловой системы — переносим их в UI-поток.</summary>
    private void OnWorkspaceFilesChanged(object? sender, IReadOnlyList<WorkspaceChange> changes)
        => Dispatcher.UIThread.Post(() => _ = ApplyWorkspaceChangesAsync(changes));

    internal async Task ApplyWorkspaceChangesAsync(IReadOnlyList<WorkspaceChange> changes)
    {
        if (Workspace is not { } workspace)
        {
            return;
        }

        await ApplyTabChangesAsync(changes).ConfigureAwait(true);

        // Каталоги перечитываем по одному разу, даже если в пачке двадцать событий из одного места.
        foreach (var directory in changes.Select(static change => change.AffectedDirectory).Distinct(PathComparer))
        {
            await workspace.RefreshDirectoryAsync(directory).ConfigureAwait(true);
        }

        SyncWorkspaceActiveDocument();
    }

    /// <summary>
    /// Реакция открытых вкладок на внешние изменения — общая для папки и для файлов,
    /// открытых без неё (<see cref="ApplyDocumentChangesAsync"/>).
    /// </summary>
    private async Task ApplyTabChangesAsync(IReadOnlyList<WorkspaceChange> changes)
    {
        // В одной пачке бывает несколько событий об одном файле: перечитываем его один раз.
        var reloaded = new HashSet<string>(PathComparer);

        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case WorkspaceChangeKind.Renamed when change.PreviousPath is { } previous:
                    // Атомарное сохранение (так пишут многие редакторы и агенты): временный файл
                    // переименовывается поверх открытого. Для вкладки это новое содержимое, а не переезд.
                    var replacesOpenTab = OpenDocuments.FindByPath(change.Path) is not null;
                    RetargetTabsUnderPath(previous, change.Path);
                    if (replacesOpenTab && reloaded.Add(change.Path))
                    {
                        await HandleExternalContentChangeAsync(change.Path).ConfigureAwait(true);
                    }

                    break;

                // Удалили и сразу записали заново — тоже сохранение: файл на месте, значит изменился.
                case WorkspaceChangeKind.Deleted when _fileExists(change.Path):
                case WorkspaceChangeKind.Changed:
                    if (reloaded.Add(change.Path))
                    {
                        await HandleExternalContentChangeAsync(change.Path).ConfigureAwait(true);
                    }

                    break;

                case WorkspaceChangeKind.Deleted:
                    await HandleExternalDeleteAsync(change.Path).ConfigureAwait(true);
                    break;
            }
        }
    }

    /// <summary>
    /// Удалённый снаружи файл: чистая вкладка закрывается, грязная остаётся с пометкой —
    /// иначе правки исчезли бы вместе с чужим удалением.
    /// </summary>
    private async Task HandleExternalDeleteAsync(string path)
    {
        foreach (var tab in OpenDocuments.Tabs.ToList())
        {
            if (tab.Path is not { } tabPath || !(PathsMatch(tabPath, path) || IsUnderDirectory(tabPath, path)))
            {
                continue;
            }

            if (tab.EditorSession?.IsDirty == true)
            {
                tab.StateSuffix = _localization["TabDeletedSuffix"];
                continue;
            }

            await RemoveTabAsync(tab).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Изменение содержимого: чистая активная вкладка перезагружается молча, чистая фоновая
    /// помечается устаревшей и перечитается при возврате, грязная показывает плашку с выбором.
    /// </summary>
    private async Task HandleExternalContentChangeAsync(string path)
    {
        var tab = OpenDocuments.FindByPath(path);
        if (tab is null)
        {
            return;
        }

        // Своё же сохранение из редактора watcher тоже видит. Перечитывать нечего,
        // а перезагрузка выкинула бы пользователя из режима правки.
        if (tab.EditorSession is { IsDirty: false } session
            && await IsDiskContentAsync(path, session.SourceText).ConfigureAwait(true))
        {
            return;
        }

        if (tab.EditorSession?.IsDirty == true)
        {
            tab.HasExternalChange = true;

            if (ReferenceEquals(tab, OpenDocuments.ActiveTab))
            {
                ShowsExternalChangeBanner = true;
            }

            return;
        }

        if (!ReferenceEquals(tab, OpenDocuments.ActiveTab))
        {
            // Фоновую вкладку не перечитываем: на неё никто не смотрит.
            tab.NeedsReload = true;
            return;
        }

        var offset = tab.ScrollOffset;
        await LoadDocumentAsync(path, preserveEditModeAfterLoad: false).ConfigureAwait(true);

        // Позиция чтения не должна прыгать в начало из-за чужого сохранения.
        _pendingScrollOffset = offset;
    }

    [RelayCommand]
    private async Task ReloadExternalChangeAsync()
    {
        if (OpenDocuments.ActiveTab is not { Path: { } path } tab)
        {
            return;
        }

        tab.HasExternalChange = false;
        ShowsExternalChangeBanner = false;

        var offset = tab.ScrollOffset;
        tab.EditorSession?.Dispose();
        tab.EditorSession = null;
        tab.IsEditMode = false;
        EditorSession = null;
        IsEditMode = false;

        await LoadDocumentAsync(path, preserveEditModeAfterLoad: false).ConfigureAwait(true);
        _pendingScrollOffset = offset;
    }

    /// <summary>«Оставить мои правки»: плашка уходит, файл на диске трогать не надо.</summary>
    [RelayCommand]
    private void KeepExternalChange()
    {
        if (OpenDocuments.ActiveTab is { } tab)
        {
            tab.HasExternalChange = false;
        }

        ShowsExternalChangeBanner = false;
    }

    private async Task<bool> IsDiskContentAsync(string path, string expected)
    {
        var result = await _openDocument.ExecuteAsync(path).ConfigureAwait(true);
        return result is OpenDocumentResult.Success success
            && string.Equals(
                success.Source.Content.ReplaceLineEndings("\n"),
                expected.ReplaceLineEndings("\n"),
                StringComparison.Ordinal);
    }

    private void SyncExternalChangeBanner()
        => ShowsExternalChangeBanner = OpenDocuments.ActiveTab?.HasExternalChange == true;

    private static IEqualityComparer<string> PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
