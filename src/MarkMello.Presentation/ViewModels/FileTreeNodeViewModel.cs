using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Строка дерева файлов. Дети каталога заполняются при первом раскрытии:
/// до этого узел держит один placeholder, чтобы <c>TreeView</c> показал шеврон,
/// но ничего не читалось с диска (ADR-0007 Rule 5).
/// </summary>
public sealed partial class FileTreeNodeViewModel : ObservableObject
{
    private readonly Func<FileTreeNodeViewModel, Task> _expandAsync;

    public FileTreeNodeViewModel(
        WorkspaceEntry entry,
        int depth,
        Func<FileTreeNodeViewModel, Task> expandAsync)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(expandAsync);

        Entry = entry;
        Depth = depth;
        _expandAsync = expandAsync;

        if (entry.IsDirectory)
        {
            // Placeholder делает узел раскрываемым до того, как каталог прочитан.
            Children.Add(Placeholder);
        }
    }

    /// <summary>Единственный экземпляр-заглушка: сравнивается по ссылке, на экран не попадает.</summary>
    private static readonly WorkspaceEntry PlaceholderEntry =
        new(string.Empty, string.Empty, IsDirectory: false, IsSupportedDocument: false);

    private FileTreeNodeViewModel? _placeholder;

    private FileTreeNodeViewModel Placeholder =>
        _placeholder ??= new FileTreeNodeViewModel(PlaceholderEntry, Depth + 1, static _ => Task.CompletedTask);

    public WorkspaceEntry Entry { get; }

    public string Path => Entry.Path;

    public string Name => Entry.Name;

    public bool IsDirectory => Entry.IsDirectory;

    /// <summary>Открывается ли узел во вкладке. Не-документы показываются приглушённо и инертны.</summary>
    public bool IsSupportedDocument => Entry.IsSupportedDocument;

    public bool IsInert => !Entry.IsDirectory && !Entry.IsSupportedDocument;

    public int Depth { get; }

    public ObservableCollection<FileTreeNodeViewModel> Children { get; } = [];

    public bool HasLoadedChildren { get; private set; }

    /// <summary>
    /// Чтение каталога уже идёт. Нужен, чтобы раскрытие не запустилось дважды:
    /// один раз из биндинга <c>IsExpanded</c>, второй — из явного вызова.
    /// </summary>
    internal bool IsLoadingChildren { get; set; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isActiveDocument;

    /// <summary>
    /// В открытой вкладке этого файла есть несохранённые правки — строка дерева
    /// показывает такую же точку, как и вкладка (макет 05).
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Локальная ошибка узла: нет прав, каталог исчез. Дерево при этом продолжает работать.</summary>
    [ObservableProperty]
    private string? _loadError;

    public bool HasLoadError => !string.IsNullOrEmpty(LoadError);

    partial void OnLoadErrorChanged(string? value) => OnPropertyChanged(nameof(HasLoadError));

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || !IsDirectory || HasLoadedChildren)
        {
            return;
        }

        // Раскрытие идёт из биндинга TreeViewItem.IsExpanded, поэтому чтение каталога
        // запускается здесь и не блокирует UI-поток.
        _ = _expandAsync(this);
    }

    public void ReplaceChildren(IEnumerable<FileTreeNodeViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        Children.Clear();
        foreach (var child in children)
        {
            Children.Add(child);
        }

        HasLoadedChildren = true;
        LoadError = null;
    }

    public void FailChildrenLoad(string message)
    {
        Children.Clear();
        HasLoadedChildren = true;
        LoadError = message;
    }

    public IEnumerable<FileTreeNodeViewModel> EnumerateLoadedNodes()
    {
        yield return this;

        if (!HasLoadedChildren)
        {
            yield break;
        }

        foreach (var child in Children)
        {
            foreach (var node in child.EnumerateLoadedNodes())
            {
                yield return node;
            }
        }
    }
}
