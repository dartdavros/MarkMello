using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.UseCases;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Режим открытой папки. Всё, что здесь есть, включается только по явной команде
/// «Открыть папку»: при старте с одним файлом <see cref="Workspace"/> остаётся null,
/// сайдбар не монтируется, каталоги не читаются (ADR-0007 Rule 1).
/// </summary>
public partial class ShellViewModel
{
    /// <summary>
    /// Папка открыта. Дерево, поиск и файловые операции живут внутри
    /// <see cref="WorkspaceViewModel"/>, поэтому single-file режим не платит за них ничем.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSidebar))]
    [NotifyPropertyChangedFor(nameof(SidebarContent))]
    [NotifyPropertyChangedFor(nameof(ShowsFloatingAppMenuButton))]
    private WorkspaceViewModel? _workspace;

    [ObservableProperty]
    private double _sidebarWidth = WorkspaceSidebarWidth.Default;

    private double _persistedSidebarWidth = WorkspaceSidebarWidth.Default;

    public bool ShowsSidebar => Workspace is not null;

    /// <summary>
    /// Содержимое сайдбара для ленивого <c>ContentControl</c>. До открытия папки — null,
    /// поэтому в single-file режиме контрол вообще не создаётся (ADR-0007 Rule 3):
    /// раньше он висел в дереве скрытым и стоил памяти на каждом старте.
    /// </summary>
    public object? SidebarContent => Workspace is null ? null : this;

    /// <summary>
    /// Плавающий гамбургер живёт только без сайдбара: при открытой папке его роль
    /// исполняет кнопка в шапке сайдбара (макет 03).
    /// </summary>
    public bool ShowsFloatingAppMenuButton => ShowsAppMenuControl && !ShowsSidebar;

    public bool CanCloseFolder => Workspace is not null;

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        CloseOverlayCore();

