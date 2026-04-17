using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// View model главного окна. Отвечает за state machine (NoDocument/Viewing/LoadError),
/// тему, команды open/reload, drag-overlay, reading progress, метрики Stage 3.
/// Editor-specific properties отсутствуют — это constitution §4.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly OpenDocumentUseCase _openDocument;
    private readonly IFilePicker _filePicker;
    private readonly ICommandLineActivation _commandLine;
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themeService;
    private readonly IStartupMetrics _startupMetrics;

    private bool _stage3Marked;
    private string? _currentPath;
    private readonly bool _showCustomTitleBar = OperatingSystem.IsWindows();

    public MainWindowViewModel(
        OpenDocumentUseCase openDocument,
        IFilePicker filePicker,
        ICommandLineActivation commandLine,
        ISettingsStore settings,
        IThemeService themeService,
        IStartupMetrics startupMetrics)
    {
        _openDocument = openDocument;
        _filePicker = filePicker;
        _commandLine = commandLine;
        _settings = settings;
        _themeService = themeService;
        _startupMetrics = startupMetrics;
    }

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
    private double _readingProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextThemeHint))]
    private ThemeMode _theme = ThemeMode.System;

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

    public string NextThemeHint => Theme switch
    {
        ThemeMode.System => "Switch to Light",
        ThemeMode.Light => "Switch to Dark",
        ThemeMode.Dark => "Follow system",
        _ => "Theme"
    };

    // ---------- Lifecycle ----------

    /// <summary>
    /// Вызывается из MainWindow.Opened. Загружает тему, применяет, и пытается открыть
    /// файл из command-line, если он передан.
    /// </summary>
    public async Task InitializeAsync()
    {
        var savedTheme = await _settings.LoadThemeAsync().ConfigureAwait(true);
        Theme = savedTheme;
        _themeService.Apply(savedTheme);

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
        var next = Theme switch
        {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.System
        };
        Theme = next;
        _themeService.Apply(next);
        await _settings.SaveThemeAsync(next).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ClearError()
    {
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
        }
    }

    private void ShowError(string title, string details)
    {
        ErrorTitle = title;
        ErrorDetails = details;
        State = ViewState.LoadError;
        WindowTitle = "MarkMello";
    }
}
