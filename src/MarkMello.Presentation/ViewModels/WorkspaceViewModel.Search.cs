using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Поиск по именам внутри открытой папки. Каждое нажатие отменяет предыдущий запрос,
/// выдача ограничена лимитами и честно помечается неполной (ADR-0007 Rule 8).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// Порог показа индикатора. Быстрый поиск не должен мигать полоской прогресса,
    /// а долгий — оставлять пользователя в неведении.
    /// </summary>
    private static readonly TimeSpan SearchProgressDelay = TimeSpan.FromMilliseconds(400);

    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchQuery))]
    [NotifyPropertyChangedFor(nameof(ShowsTree))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSearchEmptyState))]
    private bool _isSearchRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchCountLabel))]
    private bool _isSearchTruncated;

    public ObservableCollection<WorkspaceSearchHit> SearchHits { get; } = [];

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    /// <summary>Дерево уступает место списку совпадений, пока в поле есть запрос.</summary>
    public bool ShowsTree => !HasSearchQuery;

    public bool ShowsSearchEmptyState => HasSearchQuery && !IsSearchRunning && SearchHits.Count == 0;

    /// <summary>Шапка «СОВПАДЕНИЯ» появляется только над непустым списком.</summary>
    public bool HasSearchHits => SearchHits.Count > 0;

    /// <summary>«200+» вместо «200»: обрезанная выдача не должна выглядеть полной.</summary>
    public string SearchCountLabel => IsSearchTruncated
        ? SearchHits.Count.ToString(CultureInfo.CurrentCulture) + "+"
        : SearchHits.Count.ToString(CultureInfo.CurrentCulture);

    partial void OnSearchQueryChanged(string value) => _ = RunSearchAsync(value);

    [RelayCommand]
    private void ClearSearch() => SearchQuery = string.Empty;

    /// <summary>
    /// Клик по совпадению: файл открывается во вкладке, каталог сбрасывает поиск
    /// и раскрывается в дереве — искать в нём дальше пользователь будет глазами.
    /// </summary>
    [RelayCommand]
    private async Task ActivateSearchHitAsync(WorkspaceSearchHit? hit)
    {
        if (hit is null)
        {
            return;
        }

        if (hit.Entry.IsDirectory)
        {
            SearchQuery = string.Empty;
            await RevealAsync(hit.Entry.Path).ConfigureAwait(true);
            return;
        }

        await _openDocumentAsync(hit.Entry.Path).ConfigureAwait(true);
    }

    /// <summary>
    /// Раскрывает дерево до указанного пути и выделяет узел. Используется поиском,
    /// а в M3 тем же способом показываются созданные и переименованные файлы.
    /// </summary>
    public async Task RevealAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var current = Roots;

        foreach (var segment in EnumerateSegments(path))
        {
            var node = current.FirstOrDefault(candidate =>
                string.Equals(candidate.Path, segment, StringComparison.OrdinalIgnoreCase));

            if (node is null)
            {
                return;
            }

            if (string.Equals(node.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                SelectedNode = node;
                return;
            }

            await ExpandNodeAsync(node).ConfigureAwait(true);
            current = node.Children;
        }
    }

    /// <summary>Пути от корня папки до целевого элемента, сверху вниз.</summary>
    private List<string> EnumerateSegments(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Folder.RootPath);
        var segments = new List<string>();

        for (var current = Path.TrimEndingDirectorySeparator(path);
             !string.IsNullOrEmpty(current) && !string.Equals(current, root, StringComparison.OrdinalIgnoreCase);
             current = Path.GetDirectoryName(current) ?? string.Empty)
        {
            segments.Add(current);
        }

        segments.Reverse();
        return segments;
    }

    private async Task RunSearchAsync(string query)
    {
        // Только отменяем: освобождает источник тот вызов, который его создал,
        // иначе Task.Delay индикатора обращается к уже уничтоженному токену.
        _searchCancellation?.Cancel();

        if (string.IsNullOrWhiteSpace(query))
        {
            _searchCancellation = null;
            IsSearchRunning = false;
            IsSearchTruncated = false;
            ReplaceHits([]);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;

        var progress = ShowProgressAfterDelayAsync(cancellation.Token);

        try
        {
            var result = await _searchWorkspaceFiles
                .ExecuteAsync(Folder.RootPath, query, cancellation.Token)
                .ConfigureAwait(true);

            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            IsSearchTruncated = result.IsTruncated;
            ReplaceHits(result.Hits);
        }
        catch (OperationCanceledException)
        {
            // Запрос устарел — результат следующего уже в пути.
        }
        finally
        {
            // Индикатор ждёт своего порога на том же токене. Без отмены он зажигается
            // уже после того, как результат показан, и больше не гаснет — а вместе с ним
            // не показывается и пустое состояние.
            cancellation.Cancel();
            await progress.ConfigureAwait(true);

            if (ReferenceEquals(_searchCancellation, cancellation))
            {
                IsSearchRunning = false;
                _searchCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task ShowProgressAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchProgressDelay, cancellationToken).ConfigureAwait(true);
            IsSearchRunning = true;
        }
        catch (OperationCanceledException)
        {
            // Поиск закончился раньше порога — индикатор так и не появился.
        }
    }

    private void ReplaceHits(IReadOnlyList<WorkspaceSearchHit> hits)
    {
        SearchHits.Clear();
        foreach (var hit in hits)
        {
            SearchHits.Add(hit);
        }

        OnPropertyChanged(nameof(SearchCountLabel));
        OnPropertyChanged(nameof(ShowsSearchEmptyState));
        OnPropertyChanged(nameof(HasSearchHits));
    }
}
