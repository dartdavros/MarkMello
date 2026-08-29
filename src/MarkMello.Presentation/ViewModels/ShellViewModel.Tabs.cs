using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Вкладки открытых документов. Активная вкладка — это то, что видно в document surface:
/// её содержимое живёт в полях shell, а остальные вкладки держат снимок своего документа
/// и позицию прокрутки, чтобы возврат не перечитывал файл (ADR-0007 Rule 4).
/// </summary>
public partial class ShellViewModel
{
    private bool _isRestoringTab;
    private double? _pendingScrollOffset;

    public OpenDocumentsViewModel OpenDocuments { get; private set; } = default!;

    /// <summary>Полосы вкладок нет, пока нет ни одного открытого документа.</summary>
    public bool ShowsTabStrip => OpenDocuments.HasTabs;

    /// <summary>
    /// Папка открыта, но документ не выбран. Отдельно от welcome: там предлагают открыть файл,
    /// здесь — выбрать его в дереве слева (макет 07).
    /// </summary>
    public bool IsEmptyDocumentSurface => State == ViewState.NoDocument && ShowsSidebar;

    private void InitializeOpenDocuments()
        => OpenDocuments = new OpenDocumentsViewModel(ActivateTabAsync, CloseTabAsync);

    /// <summary>
    /// Позиция прокрутки приходит из вьюера на каждое изменение: держать её в вкладке
    /// дешевле, чем спрашивать вьюер в момент переключения — его может уже не быть.
    /// </summary>
    public void ReportScrollOffset(double offset)
    {
        if (_isRestoringTab || OpenDocuments.ActiveTab is not { } tab)
        {
            return;
        }

        tab.ScrollOffset = offset;
    }

    /// <summary>Возвращает отложенную позицию ровно один раз — после того, как документ отрисован.</summary>
    public double? TakePendingScrollOffset()
    {
        var offset = _pendingScrollOffset;
        _pendingScrollOffset = null;
        return offset;
    }

    [RelayCommand]
    private Task ActivateNextTabAsync() => ActivateNeighbourAsync(1);

    [RelayCommand]
    private Task ActivatePreviousTabAsync() => ActivateNeighbourAsync(-1);

    [RelayCommand(CanExecute = nameof(CanCloseActiveTab))]
    private Task CloseActiveTabAsync()
        => OpenDocuments.ActiveTab is { } tab ? CloseTabAsync(tab) : Task.CompletedTask;

    private bool CanCloseActiveTab() => OpenDocuments.ActiveTab is not null;

    private Task ActivateNeighbourAsync(int direction)
    {
        var neighbour = OpenDocuments.GetNeighbour(direction);
        return neighbour is null || ReferenceEquals(neighbour, OpenDocuments.ActiveTab)
            ? Task.CompletedTask
            : ActivateTabAsync(neighbour);
    }

    /// <summary>
    /// Переключение на другую вкладку. Документ не перечитывается с диска — берётся снимок,
    /// поэтому переключение стоит столько же, сколько перерисовка.
    /// </summary>
    private async Task ActivateTabAsync(DocumentTabViewModel tab)
    {
        if (ReferenceEquals(OpenDocuments.ActiveTab, tab))
        {
            return;
        }

        if (RequiresDirtyResolution)
        {
            // Уходить с грязной вкладки без разрешения нельзя: тот же диалог, что и при открытии файла.
            await RunWithDirtyCheckAsync(PendingDirtyActionKind.OpenFile, () => RestoreTabAsync(tab))
                .ConfigureAwait(true);
            return;
        }

        await RestoreTabAsync(tab).ConfigureAwait(true);
    }

