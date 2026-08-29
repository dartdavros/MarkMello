using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Список открытых документов. Держит порядок вкладок, активную вкладку и разделение
/// на видимые и ушедшие в меню «ещё N». Сам ничего не открывает и не закрывает:
/// решения про грязный документ принимает shell, сюда приходят уже разрешённые действия.
/// </summary>
public sealed partial class OpenDocumentsViewModel : ObservableObject
{
    /// <summary>Место под кнопку «ещё N» — она обязана поместиться рядом с видимыми вкладками.</summary>
    private const double OverflowButtonWidth = 60;

    private readonly Func<DocumentTabViewModel, Task> _activateRequested;
    private readonly Func<DocumentTabViewModel, Task> _closeRequested;

    public OpenDocumentsViewModel(
        Func<DocumentTabViewModel, Task> activateRequested,
        Func<DocumentTabViewModel, Task> closeRequested)
    {
        ArgumentNullException.ThrowIfNull(activateRequested);
        ArgumentNullException.ThrowIfNull(closeRequested);

        _activateRequested = activateRequested;
        _closeRequested = closeRequested;
        Tabs.CollectionChanged += (_, _) => OnTabsChanged();
    }

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];

    public ObservableCollection<DocumentTabViewModel> VisibleTabs { get; } = [];

    public ObservableCollection<DocumentTabViewModel> OverflowTabs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTabs))]
    private DocumentTabViewModel? _activeTab;

    /// <summary>Без вкладок полосы нет вообще — она не должна становиться постоянным chrome.</summary>
    public bool HasTabs => Tabs.Count > 0;

    public bool HasOverflow => OverflowTabs.Count > 0;

    public string OverflowCountLabel => OverflowTabs.Count.ToString(CultureInfo.CurrentCulture);

    /// <summary>Доступная ширина полосы. Проставляется вью при изменении размера окна.</summary>
    [ObservableProperty]
    private double _availableWidth;

    public DocumentTabViewModel? FindByPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return Tabs.FirstOrDefault(tab => PathsEqual(tab.Path, path));
    }

    public DocumentTabViewModel Add(DocumentTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        Tabs.Add(tab);
        return tab;
    }

    public void Remove(DocumentTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        var index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        Tabs.RemoveAt(index);

        if (!ReferenceEquals(ActiveTab, tab))
        {
            return;
        }

        // После закрытия активной вкладки фокус уходит на соседнюю справа,
        // а если её нет — на последнюю: так порядок чтения не прыгает в начало.
        ActiveTab = Tabs.Count == 0
            ? null
            : Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    public void Activate(DocumentTabViewModel? tab)
    {
        if (tab is not null && !Tabs.Contains(tab))
        {
            return;
        }

        ActiveTab = tab;
    }

    /// <summary>`Ctrl Tab` — по позиции в полосе с закольцовкой, а не по истории обращений.</summary>
    public DocumentTabViewModel? GetNeighbour(int direction)
    {
        if (Tabs.Count == 0)
        {
            return null;
        }

        if (ActiveTab is null)
        {
            return Tabs[0];
        }

        var index = Tabs.IndexOf(ActiveTab);
        if (index < 0)
        {
            return Tabs[0];
        }

        var next = (index + direction) % Tabs.Count;
        if (next < 0)
        {
            next += Tabs.Count;
        }

        return Tabs[next];
    }

    [RelayCommand]
    private Task ActivateAsync(DocumentTabViewModel? tab)
        => tab is null ? Task.CompletedTask : _activateRequested(tab);

    [RelayCommand]
    private Task CloseAsync(DocumentTabViewModel? tab)
        => tab is null ? Task.CompletedTask : _closeRequested(tab);

    [RelayCommand]
    private async Task CloseOthersAsync(DocumentTabViewModel? tab)
    {
        var keep = tab ?? ActiveTab;
        if (keep is null)
        {
            return;
        }

        foreach (var other in Tabs.Where(candidate => !ReferenceEquals(candidate, keep)).ToList())
        {
            await _closeRequested(other).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Пересчитывает подписи и разделение на видимые/переполнение. Вызывается
    /// при любом изменении состава, активной вкладки или ширины полосы.
    /// </summary>
    public void Refresh()
    {
        RefreshDisambiguators();
        RefreshVisibility();
    }

    private void OnTabsChanged()
    {
        OnPropertyChanged(nameof(HasTabs));
        Refresh();
    }

    partial void OnActiveTabChanged(DocumentTabViewModel? oldValue, DocumentTabViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
        }

        RefreshVisibility();
    }

    partial void OnAvailableWidthChanged(double value) => RefreshVisibility();

    /// <summary>
    /// Одинаковые имена различаются именем родительской папки: «README.md docs».
    /// Подпись появляется только у конфликтующих вкладок и снимается, когда конфликт ушёл.
    /// </summary>
    private void RefreshDisambiguators()
    {
        var duplicates = Tabs
            .GroupBy(tab => tab.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToHashSet();

        foreach (var tab in Tabs)
        {
            tab.Disambiguator = duplicates.Contains(tab)
                ? TryGetParentFolderName(tab.Path)
                : null;
        }
    }

    private void RefreshVisibility()
    {
        var visible = CalculateVisibleTabs();

        SyncCollection(VisibleTabs, visible);
        SyncCollection(OverflowTabs, Tabs.Where(tab => !visible.Contains(tab)).ToList());

        OnPropertyChanged(nameof(HasOverflow));
        OnPropertyChanged(nameof(OverflowCountLabel));
    }

    private List<DocumentTabViewModel> CalculateVisibleTabs()
    {
        if (Tabs.Count == 0)
        {
            return [];
        }

        var total = Tabs.Sum(static tab => tab.EstimateWidth());
        if (AvailableWidth <= 0 || total <= AvailableWidth)
        {
            return [.. Tabs];
        }

        // Вкладки не сжимаются: лишние уходят с конца в меню «ещё N».
        var budget = AvailableWidth - OverflowButtonWidth;
        var visible = new List<DocumentTabViewModel>();

        foreach (var tab in Tabs)
        {
            var width = tab.EstimateWidth();
            if (visible.Count > 0 && budget - width < 0)
            {
                break;
            }

            budget -= width;
            visible.Add(tab);
        }

        // Активная вкладка видна всегда: если она попала в вытесненные,
        // меняется местами с последней видимой.
        if (ActiveTab is not null && !visible.Contains(ActiveTab) && visible.Count > 0)
        {
            visible[^1] = ActiveTab;
        }

        return visible;
    }

    private static void SyncCollection(
        ObservableCollection<DocumentTabViewModel> target,
        IReadOnlyList<DocumentTabViewModel> source)
    {
        if (target.SequenceEqual(source))
        {
            return;
        }

        target.Clear();
        foreach (var tab in source)
        {
            target.Add(tab);
        }
    }

    private static string? TryGetParentFolderName(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        var name = string.IsNullOrEmpty(directory) ? null : Path.GetFileName(directory);
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private static bool PathsEqual(string? left, string? right)
        => left is not null
            && right is not null
            && string.Equals(
                left,
                right,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
