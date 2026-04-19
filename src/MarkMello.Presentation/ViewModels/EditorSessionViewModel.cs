using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Ленивая editor-сессия для текущего документа. Не участвует в startup path
/// и создаётся только при явном входе в edit mode.
/// </summary>
public sealed class EditorSessionViewModel : ObservableObject
{
    private readonly RenderMarkdownDocumentUseCase _renderMarkdown;
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
        IImageSourceResolver? imageSourceResolver)
        : this(
            source.Path,
            source.FileName,
            source.Content,
            readingPreferences,
            renderMarkdown,
            imageSourceResolver)
    {
        ArgumentNullException.ThrowIfNull(source);
    }

    public EditorSessionViewModel(
        string fileName,
        string initialContent,
        ReadingPreferences readingPreferences,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IImageSourceResolver? imageSourceResolver)
        : this(
            currentPath: null,
            fileName,
            initialContent,
            readingPreferences,
            renderMarkdown,
            imageSourceResolver)
    {
    }

    private EditorSessionViewModel(
        string? currentPath,
        string fileName,
        string initialContent,
        ReadingPreferences readingPreferences,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IImageSourceResolver? imageSourceResolver)
    {
        ArgumentNullException.ThrowIfNull(renderMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _renderMarkdown = renderMarkdown;
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

    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (SetProperty(ref _sourceText, value ?? string.Empty))
            {
                RenderedPreview = RenderPreview(_sourceText, _currentPath);
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
            }
        }
    }

    public string? CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetProperty(ref _currentPath, value))
            {
                RenderedPreview = RenderPreview(SourceText, _currentPath);
            }
        }
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
        private set => SetProperty(ref _readingPreferences, value);
    }

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
        RaiseDocumentMetricsChanged();
    }

    public void DiscardChanges()
    {
        SourceText = LastPersistedSource;
        StatusMessage = string.Empty;
    }

    public void SetStatusMessage(string? message)
    {
        StatusMessage = message ?? string.Empty;
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
