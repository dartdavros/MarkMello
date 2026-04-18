using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;
using System.Collections.Generic;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// View model главного окна. Отвечает за state machine (NoDocument/Viewing/LoadError),
/// тему, reading preferences, команды open/reload, drag-overlay, reading progress,
/// метрики Stage 3 и компактную M4 settings panel. Editor-specific properties
/// по-прежнему отсутствуют — это constitution §4.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly OpenDocumentUseCase _openDocument;
    private readonly IFilePicker _filePicker;
    private readonly ICommandLineActivation _commandLine;
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themeService;
    private readonly IStartupMetrics _startupMetrics;
    private readonly RenderMarkdownDocumentUseCase _renderMarkdown;
    private readonly IImageSourceResolver? _imageSourceResolver;

    private bool _stage3Marked;
    private string? _currentPath;
    private readonly bool _showCustomTitleBar = OperatingSystem.IsWindows();

    public MainWindowViewModel(
        OpenDocumentUseCase openDocument,
        IFilePicker filePicker,
        ICommandLineActivation commandLine,
        ISettingsStore settings,
        IThemeService themeService,
        IStartupMetrics startupMetrics,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IImageSourceResolver? imageSourceResolver = null)
    {
        _openDocument = openDocument;
        _filePicker = filePicker;
        _commandLine = commandLine;
        _settings = settings;
        _themeService = themeService;
        _startupMetrics = startupMetrics;
        _renderMarkdown = renderMarkdown;
        _imageSourceResolver = imageSourceResolver;
    }

    /// <summary>
    /// Resolver used by the markdown view to load image block content. Null
    /// in design-time or tests; the view renders "Image unavailable"
    /// placeholders in that case.
    /// </summary>
    public IImageSourceResolver? ImageSourceResolver => _imageSourceResolver;

    // ---------- State ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome))]
    [NotifyPropertyChangedFor(nameof(IsViewer))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private ViewState _state = ViewState.NoDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName))]
    [NotifyPropertyChangedFor(nameof(WordCount))]
    [NotifyPropertyChangedFor(nameof(ReadTimeMinutes))]
    private MarkdownSource? _document;

    [ObservableProperty]
    private string _windowTitle = "MarkMello";

    [ObservableProperty]
    private bool _isDragHovering;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private double _readingProgress;

    [ObservableProperty]
    private ThemeMode _theme = ThemeMode.System;

    [ObservableProperty]
    private ReadingPreferences _readingPreferences = ReadingPreferences.Default;

    [ObservableProperty]
    private RenderedMarkdownDocument _renderedDocument = RenderedMarkdownDocument.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMoonThemeIcon))]
    [NotifyPropertyChangedFor(nameof(ShowsSunThemeIcon))]
    [NotifyPropertyChangedFor(nameof(NextThemeHint))]
    private ThemeMode _effectiveTheme = ThemeMode.Light;

    // ---------- Error state ----------

    [ObservableProperty]
    private string _errorTitle = string.Empty;

    [ObservableProperty]
    private string _errorDetails = string.Empty;

    // ---------- Computed ----------

    public string FileName => Document?.FileName ?? string.Empty;

    public bool IsWelcome => State == ViewState.NoDocument;
    public bool IsViewer => State == ViewState.Viewing;
    public bool IsError => State == ViewState.LoadError;

    public bool ShowCustomTitleBar => _showCustomTitleBar;
    public bool ShowsMoonThemeIcon => EffectiveTheme == ThemeMode.Light;
    public bool ShowsSunThemeIcon => EffectiveTheme == ThemeMode.Dark;
    public IReadOnlyList<FontFamilyMode> FontFamilyModes { get; } = Enum.GetValues<FontFamilyMode>();

    public FontFamilyMode SelectedFontFamilyMode
    {
        get => ReadingPreferences.FontFamily;
        set
        {
            if (ReadingPreferences.FontFamily == value)
            {
                return;
            }

            ApplyReadingPreferences(ReadingPreferences with { FontFamily = value });
        }
    }

    public double FontSizeSetting
    {
        get => ReadingPreferences.FontSize;
        set
        {
            var fontSize = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (ReadingPreferences.FontSize == fontSize)
            {
                return;
            }

            ApplyReadingPreferences(ReadingPreferences with { FontSize = fontSize });
        }
    }

    public double LineHeightSetting
    {
        get => ReadingPreferences.LineHeight;
        set
        {
            var normalized = Math.Round(value / ReadingPreferences.LineHeightStep, MidpointRounding.AwayFromZero) * ReadingPreferences.LineHeightStep;
            if (Math.Abs(ReadingPreferences.LineHeight - normalized) < 0.0001)
            {
                return;
            }

            ApplyReadingPreferences(ReadingPreferences with { LineHeight = normalized });
        }
    }

    public double ContentWidthSetting
    {
        get => ReadingPreferences.ContentWidth;
        set
        {
            var contentWidth = (int)Math.Round(value / ReadingPreferences.ContentWidthStep, MidpointRounding.AwayFromZero) * ReadingPreferences.ContentWidthStep;
            if (ReadingPreferences.ContentWidth == contentWidth)
            {
                return;
            }

            ApplyReadingPreferences(ReadingPreferences with { ContentWidth = contentWidth });
        }
    }

    public string FontSizeLabel => $"{ReadingPreferences.FontSize}px";
    public string LineHeightLabel => $"{ReadingPreferences.LineHeight:0.00}x";
    public string ContentWidthLabel => $"{ReadingPreferences.ContentWidth}px";
    public string ThemeDescription => Theme == ThemeMode.System
        ? "Theme follows the OS on first launch. The top-right toggle pins Light or Dark once you choose."
        : $"Theme is pinned to {Theme.ToString().ToLowerInvariant()} and restored on startup.";

    public int WordCount
    {
        get
        {
            if (Document is null || string.IsNullOrWhiteSpace(Document.Content))
            {
                return 0;
            }
            var trimmed = Document.Content.AsSpan().Trim();
            if (trimmed.IsEmpty)
            {
                return 0;
            }
            int count = 0;
            bool inWord = false;
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
    }

    public int ReadTimeMinutes => Math.Max(1, (int)Math.Round(WordCount / 220.0));

    public string NextThemeHint => EffectiveTheme == ThemeMode.Light
        ? "Switch to dark theme"
        : "Switch to light theme";

    // ---------- Lifecycle ----------

    /// <summary>
    /// Вызывается из MainWindow.Opened. Загружает тему, применяет, и пытается открыть
    /// файл из command-line, если он передан.
    /// </summary>
    public async Task InitializeAsync()
    {
        ReadingPreferences = await _settings.LoadPreferencesAsync().ConfigureAwait(true);

        var savedTheme = await _settings.LoadThemeAsync().ConfigureAwait(true);
        ApplyTheme(savedTheme);

        var path = _commandLine.GetActivationFilePath();
        if (!string.IsNullOrEmpty(path))
        {
            await OpenPathAsync(path).ConfigureAwait(true);
        }
    }

    // ---------- Commands ----------

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _filePicker.PickMarkdownFileAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        await OpenPathAsync(path).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync()
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            return;
        }
        await OpenPathAsync(_currentPath).ConfigureAwait(true);
    }

    private bool CanReload() => !string.IsNullOrEmpty(_currentPath);

    [RelayCommand]
    private async Task CycleThemeAsync()
    {
        var next = EffectiveTheme == ThemeMode.Light
            ? ThemeMode.Dark
            : ThemeMode.Light;

        ApplyTheme(next);
        await _settings.SaveThemeAsync(next).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void ResetReadingPreferences()
    {
        ApplyReadingPreferences(ReadingPreferences.Default);
    }

    [RelayCommand]
    private void ClearError()
    {
        if (IsSettingsOpen)
        {
            IsSettingsOpen = false;
            return;
        }

        if (State == ViewState.LoadError)
        {
            State = Document is null ? ViewState.NoDocument : ViewState.Viewing;
            ErrorTitle = string.Empty;
            ErrorDetails = string.Empty;
        }
    }

    // ---------- Drag & drop entry points ----------

    public Task OpenDroppedFileAsync(string path) => OpenPathAsync(path);

    // ---------- Core ----------

    public async Task OpenPathAsync(string path)
    {
        var result = await _openDocument.ExecuteAsync(path).ConfigureAwait(true);
        ApplyOpenResult(result);
    }

    private void ApplyOpenResult(OpenDocumentResult result)
    {
        switch (result)
        {
            case OpenDocumentResult.Success success:
                Document = success.Source;
                RenderedDocument = _renderMarkdown.Execute(
                    success.Source.Content,
                    baseDirectory: TryGetDirectory(success.Source.Path));
                _currentPath = success.Source.Path;
                State = ViewState.Viewing;
                WindowTitle = $"{success.Source.FileName} — MarkMello";
                ReadingProgress = 0;
                ErrorTitle = string.Empty;
                ErrorDetails = string.Empty;

                if (!_stage3Marked)
                {
                    _stage3Marked = true;
                    _startupMetrics.Mark(StartupStage.ReadableDocument);
                }
                ReloadCommand.NotifyCanExecuteChanged();
                break;

            case OpenDocumentResult.NotFound notFound:
                ShowError("Couldn't find that file", notFound.Path);
                break;

            case OpenDocumentResult.AccessDenied denied:
                ShowError("Access denied", denied.Path);
                break;

            case OpenDocumentResult.ReadError read:
                ShowError("Couldn't read the file", $"{read.Path}\n\n{read.Message}");
                break;

            case OpenDocumentResult.UnsupportedType unsupported:
                ShowError(
                    "Unsupported file type",
                    $"{unsupported.Path}\n\nSupported extensions: {string.Join(", ", SupportedDocumentTypes.Extensions)}");
                break;
        }
    }

    private void ApplyTheme(ThemeMode mode)
    {
        Theme = mode;
        _themeService.Apply(mode);
        EffectiveTheme = _themeService.GetEffectiveTheme();
    }

    private void ShowError(string title, string details)
    {
        ErrorTitle = title;
        ErrorDetails = details;
        State = ViewState.LoadError;
        WindowTitle = "MarkMello";
    }

    private static string? TryGetDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return System.IO.Path.GetDirectoryName(path);
        }
        catch
        {
            // Invalid characters or permission failure -- not fatal for viewer path.
            return null;
        }
    }

    partial void OnReadingPreferencesChanged(ReadingPreferences value)
    {
        OnPropertyChanged(nameof(SelectedFontFamilyMode));
        OnPropertyChanged(nameof(FontSizeSetting));
        OnPropertyChanged(nameof(LineHeightSetting));
        OnPropertyChanged(nameof(ContentWidthSetting));
        OnPropertyChanged(nameof(FontSizeLabel));
        OnPropertyChanged(nameof(LineHeightLabel));
        OnPropertyChanged(nameof(ContentWidthLabel));
    }

    partial void OnThemeChanged(ThemeMode value)
    {
        OnPropertyChanged(nameof(ThemeDescription));
    }

    private void ApplyReadingPreferences(ReadingPreferences preferences)
    {
        var normalized = ReadingPreferences.Normalize(preferences);
        if (normalized == ReadingPreferences)
        {
            return;
        }

        ReadingPreferences = normalized;
        PersistReadingPreferences(normalized);
    }

    private void PersistReadingPreferences(ReadingPreferences preferences)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _settings.SavePreferencesAsync(preferences).ConfigureAwait(false);
            }
            catch
            {
                // Persistence is best-effort; a failed save must not interrupt
                // the viewer interaction loop.
            }
        });
    }
}
