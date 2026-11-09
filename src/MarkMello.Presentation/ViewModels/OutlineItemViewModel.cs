using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>Пункт дерева оглавления. Свёрнутость живёт, пока открыт документ (ADR-0008 Rule 8).</summary>
public sealed partial class OutlineItemViewModel : ObservableObject
{
    private readonly Action<OutlineItemViewModel> _expansionChanged;

    internal OutlineItemViewModel(
        OutlineItem item,
        OutlineItemViewModel? parent,
        Action<OutlineItemViewModel> expansionChanged,
        Dictionary<string, int> occurrences)
    {
        Level = item.Level;
        Text = item.Text;
        _sourceLine = item.SourceLine;
        var pair = $"{item.Level}:{item.Text}";
        occurrences.TryGetValue(pair, out var index);
        occurrences[pair] = index + 1;
        Key = $"{pair}#{index}";
        Parent = parent;
        _expansionChanged = expansionChanged;
        Children = new ObservableCollection<OutlineItemViewModel>(
            item.Children.Select(child => new OutlineItemViewModel(child, this, expansionChanged, occurrences)));
    }

    public int Level { get; }

    public string Text { get; }


    public OutlineItemViewModel? Parent { get; }

    public ObservableCollection<OutlineItemViewModel> Children { get; }

    public bool HasChildren => Children.Count > 0;

    public bool IsTopLevel => Level == 1;

    /// <summary>Идентичность пункта: уровень, текст и порядковый номер такой пары в документе.</summary>
    internal string Key { get; }

    /// <summary>Строка исходника; правится на месте, когда меняется только положение заголовков.</summary>
    [ObservableProperty]
    private int _sourceLine;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private double _fontSize;

    /// <summary>Высота строки текста: размер × межстрочный интервал (Rule 11).</summary>
    [ObservableProperty]
    private double _lineBoxHeight;

    partial void OnIsExpandedChanged(bool value) => _expansionChanged(this);
}
