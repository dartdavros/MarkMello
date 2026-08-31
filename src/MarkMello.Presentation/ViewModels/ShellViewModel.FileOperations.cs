using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.UseCases;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Подтверждение удаления и реакция вкладок на файловые операции.
/// Диалог живёт на уровне окна: он модальный и перекрывает и дерево, и документ.
/// </summary>
public partial class ShellViewModel
{
    private FileTreeNodeViewModel? _deleteTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeletePromptContent))]
    private bool _isDeletePromptOpen;

    /// <summary>Карточка подтверждения строится в момент вопроса, а не живёт скрытой.</summary>
    public object? DeletePromptContent => IsDeletePromptOpen ? this : null;

    [ObservableProperty]
    private string _deletePromptTitle = string.Empty;

    [ObservableProperty]
    private string _deletePromptMessage = string.Empty;

    /// <summary>
    /// Корзина недоступна: тот же диалог переспрашивает уже про безвозвратное удаление.
    /// Пользователь должен подтвердить именно потерю, а не «удаление» вообще.
    /// </summary>
    [ObservableProperty]
    private bool _isPermanentDeletePrompt;

    /// <summary>Ошибка операции показывается той же карточкой с одной кнопкой «Закрыть».</summary>
    [ObservableProperty]
    private bool _isDeleteErrorPrompt;

    public string DeleteConfirmLabel => _localization["DeleteConfirm"];

    public string DeleteCancelLabel => _localization["DeleteCancel"];

    public string DeleteErrorCloseLabel => _localization["FileOpErrorClose"];

    /// <summary>Готовит тексты подтверждения: у файла, пустой и непустой папки они разные.</summary>
    private async Task RequestDeleteAsync(FileTreeNodeViewModel node)
    {
        _deleteTarget = node;
        IsPermanentDeletePrompt = false;
        IsDeleteErrorPrompt = false;

        if (!node.IsDirectory)
        {
            DeletePromptTitle = Format("DeleteFileTitle", node.Name);
            DeletePromptMessage = _localization["DeleteFileBody"];
        }
        else
        {
            // Считаем только верхний уровень: рекурсивный обход ради текста запрещён (Rule 5).
            var count = await _fileOperations.CountChildrenAsync(node.Path).ConfigureAwait(true);

            if (count == 0)
            {
                DeletePromptTitle = Format("DeleteFolderTitle", node.Name);
                DeletePromptMessage = _localization["DeleteFolderBody"];
            }
            else
            {
                DeletePromptTitle = Format("DeleteFolderNonEmptyTitle", node.Name);
                DeletePromptMessage = Format("DeleteFolderNonEmptyBody", node.Name, count);
            }
        }

        IsDeletePromptOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (_deleteTarget is not { } node || Workspace is not { } workspace)
        {
            CancelDelete();
            return;
        }

        if (IsDeleteErrorPrompt)
        {
            CancelDelete();
            return;
        }

        var result = IsPermanentDeletePrompt
            ? await workspace.DeletePermanentlyConfirmedAsync(node).ConfigureAwait(true)
            : await workspace.DeleteConfirmedAsync(node).ConfigureAwait(true);

        switch (result)
        {
            case WorkspaceMutationResult.Deleted:
                await CloseTabsUnderPathAsync(node.Path).ConfigureAwait(true);
                CancelDelete();
                break;

            case WorkspaceMutationResult.TrashUnavailable:
                // Ничего не удалено: переспрашиваем уже про безвозвратное удаление.
                IsPermanentDeletePrompt = true;
                DeletePromptMessage = _localization["DeletePermanentBody"];
                break;

            default:
                IsDeleteErrorPrompt = true;
                DeletePromptTitle = Format("FileOpErrorTitle", node.Name);
                DeletePromptMessage = workspace.OperationError ?? _localization["TreeOperationFailed"];
                break;
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsDeletePromptOpen = false;
        IsPermanentDeletePrompt = false;
        IsDeleteErrorPrompt = false;
        _deleteTarget = null;
    }

    /// <summary>Переименование: вкладки этого файла и файлов внутри папки следуют за новым путём.</summary>
    private void RetargetTabsUnderPath(string oldPath, string newPath)
    {
        foreach (var tab in OpenDocuments.Tabs.ToList())
        {
            if (tab.Path is not { } path)
            {
                continue;
            }

            if (PathsMatch(path, oldPath))
            {
                tab.Retarget(newPath, Path.GetFileName(newPath));
                tab.Tooltip = BuildTabTooltip(newPath);
            }
            else if (IsUnderDirectory(path, oldPath))
            {
                var moved = newPath + path[oldPath.Length..];
                tab.Retarget(moved, Path.GetFileName(moved));
                tab.Tooltip = BuildTabTooltip(moved);
            }
        }

        if (PathsMatch(CurrentDocumentPath, oldPath))
        {
            _currentPath = newPath;
            RenameOpenDocument(newPath);
        }

        OpenDocuments.Refresh();
    }

    /// <summary>
    /// Имя открытого документа живёт в загруженной модели и в сессии редактора,
    /// поэтому после переименования его надо обновить в обеих — иначе заголовок окна
    /// продолжает показывать старое имя.
    /// </summary>
    private void RenameOpenDocument(string newPath)
    {
        var fileName = Path.GetFileName(newPath);

        if (Document is { } document)
        {
            Document = document with { Path = newPath, FileName = fileName };
        }

        EditorSession?.Rename(newPath, fileName);
        RefreshDocumentSummary();
    }

    /// <summary>Удаление: открытые вкладки удалённого файла или папки закрываются.</summary>
    private async Task CloseTabsUnderPathAsync(string path)
    {
        foreach (var tab in OpenDocuments.Tabs.ToList())
        {
            if (tab.Path is { } tabPath && (PathsMatch(tabPath, path) || IsUnderDirectory(tabPath, path)))
            {
                await RemoveTabAsync(tab).ConfigureAwait(true);
            }
        }
    }

    private string Format(string key, params object?[] arguments)
        => string.Format(_localization.Culture, _localization[key], arguments);

    private static bool PathsMatch(string? left, string? right)
        => left is not null
            && right is not null
            && string.Equals(left, right, PathComparison);

    private static bool IsUnderDirectory(string path, string directory)
        => path.StartsWith(
            Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar,
            PathComparison);

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
