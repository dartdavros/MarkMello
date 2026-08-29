using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Editing;
using MarkMello.Presentation.Localization;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Ленивая editor-сессия для текущего документа. Не участвует в startup path
/// и создаётся только при явном входе в edit mode.
/// </summary>
public sealed class EditorSessionViewModel : ObservableObject, IDisposable
{
    private readonly RenderMarkdownDocumentUseCase _renderMarkdown;
    private readonly ILocalizationService _localization;
    private readonly IEditorPreviewScheduler _previewScheduler;
    private string _sourceText;
    private string _lastPersistedSource;
    private string? _currentPath;
    private string _fileName;
    private double _splitRatio;
    private ReadingPreferences _readingPreferences;
    private RenderedMarkdownDocument _renderedPreview;
    private string _statusMessage;

    public EditorSessionViewModel(
        MarkdownSource source,
        ReadingPreferences readingPreferences,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IImageSourceResolver? imageSourceResolver,
        ILocalizationService? localization = null,
        IEditorPreviewScheduler? previewScheduler = null)
        : this(
            source.Path,
            source.FileName,
            source.Content,
            readingPreferences,
            renderMarkdown,
            imageSourceResolver,
            localization,
            previewScheduler)
    {
        ArgumentNullException.ThrowIfNull(source);
    }

    public EditorSessionViewModel(
        string fileName,
        string initialContent,
        ReadingPreferences readingPreferences,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IImageSourceResolver? imageSourceResolver,
        ILocalizationService? localization = null,
        IEditorPreviewScheduler? previewScheduler = null)
        : this(
            currentPath: null,
            fileName,
            initialContent,
            readingPreferences,
            renderMarkdown,
            imageSourceResolver,
            localization,
            previewScheduler)
    {
    }

