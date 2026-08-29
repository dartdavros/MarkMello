using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.UseCases;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>Что именно вводит пользователь в строке дерева.</summary>
public enum TreeEditKind
{
    None,
    NewFile,
    NewFolder,
    Rename
}

/// <summary>
/// Файловые операции дерева. Ввод имени — инлайн в строке дерева, а не диалог:
/// пользователь видит, куда попадёт элемент после сортировки (макет 09).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>Куда встанет новый элемент и что переименовывается.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingName))]
    private TreeEditKind _editKind = TreeEditKind.None;

    [ObservableProperty]
    private string _editName = string.Empty;

    /// <summary>Ошибка под полем ввода: занятое имя или запрещённые символы.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditError))]
    private string? _editError;

    [ObservableProperty]
    private FileTreeNodeViewModel? _editingNode;

    /// <summary>Каталог, в котором создаётся элемент.</summary>
    private string _editDirectory = string.Empty;

    public bool IsEditingName => EditKind != TreeEditKind.None;

    public bool HasEditError => !string.IsNullOrEmpty(EditError);

    /// <summary>Ошибка последней операции — показывается той же карточкой, что и подтверждение.</summary>
    [ObservableProperty]
    private string? _operationError;

    [RelayCommand]
    private void StartNewFile() => StartCreate(TreeEditKind.NewFile);

    [RelayCommand]
    private void StartNewFolder() => StartCreate(TreeEditKind.NewFolder);

    /// <summary>
    /// Куда вставляется новый элемент: в выделенную папку, рядом с выделенным файлом,
    /// иначе в корень (макет 09).
    /// </summary>
    private void StartCreate(TreeEditKind kind)
    {
        var directory = SelectedNode switch
        {
            { IsDirectory: true } folder => folder.Path,
            { } file => Path.GetDirectoryName(file.Path) ?? Folder.RootPath,
            _ => Folder.RootPath
        };

        _editDirectory = directory;
        EditingNode = null;
        EditName = kind == TreeEditKind.NewFile ? ".md" : string.Empty;
        EditError = null;
        EditKind = kind;
    }

    [RelayCommand]
    private void StartRename(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null)
        {
            return;
        }

        _editDirectory = Path.GetDirectoryName(target.Path) ?? Folder.RootPath;
        EditingNode = target;
        EditName = target.Name;
        EditError = null;
        EditKind = TreeEditKind.Rename;
    }

    /// <summary>Esc и потеря фокуса — отмена; ничего не создаётся и не переименовывается.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        EditKind = TreeEditKind.None;
        EditingNode = null;
        EditName = string.Empty;
        EditError = null;
    }

    [RelayCommand]
    private async Task CommitEditAsync()
    {
        if (EditKind == TreeEditKind.None)
        {
            return;
        }

        // Пустое имя просто ничего не делает: ошибку показывать не за что.
        if (string.IsNullOrWhiteSpace(EditName) || EditName == ".md")
        {
            return;
        }

        var kind = EditKind;
        var node = EditingNode;

        var result = kind switch
        {
            TreeEditKind.NewFile => await _fileOperations.CreateFileAsync(_editDirectory, EditName).ConfigureAwait(true),
            TreeEditKind.NewFolder => await _fileOperations.CreateDirectoryAsync(_editDirectory, EditName).ConfigureAwait(true),
            _ when node is not null => await _fileOperations.RenameAsync(node.Path, EditName).ConfigureAwait(true),
            _ => null
        };

        if (result is null)
        {
            CancelEdit();
            return;
        }

        if (result is WorkspaceMutationResult.Success success)
        {
            var previousPath = node?.Path;
            CancelEdit();
            await RefreshDirectoryAsync(_editDirectory).ConfigureAwait(true);

            if (kind == TreeEditKind.Rename && previousPath is not null)
            {
                // Вкладку переводим до показа узла: выделение открывает документ,
                // и без этого на тот же файл завелась бы вторая вкладка.
                _pathChanged(previousPath, success.Entry.Path);
            }

            await RevealAsync(success.Entry.Path).ConfigureAwait(true);

            if (kind == TreeEditKind.NewFile)
            {
                // Созданный файл сразу открывается вкладкой — иначе создание выглядит как «ничего не произошло».
                await _openDocumentAsync(success.Entry.Path).ConfigureAwait(true);
            }

            return;
        }

        // Строка ввода остаётся открытой: пользователь правит имя, а не начинает заново.
        EditError = DescribeFailure(result);
    }

    [RelayCommand]
    private async Task DuplicateAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null)
        {
            return;
        }

        var result = await _fileOperations.DuplicateAsync(target.Path).ConfigureAwait(true);
        var directory = Path.GetDirectoryName(target.Path) ?? Folder.RootPath;

        if (result is WorkspaceMutationResult.Success success)
        {
            await RefreshDirectoryAsync(directory).ConfigureAwait(true);

            // Копия выделяется в дереве, но вкладку не открывает.
            await RevealAsync(success.Entry.Path).ConfigureAwait(true);
            return;
        }

        OperationError = DescribeFailure(result);
    }

    /// <summary>«Открыть в новой вкладке» из контекстного меню: то же, что клик по строке.</summary>
    [RelayCommand]
    private async Task OpenNodeAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null || target.IsDirectory || !target.IsSupportedDocument)
        {
            return;
        }

        await _openDocumentAsync(target.Path).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task RevealAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        return target is null ? Task.CompletedTask : _fileOperations.RevealAsync(target.Path).AsTask();
    }

    [RelayCommand]
    private Task RequestDeleteAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        return target is null ? Task.CompletedTask : _deleteRequested(target);
    }

    /// <summary>Вызывается shell после подтверждения: сама операция и обновление дерева.</summary>
    public async Task<WorkspaceMutationResult> DeleteConfirmedAsync(FileTreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var directory = Path.GetDirectoryName(node.Path) ?? Folder.RootPath;
        var result = await _fileOperations.DeleteAsync(node.Path).ConfigureAwait(true);

        if (result is WorkspaceMutationResult.Deleted)
        {
            await RefreshDirectoryAsync(directory).ConfigureAwait(true);
        }
        else
        {
            OperationError = DescribeFailure(result);
        }

        return result;
    }

    /// <summary>Безвозвратное удаление — только после отдельного подтверждения в shell.</summary>
    public async Task<WorkspaceMutationResult> DeletePermanentlyConfirmedAsync(FileTreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var directory = Path.GetDirectoryName(node.Path) ?? Folder.RootPath;
        var result = await _fileOperations.DeletePermanentlyAsync(node.Path).ConfigureAwait(true);

        if (result is WorkspaceMutationResult.Deleted)
        {
            await RefreshDirectoryAsync(directory).ConfigureAwait(true);
        }
        else
        {
            OperationError = DescribeFailure(result);
        }

        return result;
    }

    /// <summary>Перечитывает один каталог после операции — обход остального дерева не нужен.</summary>
    public async Task RefreshDirectoryAsync(string directoryPath)
    {
        if (PathsEqual(directoryPath, Folder.RootPath))
        {
            var result = await _expandFolderNode.ExecuteAsync(Folder.RootPath).ConfigureAwait(true);
            if (result is ExpandFolderNodeResult.Success success)
            {
                ReplaceRoots(success.Children);
            }

            return;
        }

        var node = FindLoadedNode(directoryPath);
        if (node is null)
        {
            return;
        }

        var children = await _expandFolderNode.ExecuteAsync(node.Path).ConfigureAwait(true);
        if (children is ExpandFolderNodeResult.Success loaded)
        {
            node.ReplaceChildren(CreateNodes(loaded.Children, node.Depth + 1));
            ApplyActiveDocumentHighlight(node);
        }
    }

    private void ReplaceRoots(IReadOnlyList<WorkspaceEntry> entries)
    {
        Roots.Clear();
        foreach (var node in CreateNodes(entries, depth: 0))
        {
            Roots.Add(node);
        }

        if (!string.IsNullOrEmpty(ActiveDocumentPath))
        {
            foreach (var node in Roots)
            {
                node.IsActiveDocument = !node.IsDirectory && PathsEqual(node.Path, ActiveDocumentPath);
            }
        }
    }

    private FileTreeNodeViewModel? FindLoadedNode(string path)
        => Roots
            .SelectMany(static root => root.EnumerateLoadedNodes())
            .FirstOrDefault(node => PathsEqual(node.Path, path));

    private string DescribeFailure(WorkspaceMutationResult result)
        => result switch
        {
            WorkspaceMutationResult.NameTaken { IsDirectory: true } => _localization["TreeFolderNameTaken"],
            WorkspaceMutationResult.NameTaken => _localization["TreeNameTaken"],
            WorkspaceMutationResult.InvalidName { Problem: WorkspaceNameProblem.Reserved } =>
                _localization["TreeReservedName"],
            WorkspaceMutationResult.InvalidName => _localization["TreeInvalidChars"],
            WorkspaceMutationResult.NotFound => _localization["TreeNodeMissing"],
            WorkspaceMutationResult.AccessDenied => _localization["TreeNodeAccessDenied"],
            _ => _localization["TreeOperationFailed"]
        };
}