        var path = await _filePicker.PickFolderAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        await OpenFolderPathAsync(path).ConfigureAwait(true);
    }

    /// <summary>
    /// Открывает папку по готовому пути: picker, drag&amp;drop и аргумент командной строки
    /// сходятся здесь, чтобы поведение не разъезжалось между точками входа.
    /// </summary>
    public async Task OpenFolderPathAsync(string path)
    {
        // Одна папка — одно окно. Вторая папка либо переводит фокус на уже открытое окно,
        // либо получает своё: multi-root workspace не вводится (ADR-0007 Rule 11).
        if (Workspace is not null && !IsSameFolder(path))
        {
            if (_windowLauncher.TryFocusWindowWithFolder(path))
            {
                return;
            }

            _windowLauncher.OpenFolderInNewWindow(path);
            return;
        }

        var result = await _openFolder.ExecuteAsync(path).ConfigureAwait(true);

        switch (result)
        {
            case OpenFolderResult.Success success:
                await ApplyOpenedFolderAsync(success).ConfigureAwait(true);
                break;

            case OpenFolderResult.NotFound:
                FailFolderOpen("FolderErrorNotFoundTitle", "FolderErrorNotFoundDetails", path);
                break;

            case OpenFolderResult.AccessDenied:
                FailFolderOpen("FolderErrorAccessDeniedTitle", "FolderErrorAccessDeniedDetails", path);
                break;

            case OpenFolderResult.ReadError:
                FailFolderOpen("FolderErrorReadTitle", "FolderErrorReadDetails", path);
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCloseFolder))]
    private async Task CloseFolderAsync()
    {
        CloseOverlayCore();

        var workspace = Workspace;
        if (workspace is null)
        {
            return;
        }

        // Вкладки этой папки уходят вместе с ней; файлы, открытые поверх папки,
        // остаются — окно просто возвращается в обычный single-file режим (ADR-0007 Rule 3).
        if (OpenDocuments.ActiveTab is { BelongsToWorkspace: true } && RequiresDirtyResolution)
        {
            await RunWithDirtyCheckAsync(PendingDirtyActionKind.CloseFile, CloseWorkspaceCoreAsync)
                .ConfigureAwait(true);
            return;
        }

        await CloseWorkspaceCoreAsync().ConfigureAwait(true);
    }

    private async Task CloseWorkspaceCoreAsync()
    {
        foreach (var tab in OpenDocuments.Tabs.Where(static tab => tab.BelongsToWorkspace).ToList())
        {
            await RemoveTabAsync(tab).ConfigureAwait(true);
        }

        CloseWorkspaceCore();

        if (OpenDocuments.Tabs.Count == 0)
        {
            ClearDocumentSurface();
        }
    }

    private void CloseWorkspaceCore()
    {
        StopWatching();
        Workspace = null;
        RefreshWindowTitle();
        UpdateWorkspaceCommandStates();
        RefreshTabState();
    }

    private async Task ApplyOpenedFolderAsync(OpenFolderResult.Success success)
    {
        var workspace = WorkspaceViewModel.FromOpenedFolder(
            success,
            new WorkspaceViewModel.WorkspaceDependencies(
                _expandFolderNode,
                _searchWorkspaceFiles,
                _fileOperations,
                _localization,
                OpenDocumentFromTreeAsync,
                RequestDeleteAsync,
                OnWorkspacePathChanged));

        Workspace = workspace;
        ClearLoadError();
        UpdateWorkspaceCommandStates();

        // Ширина читается здесь, а не в InitializeAsync: старт с одним файлом
        // не должен трогать ничего из workspace-подсистемы.
        await LoadSidebarWidthAsync().ConfigureAwait(true);

        StartWatching(success.Folder.RootPath);

        // Та же папка, что в прошлый раз: возвращаем её вкладки и раскрытые узлы.
        await TryRestoreSessionAsync(workspace).ConfigureAwait(true);
        if (OpenDocuments.HasTabs)
        {
            RefreshWindowTitle();
            return;
        }

        // Папка сама по себе документ не открывает — кроме README.md в корне,
        // который заменяет пустой экран осмысленным содержимым (ADR-0007 Rule 2).
        var readmePath = workspace.TryGetRootReadmePath();
        if (!string.IsNullOrEmpty(readmePath))
        {
            await OpenDocumentFromTreeAsync(readmePath).ConfigureAwait(true);
            return;
        }

        workspace.ActiveDocumentPath = CurrentDocumentPath;
        RefreshWindowTitle();
    }

    private async Task OpenDocumentFromTreeAsync(string path)
    {
        await RunWithDirtyCheckAsync(
                PendingDirtyActionKind.OpenFile,
                () => LoadDocumentAsync(path, preserveEditModeAfterLoad: false))
            .ConfigureAwait(true);
    }

    private void FailFolderOpen(string titleKey, string detailsKey, string path)
    {
        // Ошибка папки не трогает открытый документ: он остаётся читаемым (ADR-0007 Rule 14).
        ErrorTitle = _localization[titleKey];
        ErrorDetails = string.Format(_localization.Culture, _localization[detailsKey], path);
        State = ViewState.LoadError;
    }

    /// <summary>
    /// Ширина сайдбара сохраняется по завершении перетаскивания, а не на каждый пиксель:
    /// иначе один drag превращается в сотню записей в settings.json.
    /// </summary>
    public async Task PersistSidebarWidthAsync()
    {
        var normalized = WorkspaceSidebarWidth.Normalize(SidebarWidth);
        if (Math.Abs(normalized - _persistedSidebarWidth) < 0.5)
        {
            return;
        }

        _persistedSidebarWidth = normalized;
        SidebarWidth = normalized;
        await _settings.SaveSidebarWidthAsync(normalized).ConfigureAwait(true);
    }

    private async Task LoadSidebarWidthAsync()
    {
        var width = await _settings.LoadSidebarWidthAsync().ConfigureAwait(true);
        _persistedSidebarWidth = WorkspaceSidebarWidth.Normalize(width);
        SidebarWidth = _persistedSidebarWidth;
    }

    /// <summary>
    /// Переименование в дереве: открытые вкладки этого файла (и файлов внутри папки)
    /// следуют за новым путём, иначе они указывали бы в пустоту (ADR-0007 Rule 7).
    /// </summary>
    private void OnWorkspacePathChanged(string oldPath, string newPath)
    {
        RetargetTabsUnderPath(oldPath, newPath);
        SyncWorkspaceActiveDocument();
        RefreshWindowTitle();
    }

    private void SyncWorkspaceActiveDocument()
    {
        if (Workspace is { } workspace)
        {
            workspace.ActiveDocumentPath = CurrentDocumentPath;
        }
    }

    private void UpdateWorkspaceCommandStates()
    {
        CloseFolderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanCloseFolder));
    }

    /// <summary>Та же самая папка — просто повторный запрос, окно не плодим.</summary>
    private bool IsSameFolder(string path)
        => Workspace is { } workspace
            && string.Equals(
                Path.TrimEndingDirectorySeparator(workspace.Folder.RootPath),
                Path.TrimEndingDirectorySeparator(path),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsInsideWorkspace(string? documentPath, WorkspaceFolder folder)
    {
        if (string.IsNullOrEmpty(documentPath))
        {
            return false;
        }

        var root = Path.TrimEndingDirectorySeparator(folder.RootPath) + Path.DirectorySeparatorChar;
        return documentPath.StartsWith(
            root,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
