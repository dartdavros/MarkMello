using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Дерево оглавления активного документа. Не знает, viewer сейчас или редактор:
/// навигация уходит наружу событием со строкой исходника (ADR-0008, Presentation).
/// </summary>
public sealed partial class OutlineViewModel : ObservableObject
{
    private static readonly object NoDocumentKey = new();

    // Слабые ключи: закрытая вкладка уносит своё состояние свёрнутости, ничего не утекает.
    private static readonly ConditionalWeakTable<object, HashSet<string>> FoldState = new();

    private int? _currentSourceLine;
    private object? _currentKey;
    private bool _isApplying;

    public ObservableCollection<OutlineItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isEmpty = true;

    public event EventHandler<int>? NavigateRequested;

    /// <summary>
    /// Обновляет дерево. Если набор заголовков (уровень + текст) не изменился и документ тот же,
    /// существующие пункты правятся на месте: так правка текста не сбрасывает прокрутку панели.
    /// Свёрнутость хранится отдельно для каждого <paramref name="documentKey"/> (Rules 8 and 9).
    /// </summary>
    public void Update(RenderedMarkdownDocument document, OutlinePreferences preferences, object? documentKey = null)
    {
        var key = documentKey ?? NoDocumentKey;
        var built = DocumentOutlineBuilder.Build(document, preferences.MaxLevel).Items;

        if (ReferenceEquals(key, _currentKey) && TryPatchInPlace(built))
        {
            ApplyTypography(preferences);
            RefreshActive();
            return;
        }

        _isApplying = true;
        try
        {
            _currentKey = key;
            Items.Clear();
            var occurrences = new Dictionary<string, int>();
            foreach (var item in built)
            {
                Items.Add(new OutlineItemViewModel(item, parent: null, OnExpansionChanged, occurrences));
            }

            if (FoldState.TryGetValue(key, out var collapsed) && collapsed.Count > 0)
            {
                foreach (var item in Flatten(Items))
                {
                    item.IsExpanded = !collapsed.Contains(item.Key);
                }
            }
        }
        finally
        {
            _isApplying = false;
        }

        ApplyTypography(preferences);
        IsEmpty = Items.Count == 0;
        RefreshActive();
    }

    private bool TryPatchInPlace(IReadOnlyList<OutlineItem> built)
    {
        var current = Flatten(Items).ToList();
        var next = new List<OutlineItem>();
        FlattenSource(built, next);
        if (current.Count != next.Count)
        {
            return false;
        }

        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].Level != next[i].Level || current[i].Text != next[i].Text)
            {
                return false;
            }
        }

        for (var i = 0; i < current.Count; i++)
        {
            current[i].SourceLine = next[i].SourceLine;
        }

        return true;
    }

    private static void FlattenSource(IEnumerable<OutlineItem> items, List<OutlineItem> into)
    {
        foreach (var item in items)
        {
            into.Add(item);
            FlattenSource(item.Children, into);
        }
    }

    // Фиксируем только действия пользователя: пока Update сам расставляет состояние,
    // переходные пересборки при смене вкладки не должны затирать сохранённое.
    private void OnExpansionChanged(OutlineItemViewModel item)
    {
        if (_isApplying)
        {
            return;
        }

        if (_currentKey is { } key)
        {
            var collapsed = FoldState.GetOrCreateValue(key);
            if (item.IsExpanded)
            {
                collapsed.Remove(item.Key);
            }
            else
            {
                collapsed.Add(item.Key);
            }
        }

        RefreshActive();
    }

    public void ApplyTypography(OutlinePreferences preferences)
    {
        foreach (var item in Flatten(Items))
        {
            item.FontSize = preferences.GetFontSize(item.Level);
            item.LineBoxHeight = item.FontSize * preferences.LineHeight;
        }
    }

    public void SetCurrentSourceLine(int? sourceLine)
    {
        _currentSourceLine = sourceLine;
        RefreshActive();
    }

    [RelayCommand]
    private void Navigate(OutlineItemViewModel? item)
    {
        if (item is not null)
        {
            NavigateRequested?.Invoke(this, item.SourceLine);
        }
    }

    private void RefreshActive()
    {
        var target = FindVisibleTarget();
        foreach (var item in Flatten(Items))
        {
            item.IsActive = ReferenceEquals(item, target);
        }
    }

    /// <summary>
    /// Последний заголовок не ниже текущей строки; если он внутри свёрнутой ветки —
    /// её самый верхний свёрнутый предок, то есть ближайший видимый (Rule 7).
    /// </summary>
    private OutlineItemViewModel? FindVisibleTarget()
    {
        if (_currentSourceLine is not { } line)
        {
            return null;
        }

        var current = Flatten(Items).LastOrDefault(i => i.SourceLine <= line);
        if (current is null)
        {
            return null;
        }

        var path = new List<OutlineItemViewModel>();
        for (var node = current; node is not null; node = node.Parent)
        {
            path.Insert(0, node);
        }

        return path.FirstOrDefault(static node => !node.IsExpanded && node.HasChildren) is { } collapsed
            && !ReferenceEquals(collapsed, current)
                ? collapsed
                : current;
    }

    private static IEnumerable<OutlineItemViewModel> Flatten(IEnumerable<OutlineItemViewModel> items)
        => items.SelectMany(static i => Enumerable.Concat([i], Flatten(i.Children)));
}