    private EditorSessionViewModel(
        string? currentPath,
        string fileName,
        string initialContent,
        ReadingPreferences readingPreferences,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IImageSourceResolver? imageSourceResolver,
        ILocalizationService? localization,
        IEditorPreviewScheduler? previewScheduler)
    {
        ArgumentNullException.ThrowIfNull(renderMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _renderMarkdown = renderMarkdown;
        _localization = localization ?? new LocalizationService();
        _previewScheduler = previewScheduler ?? ImmediateEditorPreviewScheduler.Instance;
        ImageSourceResolver = imageSourceResolver;
        _currentPath = currentPath;
        _fileName = fileName;
        _readingPreferences = readingPreferences;
        _lastPersistedSource = initialContent ?? string.Empty;
        _sourceText = initialContent ?? string.Empty;
        _renderedPreview = RenderPreview(_sourceText, _currentPath);
        _statusMessage = string.Empty;
        _splitRatio = 0.5;
    }

    public IImageSourceResolver? ImageSourceResolver { get; }

    private static readonly string[] LocalizedBindingPropertyNames =
    [
        nameof(EditorBoldTooltip),
        nameof(EditorCodeTooltip),
        nameof(EditorItalicTooltip),
        nameof(EditorLinkTooltip),
        nameof(EditorListTooltip),
        nameof(EditorQuoteTooltip),
        nameof(EditorProtectedImageDataMessage),
        nameof(EditorSourceLabel),
    ];

    public string EditorBoldTooltip => _localization["EditorBoldTooltip"];
    public string EditorCodeTooltip => _localization["EditorCodeTooltip"];
    public string EditorItalicTooltip => _localization["EditorItalicTooltip"];
    public string EditorLinkTooltip => _localization["EditorLinkTooltip"];
    public string EditorListTooltip => _localization["EditorListTooltip"];
    public string EditorQuoteTooltip => _localization["EditorQuoteTooltip"];
    public string EditorProtectedImageDataMessage => _localization["EditorProtectedImageDataMessage"];
    public string EditorSourceLabel => _localization["EditorSourceLabel"];

    public void RefreshLocalizedProperties()
    {
        foreach (var propertyName in LocalizedBindingPropertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (SetProperty(ref _sourceText, value ?? string.Empty))
            {
                // Ввод текста — горячий путь: preview пересобирается отложенно и
                // вне UI-потока, иначе каждый символ стоит полного parse документа.
                SchedulePreviewRefresh();
                StatusMessage = string.Empty;
                RaiseDocumentMetricsChanged();
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string LastPersistedSource
    {
        get => _lastPersistedSource;
        private set
        {
            if (SetProperty(ref _lastPersistedSource, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(DocumentNewLine));
            }
        }
    }

    /// <summary>
    /// Перевод строки, которым редактор продолжает документ. Берётся из
    /// загруженного содержимого, чтобы Enter не подменял LF на CRLF и не оставлял
    /// документ «изменённым» после отката правки.
    /// </summary>
    public string DocumentNewLine => MarkdownLineEndings.Detect(LastPersistedSource);

    public string? CurrentPath
    {
        get => _currentPath;
        // Меняется только вместе с содержимым документа (load/save), поэтому
        // сам по себе preview не перерисовывает — это делает RefreshPreviewNow.
        private set => SetProperty(ref _currentPath, value);
    }

    public string FileName
    {
        get => _fileName;
        private set => SetProperty(ref _fileName, value);
    }

    public double SplitRatio
    {
        get => _splitRatio;
        set => SetProperty(ref _splitRatio, Math.Clamp(value, 0.2, 0.8));
    }

    public ReadingPreferences ReadingPreferences
    {
        get => _readingPreferences;
        private set
        {
            if (SetProperty(ref _readingPreferences, value))
            {
                OnPropertyChanged(nameof(DocumentColumnMaxWidth));
            }
        }
    }

    public double DocumentColumnMaxWidth => ReadingLayoutMetrics.GetDocumentColumnMaxWidth(ReadingPreferences);

    public RenderedMarkdownDocument RenderedPreview
    {
        get => _renderedPreview;
        private set => SetProperty(ref _renderedPreview, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsDirty => !string.Equals(SourceText, LastPersistedSource, StringComparison.Ordinal);

    public int WordCount => CountWords(SourceText);

    public int ReadTimeMinutes => Math.Max(1, (int)Math.Round(WordCount / 220.0));

    public void UpdateReadingPreferences(ReadingPreferences preferences)
    {
        ReadingPreferences = ReadingPreferences.Normalize(preferences);
    }

    public void ApplyLoadedDocument(MarkdownSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        CurrentPath = source.Path;
        FileName = source.FileName;
        LastPersistedSource = source.Content;
        SourceText = source.Content;
        StatusMessage = string.Empty;
        RefreshPreviewNow();
        RaiseDocumentMetricsChanged();
    }

    public void ApplySavedDocument(MarkdownSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        CurrentPath = source.Path;
        FileName = source.FileName;
        LastPersistedSource = source.Content;
        SourceText = source.Content;
        StatusMessage = string.Empty;
        RefreshPreviewNow();
        RaiseDocumentMetricsChanged();
    }

    public void DiscardChanges()
    {
        SourceText = LastPersistedSource;
        StatusMessage = string.Empty;
        RefreshPreviewNow();
    }

    public void UpdateDraftFileName(string fileName)
    {
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        FileName = fileName;
    }

    public void SetStatusMessage(string? message)
    {
        StatusMessage = message ?? string.Empty;
    }

    /// <summary>
    /// Гасит отложенный рендер, чтобы результат закрытой сессии не долетал до
    /// UI и не удерживал её через таймер.
    /// </summary>
    public void Dispose()
    {
        _previewScheduler.Cancel();
        (_previewScheduler as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Ставит отложенную пересборку preview. Снимок текста и пути берётся сразу,
    /// чтобы фоновый рендер не читал состояние, которое к тому моменту уехало.
    /// </summary>
    private void SchedulePreviewRefresh()
    {
        var markdown = _sourceText;
        var path = _currentPath;
        _previewScheduler.Schedule(
            () => RenderPreview(markdown, path),
            preview => RenderedPreview = preview);
    }

    /// <summary>
    /// Немедленная пересборка preview для смены документа целиком (load/save/discard),
    /// где задержка выглядела бы как подвисший экран.
    /// </summary>
    private void RefreshPreviewNow()
    {
        _previewScheduler.Cancel();
        RenderedPreview = RenderPreview(_sourceText, _currentPath);
    }

    private RenderedMarkdownDocument RenderPreview(string markdown, string? path)
        => _renderMarkdown.Execute(markdown, ResolveBaseDirectory(path));

    private static string? ResolveBaseDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(path);
        }
        catch
        {
            return null;
        }
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var trimmed = text.AsSpan().Trim();
        if (trimmed.IsEmpty)
        {
            return 0;
        }

        var count = 0;
        var inWord = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    private void RaiseDocumentMetricsChanged()
    {
        OnPropertyChanged(nameof(WordCount));
        OnPropertyChanged(nameof(ReadTimeMinutes));
    }
}