    private Task RestoreTabAsync(DocumentTabViewModel tab)
    {
        _isRestoringTab = true;
        try
        {
            OpenDocuments.Activate(tab);

            IsEditMode = false;
            EditorSession = null;
            Document = tab.Document;
            RenderedDocument = tab.RenderedDocument;
            _currentPath = tab.Path;
            State = tab.Document is null && tab.Path is null && tab.RenderedDocument.Blocks.Count == 0
                ? ViewState.Viewing
                : ViewState.Viewing;
            ClearLoadError();

            _pendingScrollOffset = tab.ScrollOffset;
            ReadingProgress = 0;

            SyncWorkspaceActiveDocument();
            RefreshWindowTitle();
            UpdateCommandStates();
            UpdateTabCommandStates();
        }
        finally
        {
            _isRestoringTab = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Закрытие вкладки. Грязная вкладка проходит через существующий диалог
    /// «Сохранить / Не сохранять / Отмена», причём «Отмена» оставляет вкладку на месте.
    /// </summary>
    private async Task CloseTabAsync(DocumentTabViewModel tab)
    {
        var isActive = ReferenceEquals(OpenDocuments.ActiveTab, tab);

        if (isActive && RequiresDirtyResolution)
        {
            await RunWithDirtyCheckAsync(PendingDirtyActionKind.CloseFile, () => RemoveTabAsync(tab))
                .ConfigureAwait(true);
            return;
        }

        await RemoveTabAsync(tab).ConfigureAwait(true);
    }

    private async Task RemoveTabAsync(DocumentTabViewModel tab)
    {
        var wasActive = ReferenceEquals(OpenDocuments.ActiveTab, tab);
        OpenDocuments.Remove(tab);

        if (!wasActive)
        {
            RefreshTabState();
            return;
        }

        if (OpenDocuments.ActiveTab is { } next)
        {
            // Remove уже перевёл активность на соседа — восстанавливаем его содержимое.
            OpenDocuments.Activate(null);
            await RestoreTabAsync(next).ConfigureAwait(true);
            RefreshTabState();
            return;
        }

        ClearDocumentSurface();
        RefreshTabState();
    }

    /// <summary>Последняя вкладка закрыта: в folder mode остаётся пустое состояние, иначе welcome.</summary>
    private void ClearDocumentSurface()
    {
        IsEditMode = false;
        EditorSession = null;
        Document = null;
        RenderedDocument = RenderedMarkdownDocument.Empty;
        _currentPath = null;
        State = ViewState.NoDocument;
        ReadingProgress = 0;
        ClearLoadError();
        SyncWorkspaceActiveDocument();
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    /// <summary>Заводит вкладку под загруженный документ или обновляет уже открытую.</summary>
    private void TrackLoadedDocumentTab(MarkdownSource source)
    {
        var tab = OpenDocuments.FindByPath(source.Path);
        if (tab is null)
        {
            tab = OpenDocuments.Add(new DocumentTabViewModel(source.Path, source.FileName));
        }

        tab.ApplyDocument(source, RenderedDocument);
        tab.Tooltip = BuildTabTooltip(source.Path);
        tab.BelongsToWorkspace = Workspace is { } workspace && IsInsideWorkspace(source.Path, workspace.Folder);
        tab.IsDirty = false;

        OpenDocuments.Activate(tab);
        OpenDocuments.Refresh();
        RefreshTabState();
    }

    /// <summary>Новый несохранённый документ тоже занимает вкладку — просто без пути.</summary>
    private void TrackNewDocumentTab()
    {
        var tab = OpenDocuments.Add(new DocumentTabViewModel(null, _localization["UntitledFileName"]));
        tab.ApplyDocument(null, RenderedMarkdownDocument.Empty);
        tab.Tooltip = _localization["UntitledFileName"];

        OpenDocuments.Activate(tab);
        OpenDocuments.Refresh();
        RefreshTabState();
    }

    /// <summary>«Сохранить как» уводит вкладку на новый путь, вместе с заголовком и тултипом.</summary>
    private void RetargetActiveTab(MarkdownSource source)
    {
        if (OpenDocuments.ActiveTab is not { } tab)
        {
            return;
        }

        tab.Retarget(source.Path, source.FileName);
        tab.ApplyDocument(source, RenderedDocument);
        tab.Tooltip = BuildTabTooltip(source.Path);
        tab.BelongsToWorkspace = Workspace is { } workspace && IsInsideWorkspace(source.Path, workspace.Folder);
        OpenDocuments.Refresh();
        RefreshTabState();
    }

    private void SyncActiveTabDirtyState()
    {
        if (OpenDocuments.ActiveTab is { } tab)
        {
            tab.IsDirty = IsDirty;
        }
    }

    private void RefreshTabState()
    {
        OnPropertyChanged(nameof(ShowsTabStrip));
        OnPropertyChanged(nameof(IsEmptyDocumentSurface));
        UpdateTabCommandStates();
    }

    private void UpdateTabCommandStates()
    {
        CloseActiveTabCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Тултип — путь относительно корня папки, для файлов вне её — абсолютный.</summary>
    private string BuildTabTooltip(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        if (Workspace is not { } workspace || !IsInsideWorkspace(path, workspace.Folder))
        {
            return path;
        }

        var root = Path.TrimEndingDirectorySeparator(workspace.Folder.RootPath) + Path.DirectorySeparatorChar;
        return path[root.Length..];
    }
}
