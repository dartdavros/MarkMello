using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Application.UseCases;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Открытая папка: корень, дерево и выбор файла. Создаётся только по команде
/// «Открыть папку» — при старте с одним файлом ничего из этого не инстанцируется
/// (ADR-0007 Rule 1). Поиск и файловые операции появятся в M3.
/// </summary>
public sealed partial class WorkspaceViewModel : ObservableObject
{
    private readonly ExpandFolderNodeUseCase _expandFolderNode;
    private readonly ILocalizationService _localization;
    private readonly Func<string, Task> _openDocumentAsync;

    private WorkspaceViewModel(
        WorkspaceFolder folder,
        IReadOnlyList<WorkspaceEntry> rootEntries,
        ExpandFolderNodeUseCase expandFolderNode,
        ILocalizationService localization,
        Func<string, Task> openDocumentAsync)
    {
        Folder = folder;
        _expandFolderNode = expandFolderNode;
        _localization = localization;
        _openDocumentAsync = openDocumentAsync;

        foreach (var node in CreateNodes(rootEntries, depth: 0))
        {
            Roots.Add(node);
        }
    }

    /// <summary>
    /// Собирает workspace из уже прочитанного корневого уровня. Чтение делает use case,
    /// view-model только раскладывает результат — так дерево остаётся тестируемым без диска.
    /// </summary>
    public static WorkspaceViewModel FromOpenedFolder(
        OpenFolderResult.Success result,
        ExpandFolderNodeUseCase expandFolderNode,
        ILocalizationService localization,
        Func<string, Task> openDocumentAsync)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(expandFolderNode);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(openDocumentAsync);

        return new WorkspaceViewModel(
            result.Folder,
            result.Children,
            expandFolderNode,
            localization,
            openDocumentAsync);
    }

    public WorkspaceFolder Folder { get; }

    public string RootDisplayName => Folder.DisplayName;

    public ObservableCollection<FileTreeNodeViewModel> Roots { get; } = [];

    [ObservableProperty]
    private FileTreeNodeViewModel? _selectedNode;

    /// <summary>Путь документа, открытого в окне. Подсвечивает строку дерева акцентной планкой.</summary>
    [ObservableProperty]
    private string? _activeDocumentPath;

    /// <summary>
    /// Выбор строки открывает документ. Каталоги и не-документы инертны: строка выделяется,
    /// но документ не открывается и сообщение не показывается (ADR-0007 Rule 6).
    /// Раскрытие каталога делает сам <c>TreeView</c> по шеврону.
    /// </summary>
    partial void OnSelectedNodeChanged(FileTreeNodeViewModel? value)
    {
        if (value is null || value.IsDirectory || !value.IsSupportedDocument)
        {
            return;
        }

        if (!string.IsNullOrEmpty(ActiveDocumentPath) && PathsEqual(value.Path, ActiveDocumentPath))
        {
            return;
        }

        _ = OpenSelectedDocumentAsync(value.Path);
    }

    private async Task OpenSelectedDocumentAsync(string path)
    {
        try
        {
            await _openDocumentAsync(path).ConfigureAwait(true);
        }
        catch
        {
            // Ошибки открытия документа уже показываются через состояние окна;
            // fire-and-forget из сеттера не должен уносить приложение.
        }
    }

    /// <summary>Первый `README.md` в корне: с него открывается папка, если он есть.</summary>
    public string? TryGetRootReadmePath()
        => Roots
            .FirstOrDefault(node =>
                !node.IsDirectory
                && node.IsSupportedDocument
                && string.Equals(node.Name, "README.md", StringComparison.OrdinalIgnoreCase))
            ?.Path;

    partial void OnActiveDocumentPathChanged(string? value)
    {
        foreach (var node in EnumerateLoadedNodes())
        {
            node.IsActiveDocument = !node.IsDirectory
                && !string.IsNullOrEmpty(value)
                && PathsEqual(node.Path, value);
        }
    }

    /// <summary>
    /// Читает детей узла. Публичен, потому что раскрытие бывает нужно дождаться:
    /// из биндинга <c>IsExpanded</c> вызов идёт fire-and-forget, а тестам
    /// и диагностическому замеру нужен именно завершённый Task.
    /// </summary>
    public async Task ExpandNodeAsync(FileTreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.HasLoadedChildren || node.IsLoadingChildren)
        {
            return;
        }

        node.IsLoadingChildren = true;
        try
        {
            // Присваивание поднимет IsExpanded в UI; вложенный вызов из сеттера
            // упрётся в IsLoadingChildren и каталог не будет прочитан дважды.
            node.IsExpanded = true;

            var result = await _expandFolderNode.ExecuteAsync(node.Path).ConfigureAwait(true);

            switch (result)
            {
                case ExpandFolderNodeResult.Success success:
                    node.ReplaceChildren(CreateNodes(success.Children, node.Depth + 1));
                    ApplyActiveDocumentHighlight(node);
                    break;

                case ExpandFolderNodeResult.NotFound:
                    node.FailChildrenLoad(_localization["TreeNodeMissing"]);
                    break;

                case ExpandFolderNodeResult.AccessDenied:
                    node.FailChildrenLoad(_localization["TreeNodeAccessDenied"]);
                    break;

                case ExpandFolderNodeResult.ReadError:
                    node.FailChildrenLoad(_localization["TreeNodeReadError"]);
                    break;
            }
        }
        finally
        {
            node.IsLoadingChildren = false;
        }
    }

    private IEnumerable<FileTreeNodeViewModel> CreateNodes(IReadOnlyList<WorkspaceEntry> entries, int depth)
        => entries.Select(entry => new FileTreeNodeViewModel(entry, depth, ExpandNodeAsync));

    private void ApplyActiveDocumentHighlight(FileTreeNodeViewModel parent)
    {
        if (string.IsNullOrEmpty(ActiveDocumentPath))
        {
            return;
        }

        foreach (var node in parent.Children)
        {
            node.IsActiveDocument = !node.IsDirectory && PathsEqual(node.Path, ActiveDocumentPath);
        }
    }

    private IEnumerable<FileTreeNodeViewModel> EnumerateLoadedNodes()
        => Roots.SelectMany(static root => root.EnumerateLoadedNodes());

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
