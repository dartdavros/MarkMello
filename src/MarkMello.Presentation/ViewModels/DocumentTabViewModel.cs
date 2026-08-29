using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Одна открытая вкладка. Держит документ, его позицию прокрутки и признак
/// принадлежности папке: файл, открытый поверх папки, даёт вкладку, но в дереве
/// не подсвечивается (ADR-0007 Rule 4).
/// </summary>
public sealed partial class DocumentTabViewModel : ObservableObject, IDisposable
{
    /// <summary>Ширина вкладки из макета: меньше 120px имя нечитаемо, больше 240px полоса пустеет.</summary>
    public const double MinimumWidth = 120;
    public const double MaximumWidth = 240;

    public DocumentTabViewModel(string? path, string title)
    {
        Path = path;
        _title = string.IsNullOrWhiteSpace(title) ? string.Empty : title;
    }

    /// <summary>Путь файла. <c>null</c> у нового документа, который ещё не сохраняли.</summary>
    public string? Path { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private string _title;

    /// <summary>
    /// Имя родительской папки. Появляется только у вкладок, чьи имена совпали,
    /// и снимается, когда конфликт исчезает.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(HasDisambiguator))]
    private string? _disambiguator;

    public bool HasDisambiguator => !string.IsNullOrEmpty(Disambiguator);

    /// <summary>Пометка состояния в заголовке вкладки: «(удалён)» у пропавшего файла.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private string? _stateSuffix;

    /// <summary>Файл изменился на диске, а во вкладке есть правки — ждём решения пользователя.</summary>
    [ObservableProperty]
    private bool _hasExternalChange;

    /// <summary>
    /// Снимок устарел: файл изменился, пока вкладка была в фоне. Перечитаем при возврате,
    /// а не в момент чужого сохранения — на фоновую вкладку никто не смотрит.
    /// </summary>
    public bool NeedsReload { get; set; }

    public string DisplayTitle => string.IsNullOrEmpty(StateSuffix)
        ? Title
        : $"{Title} {StateSuffix}";

    /// <summary>Полный путь в тултипе: имя без пути не отвечает на вопрос «какой из двух README».</summary>
    [ObservableProperty]
    private string _tooltip = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Файл лежит внутри открытой папки. У файлов, открытых поверх неё, — false.</summary>
    [ObservableProperty]
    private bool _belongsToWorkspace;

    [ObservableProperty]
    private bool _isActive;

    /// <summary>Позиция прокрутки: сохраняется при уходе с вкладки и восстанавливается при возврате.</summary>
    public double ScrollOffset { get; set; }

    /// <summary>
    /// Editor-сессия этой вкладки. Ленивая и живёт до закрытия вкладки: уход на соседнюю
    /// не должен терять несохранённые правки (ADR-0007 Rule 4).
    /// </summary>
    public EditorSessionViewModel? EditorSession { get; set; }

    /// <summary>Вкладка была в режиме правки — возвращаемся в него при активации.</summary>
    public bool IsEditMode { get; set; }

    public MarkdownSource? Document { get; private set; }

    public RenderedMarkdownDocument RenderedDocument { get; private set; } = RenderedMarkdownDocument.Empty;

    public void ApplyDocument(MarkdownSource? document, RenderedMarkdownDocument rendered)
    {
        Document = document;
        RenderedDocument = rendered;

        if (document is not null)
        {
            Path = document.Path;
            Title = document.FileName;
        }
    }

    /// <summary>Путь сменился после «Сохранить как»: вкладка следует за файлом.</summary>
    public void Retarget(string path, string title)
    {
        Path = path;
        Title = title;
    }

    /// <summary>Сессия закрывается вместе со вкладкой, а не при уходе на соседнюю.</summary>
    public void Dispose()
    {
        EditorSession?.Dispose();
        EditorSession = null;
    }

    /// <summary>
    /// Ширина вкладки для расчёта переполнения. Точную ширину знает только layout,
    /// но полоса всё равно зажата в [120; 240], поэтому оценки по длине имени достаточно,
    /// чтобы решить, сколько вкладок показать.
    /// </summary>
    public double EstimateWidth()
    {
        // 10px паддинги по краям + ~6.6px на символ 12px Inter Tight + 8px gap + 14px крестик.
        var content = 20 + (DisplayTitle.Length * 6.6) + 22;
        if (HasDisambiguator)
        {
            content += 6 + (Disambiguator!.Length * 5.8);
        }

        return Math.Clamp(content, MinimumWidth, MaximumWidth);
    }
}
