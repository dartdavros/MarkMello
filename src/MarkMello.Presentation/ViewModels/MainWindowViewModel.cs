using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;
using System.ComponentModel;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// View model главного окна. Отвечает за state machine (NoDocument/Viewing/LoadError),
/// тему, reading preferences, команды open/reload, lazy edit mode и dirty/save flow.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private const string NewDocumentFileName = "Untitled.md";

    private readonly OpenDocumentUseCase _openDocument;
    private readonly SaveDocumentUseCase _saveDocument;
    private readonly IFilePicker _filePicker;
    private readonly ICommandLineActivation _commandLine;
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themeService;
    private readonly IStartupMetrics _startupMetrics;
    private readonly RenderMarkdownDocumentUseCase _renderMarkdown;
    private readonly IImageSourceResolver? _imageSourceResolver;

    private bool _stage3Marked;
    private bool _editorActivationMarked;
    private string? _currentPath;
    private Func<Task>? _pendingDirtyAction;
    private readonly bool _showCustomTitleBar = OperatingSystem.IsWindows();

    public event EventHandler? CloseRequested;

    public MainWindowViewModel(
        OpenDocumentUseCase openDocument,
        SaveDocumentUseCase saveDocument,
        IFilePicker filePicker,
        ICommandLineActivation commandLine,
        ISettingsStore settings,
        IThemeService themeService,
        IStartupMetrics startupMetrics,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IImageSourceResolver? imageSourceResolver = null)
    {
        _openDocument = openDocument;
        _saveDocument = saveDocument;
        _filePicker = filePicker;
        _commandLine = commandLine;
        _settings = settings;
        _themeService = themeService;
        _startupMetrics = startupMetrics;
        _renderMarkdown = renderMarkdown;
        _imageSourceResolver = imageSourceResolver;
    }

    public IImageSourceResolver? ImageSourceResolver => _imageSourceResolver;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome))]
    [NotifyPropertyChangedFor(nameof(IsViewer))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private ViewState _state = ViewState.NoDocument;

    [ObservableProperty]
    private MarkdownSource? _document;

    [ObservableProperty]
    private string _windowTitle = "MarkMello";

    [ObservableProperty]
    private bool _isDragHovering;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    [NotifyPropertyChangedFor(nameof(IsAppMenuOpen))]
    [NotifyPropertyChangedFor(nameof(IsAppSettingsOpen))]
    [NotifyPropertyChangedFor(nameof(IsAppOverlayOpen))]
    [NotifyPropertyChangedFor(nameof(HasOpenOverlay))]
    private ShellOverlayKind _shellOverlay = ShellOverlayKind.None;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveDocumentContent))]
    [NotifyPropertyChangedFor(nameof(EditToggleLabel))]
    [NotifyPropertyChangedFor(nameof(EditShortcutLabel))]
    [NotifyPropertyChangedFor(nameof(ShowsEditPencilIcon))]
    [NotifyPropertyChangedFor(nameof(ShowsReadEyeIcon))]
    private bool _isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveDocumentContent))]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private EditorSessionViewModel? _editorSession;

    [ObservableProperty]
    private bool _isDirtyPromptOpen;

    [ObservableProperty]
    private string _dirtyPromptTitle = string.Empty;

    [ObservableProperty]
    private string _dirtyPromptMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDirtyPromptError))]
    private string _dirtyPromptErrorMessage = string.Empty;

    [ObservableProperty]
    private string _errorTitle = string.Empty;

    [ObservableProperty]
    private string _errorDetails = string.Empty;

    public object ActiveDocumentContent => IsEditMode && EditorSession is not null ? EditorSession : this;

    public string FileName => EditorSession?.FileName ?? Document?.FileName ?? string.Empty;

    public string TitleFileDisplayName => string.IsNullOrWhiteSpace(FileName)
        ? string.Empty
        : FileName + (IsDirty ? " •" : string.Empty);

    public bool HasDocumentTitle => State == ViewState.Viewing && !string.IsNullOrWhiteSpace(FileName);

    public bool IsWelcome => State == ViewState.NoDocument;

    public bool IsViewer => State == ViewState.Viewing;

    public bool IsError => State == ViewState.LoadError;

    public bool IsDirty => EditorSession?.IsDirty == true;

    public bool ShowCustomTitleBar => _showCustomTitleBar;

    public bool IsSettingsOpen => ShellOverlay == ShellOverlayKind.ReadingSettings;

    public bool IsAppMenuOpen => ShellOverlay == ShellOverlayKind.AppMenu;

    public bool IsAppSettingsOpen => ShellOverlay == ShellOverlayKind.AppSettings;

    public bool IsAppOverlayOpen => ShellOverlay is ShellOverlayKind.AppMenu or ShellOverlayKind.AppSettings;

    public bool HasOpenOverlay => ShellOverlay != ShellOverlayKind.None;

    public bool ShowsReadingStatus => IsViewer && !IsEditMode;

    public bool ShowsMoonThemeIcon => EffectiveTheme == ThemeMode.Light;

    public bool ShowsSunThemeIcon => EffectiveTheme == ThemeMode.Dark;

    public bool ShowsEditPencilIcon => !IsEditMode;

    public bool ShowsReadEyeIcon => IsEditMode;

    public bool ShowsEditToggle => State == ViewState.Viewing && Document is not null;

    public string EditToggleLabel => IsEditMode ? "Reading" : "Edit";

    public string EditShortcutLabel => IsEditMode ? "read" : "edit";

    public bool HasDirtyPromptError => !string.IsNullOrWhiteSpace(DirtyPromptErrorMessage);

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
            var normalized = Math.Round(
                value / ReadingPreferences.LineHeightStep,
                MidpointRounding.AwayFromZero) * ReadingPreferences.LineHeightStep;

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
            var contentWidth = (int)Math.Round(
                value / ReadingPreferences.ContentWidthStep,
                MidpointRounding.AwayFromZero) * ReadingPreferences.ContentWidthStep;

            if (ReadingPreferences.ContentWidth == contentWidth)
            {
                return;
            }

            ApplyReadingPreferences(ReadingPreferences with { ContentWidth = contentWidth });
        }
    }

    public string FontSizeLabel => $"{ReadingPreferences.FontSize}px";

    public string LineHeightLabel => $"{ReadingPreferences.LineHeight:0.00}";

    public bool IsSerifFontSelected
    {
        get => ReadingPreferences.FontFamily == FontFamilyMode.Serif;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsSerifFontSelected));
                return;
            }

            SelectedFontFamilyMode = FontFamilyMode.Serif;
        }
    }

    public bool IsSansFontSelected
    {
        get => ReadingPreferences.FontFamily == FontFamilyMode.Sans;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsSansFontSelected));
                return;
            }

            SelectedFontFamilyMode = FontFamilyMode.Sans;
        }
    }

    public bool IsMonoFontSelected
    {
        get => ReadingPreferences.FontFamily == FontFamilyMode.Mono;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsMonoFontSelected));
                return;
            }

            SelectedFontFamilyMode = FontFamilyMode.Mono;
        }
    }

    public bool IsNarrowWidthSelected
    {
        get => ReadingPreferences.ContentWidth == 580;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsNarrowWidthSelected));
                return;
            }

            ContentWidthSetting = 580;
        }
    }

    public bool IsMediumWidthSelected
    {
        get => ReadingPreferences.ContentWidth == 720;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsMediumWidthSelected));
                return;
            }

            ContentWidthSetting = 720;
        }
    }

    public bool IsWideWidthSelected
    {
        get => ReadingPreferences.ContentWidth == 860;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsWideWidthSelected));
                return;
            }

            ContentWidthSetting = 860;
        }
    }

    public int WordCount => EditorSession?.WordCount ?? CountWords(Document?.Content);

    public int ReadTimeMinutes => Math.Max(1, (int)Math.Round(WordCount / 220.0));

    public string NextThemeHint => EffectiveTheme == ThemeMode.Light
        ? "Switch to dark theme"
        : "Switch to light theme";

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

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        CloseOverlayCore();
        await RunWithDirtyCheckAsync(PendingDirtyActionKind.OpenFile, OpenFileCoreAsync).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateNewDocumentAsync()
    {
        CloseOverlayCore();
        await RunWithDirtyCheckAsync(
                PendingDirtyActionKind.CreateNewDocument,
                CreateNewDocumentCoreAsync)
            .ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCloseFile))]
    private async Task CloseFileAsync()
    {
        CloseOverlayCore();
        await RunWithDirtyCheckAsync(
                PendingDirtyActionKind.CloseFile,
                CloseFileCoreAsync)
            .ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync()
    {
        var path = CurrentDocumentPath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var preserveEditMode = IsEditMode;
        await RunWithDirtyCheckAsync(
            PendingDirtyActionKind.Reload,
            () => LoadDocumentAsync(path, preserveEditModeAfterLoad: preserveEditMode))
            .ConfigureAwait(true);
    }

    private bool CanReload() => !string.IsNullOrEmpty(CurrentDocumentPath);

    private bool CanCloseFile() => Document is not null || EditorSession is not null;

    [RelayCommand(CanExecute = nameof(CanToggleEditMode))]
    private async Task ToggleEditModeAsync()
    {
        if (IsEditMode)
        {
            await RunWithDirtyCheckAsync(
                PendingDirtyActionKind.LeaveEditMode,
                ExitEditModeCoreAsync)
                .ConfigureAwait(true);
            return;
        }

        EnterEditModeCore();
    }

    private bool CanToggleEditMode() => State == ViewState.Viewing && Document is not null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var outcome = await SaveEditorAsync(promptForPathWhenMissing: true, forceSaveAs: false).ConfigureAwait(true);
        if (outcome.Cancelled)
        {
            return;
        }

        if (outcome.Result is not SaveDocumentResult.Success success)
        {
            EditorSession?.SetStatusMessage(GetSaveFailureMessage(outcome.Result));
            return;
        }

        ApplySavedDocument(success.Source);
    }

    private bool CanSave() => IsEditMode && EditorSession is not null;

    [RelayCommand(CanExecute = nameof(CanSaveAs))]
    private async Task SaveAsAsync()
    {
        var outcome = await SaveEditorAsync(promptForPathWhenMissing: true, forceSaveAs: true).ConfigureAwait(true);
        if (outcome.Cancelled)
        {
            return;
        }

        if (outcome.Result is not SaveDocumentResult.Success success)
        {
            EditorSession?.SetStatusMessage(GetSaveFailureMessage(outcome.Result));
            return;
        }

        ApplySavedDocument(success.Source);
    }

    private bool CanSaveAs() => IsEditMode && EditorSession is not null;

    [RelayCommand]
    private async Task ConfirmDirtySaveAsync()
    {
        if (_pendingDirtyAction is null)
        {
            return;
        }

        DirtyPromptErrorMessage = string.Empty;

        var outcome = await SaveEditorAsync(promptForPathWhenMissing: true, forceSaveAs: false).ConfigureAwait(true);
        if (outcome.Cancelled)
        {
            return;
        }

        if (outcome.Result is not SaveDocumentResult.Success success)
        {
            DirtyPromptErrorMessage = GetSaveFailureMessage(outcome.Result);
            return;
        }

        ApplySavedDocument(success.Source);
        await ContinuePendingDirtyActionAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ConfirmDirtyDiscardAsync()
    {
        DiscardEditorChanges();
        await ContinuePendingDirtyActionAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void CancelDirtyPrompt()
    {
        ClearDirtyPrompt();
    }

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
        ShellOverlay = IsSettingsOpen
            ? ShellOverlayKind.None
            : ShellOverlayKind.ReadingSettings;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        if (IsSettingsOpen)
        {
            ShellOverlay = ShellOverlayKind.None;
        }
    }

    [RelayCommand]
    private void ToggleAppMenu()
    {
        ShellOverlay = IsAppOverlayOpen
            ? ShellOverlayKind.None
            : ShellOverlayKind.AppMenu;
    }

    [RelayCommand]
    private void OpenAppSettings()
    {
        ShellOverlay = ShellOverlayKind.AppSettings;
    }

    [RelayCommand]
    private void ReturnToAppMenu()
    {
        ShellOverlay = ShellOverlayKind.AppMenu;
    }

    [RelayCommand]
    private void CloseOverlay()
    {
        CloseOverlayCore();
    }

    [RelayCommand]
    private void ClearError()
    {
        if (IsDirtyPromptOpen)
        {
            CancelDirtyPrompt();
            return;
        }

        if (HasOpenOverlay)
        {
            CloseOverlayCore();
            return;
        }

        if (State == ViewState.LoadError)
        {
            State = Document is null ? ViewState.NoDocument : ViewState.Viewing;
            ErrorTitle = string.Empty;
            ErrorDetails = string.Empty;
            RefreshWindowTitle();
        }
    }

    public async Task OpenDroppedFileAsync(string path)
        => await RunWithDirtyCheckAsync(
            PendingDirtyActionKind.OpenFile,
            () => LoadDocumentAsync(path, preserveEditModeAfterLoad: false))
            .ConfigureAwait(true);

    public async Task OpenPathAsync(string path)
        => await LoadDocumentAsync(path, preserveEditModeAfterLoad: false).ConfigureAwait(true);

    public bool TryQueueCloseRequest()
    {
        if (IsDirtyPromptOpen)
        {
            return true;
        }

        if (!RequiresDirtyResolution)
        {
            return false;
        }

        QueueDirtyAction(
            PendingDirtyActionKind.CloseWindow,
            () =>
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            });

        return true;
    }

    partial void OnDocumentChanged(MarkdownSource? value)
    {
        RefreshDocumentSummary();
        OnPropertyChanged(nameof(ShowsEditToggle));
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    partial void OnStateChanged(ViewState value)
    {
        OnPropertyChanged(nameof(HasDocumentTitle));
        OnPropertyChanged(nameof(ShowsReadingStatus));
        OnPropertyChanged(nameof(ShowsEditToggle));
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(EditToggleLabel));
        OnPropertyChanged(nameof(EditShortcutLabel));
        OnPropertyChanged(nameof(ShowsEditPencilIcon));
        OnPropertyChanged(nameof(ShowsReadEyeIcon));
        OnPropertyChanged(nameof(ShowsReadingStatus));
        OnPropertyChanged(nameof(ActiveDocumentContent));
        UpdateCommandStates();
    }

    partial void OnEditorSessionChanging(EditorSessionViewModel? oldValue, EditorSessionViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnEditorSessionPropertyChanged;
        }
    }

    partial void OnEditorSessionChanged(EditorSessionViewModel? value)
    {
        if (value is not null)
        {
            value.PropertyChanged += OnEditorSessionPropertyChanged;
            value.UpdateReadingPreferences(ReadingPreferences);
            _currentPath = value.CurrentPath;
        }

        RefreshDocumentSummary();
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    partial void OnReadingPreferencesChanged(ReadingPreferences value)
    {
        EditorSession?.UpdateReadingPreferences(value);

        OnPropertyChanged(nameof(SelectedFontFamilyMode));
        OnPropertyChanged(nameof(FontSizeSetting));
        OnPropertyChanged(nameof(LineHeightSetting));
        OnPropertyChanged(nameof(ContentWidthSetting));
        OnPropertyChanged(nameof(FontSizeLabel));
        OnPropertyChanged(nameof(LineHeightLabel));
        OnPropertyChanged(nameof(IsSerifFontSelected));
        OnPropertyChanged(nameof(IsSansFontSelected));
        OnPropertyChanged(nameof(IsMonoFontSelected));
        OnPropertyChanged(nameof(IsNarrowWidthSelected));
        OnPropertyChanged(nameof(IsMediumWidthSelected));
        OnPropertyChanged(nameof(IsWideWidthSelected));
    }

    private async Task OpenFileCoreAsync()
    {
        var path = await _filePicker.PickMarkdownFileAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        await LoadDocumentAsync(path, preserveEditModeAfterLoad: false).ConfigureAwait(true);
    }

    private Task CreateNewDocumentCoreAsync()
    {
        CreateNewDocumentCore();
        return Task.CompletedTask;
    }

    private void CreateNewDocumentCore()
    {
        Document = null;
        RenderedDocument = RenderedMarkdownDocument.Empty;
        _currentPath = null;
        State = ViewState.Viewing;
        ReadingProgress = 0;
        ErrorTitle = string.Empty;
        ErrorDetails = string.Empty;
        CloseOverlayCore();
        EditorSession = new EditorSessionViewModel(
            NewDocumentFileName,
            string.Empty,
            ReadingPreferences,
            _renderMarkdown,
            _imageSourceResolver);

        if (!_editorActivationMarked)
        {
            _editorActivationMarked = true;
            _startupMetrics.Mark(StartupStage.EditorActivation);
        }

        EditorSession.UpdateReadingPreferences(ReadingPreferences);
        EditorSession.SetStatusMessage(string.Empty);
        IsEditMode = true;
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private Task CloseFileCoreAsync()
    {
        CloseFileCore();
        return Task.CompletedTask;
    }

    private void CloseFileCore()
    {
        CloseOverlayCore();
        IsEditMode = false;
        EditorSession = null;
        Document = null;
        RenderedDocument = RenderedMarkdownDocument.Empty;
        _currentPath = null;
        State = ViewState.NoDocument;
        ReadingProgress = 0;
        ErrorTitle = string.Empty;
        ErrorDetails = string.Empty;
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private void EnterEditModeCore()
    {
        if (Document is null)
        {
            return;
        }

        if (EditorSession is null)
        {
            EditorSession = new EditorSessionViewModel(
                Document,
                ReadingPreferences,
                _renderMarkdown,
                _imageSourceResolver);
        }

        if (!_editorActivationMarked)
        {
            _editorActivationMarked = true;
            _startupMetrics.Mark(StartupStage.EditorActivation);
        }

        EditorSession.UpdateReadingPreferences(ReadingPreferences);
        EditorSession.SetStatusMessage(string.Empty);
        IsEditMode = true;
    }

    private Task ExitEditModeCoreAsync()
    {
        IsEditMode = false;
        EditorSession?.SetStatusMessage(string.Empty);
        return Task.CompletedTask;
    }

    private async Task LoadDocumentAsync(string path, bool preserveEditModeAfterLoad)
    {
        var result = await _openDocument.ExecuteAsync(path).ConfigureAwait(true);
        ApplyOpenResult(result, preserveEditModeAfterLoad);
    }

    private void ApplyOpenResult(OpenDocumentResult result, bool preserveEditModeAfterLoad)
    {
        switch (result)
        {
            case OpenDocumentResult.Success success:
                ApplyLoadedDocument(success.Source, preserveEditModeAfterLoad);
                break;

            case OpenDocumentResult.NotFound notFound:
                FailOpenResult("Couldn't find that file", notFound.Path);
                break;

            case OpenDocumentResult.AccessDenied denied:
                FailOpenResult("Access denied", denied.Path);
                break;

            case OpenDocumentResult.ReadError read:
                FailOpenResult("Couldn't read the file", $"{read.Path}\n\n{read.Message}");
                break;

            case OpenDocumentResult.UnsupportedType unsupported:
                FailOpenResult(
                    "Unsupported file type",
                    $"{unsupported.Path}\n\nSupported extensions: {string.Join(", ", SupportedDocumentTypes.Extensions)}");
                break;
        }
    }

    private void ApplyLoadedDocument(MarkdownSource source, bool preserveEditModeAfterLoad)
    {
        Document = source;
        RenderedDocument = _renderMarkdown.Execute(
            source.Content,
            baseDirectory: TryGetDirectory(source.Path));
        _currentPath = source.Path;
        State = ViewState.Viewing;
        ReadingProgress = 0;
        ErrorTitle = string.Empty;
        ErrorDetails = string.Empty;

        if (preserveEditModeAfterLoad)
        {
            if (EditorSession is null)
            {
                EditorSession = new EditorSessionViewModel(
                    source,
                    ReadingPreferences,
                    _renderMarkdown,
                    _imageSourceResolver);
            }
            else
            {
                EditorSession.ApplyLoadedDocument(source);
            }

            IsEditMode = true;
        }
        else
        {
            IsEditMode = false;
            EditorSession = null;
        }

        if (!_stage3Marked)
        {
            _stage3Marked = true;
            _startupMetrics.Mark(StartupStage.ReadableDocument);
        }

        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private void ApplySavedDocument(MarkdownSource source)
    {
        Document = source;
        RenderedDocument = _renderMarkdown.Execute(
            source.Content,
            baseDirectory: TryGetDirectory(source.Path));
        _currentPath = source.Path;

        if (EditorSession is null)
        {
            EditorSession = new EditorSessionViewModel(
                source,
                ReadingPreferences,
                _renderMarkdown,
                _imageSourceResolver);
        }
        else
        {
            EditorSession.ApplySavedDocument(source);
        }

        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private void FailOpenResult(string title, string details)
    {
        CloseOverlayCore();
        IsEditMode = false;
        EditorSession = null;
        ErrorTitle = title;
        ErrorDetails = details;
        State = ViewState.LoadError;
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private async Task RunWithDirtyCheckAsync(PendingDirtyActionKind kind, Func<Task> action)
    {
        if (IsDirtyPromptOpen)
        {
            return;
        }

        if (!RequiresDirtyResolution)
        {
            await action().ConfigureAwait(true);
            return;
        }

        QueueDirtyAction(kind, action);
    }

    private bool RequiresDirtyResolution => IsEditMode && EditorSession?.IsDirty == true;

    private void QueueDirtyAction(PendingDirtyActionKind kind, Func<Task> action)
    {
        if (IsDirtyPromptOpen)
        {
            return;
        }

        _pendingDirtyAction = action;
        DirtyPromptTitle = "Unsaved changes";
        DirtyPromptMessage = kind switch
        {
            PendingDirtyActionKind.OpenFile => "Save your changes before opening another document?",
            PendingDirtyActionKind.CreateNewDocument => "Save your changes before creating a new document?",
            PendingDirtyActionKind.CloseFile => "Save your changes before closing the current document?",
            PendingDirtyActionKind.Reload => "Save your changes before reloading the current document?",
            PendingDirtyActionKind.LeaveEditMode => "Save your changes before returning to reading mode?",
            PendingDirtyActionKind.CloseWindow => "Save your changes before closing MarkMello?",
            _ => "Save your changes before continuing?"
        };
        DirtyPromptErrorMessage = string.Empty;
        IsDirtyPromptOpen = true;
    }

    private async Task ContinuePendingDirtyActionAsync()
    {
        var pendingAction = _pendingDirtyAction;
        ClearDirtyPrompt();
        if (pendingAction is null)
        {
            return;
        }

        await pendingAction().ConfigureAwait(true);
    }

    private void ClearDirtyPrompt()
    {
        _pendingDirtyAction = null;
        IsDirtyPromptOpen = false;
        DirtyPromptTitle = string.Empty;
        DirtyPromptMessage = string.Empty;
        DirtyPromptErrorMessage = string.Empty;
    }

    private async Task<SaveExecutionOutcome> SaveEditorAsync(bool promptForPathWhenMissing, bool forceSaveAs)
    {
        if (EditorSession is null)
        {
            return new SaveExecutionOutcome(false, new SaveDocumentResult.InvalidPath(string.Empty));
        }

        var targetPath = forceSaveAs ? null : EditorSession.CurrentPath;
        if (string.IsNullOrWhiteSpace(targetPath) && promptForPathWhenMissing)
        {
            targetPath = await PickSavePathAsync(EditorSession.FileName).ConfigureAwait(true);
        }
        else if (forceSaveAs)
        {
            targetPath = await PickSavePathAsync(EditorSession.FileName).ConfigureAwait(true);
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return new SaveExecutionOutcome(true, null);
        }

        var result = await _saveDocument.ExecuteAsync(targetPath, EditorSession.SourceText).ConfigureAwait(true);
        return new SaveExecutionOutcome(false, result);
    }

    private async Task<string?> PickSavePathAsync(string? currentFileName)
    {
        var suggestedFileName = NormalizeSuggestedFileName(currentFileName);
        return await _filePicker.PickSaveMarkdownFileAsync(suggestedFileName).ConfigureAwait(true);
    }

    private void DiscardEditorChanges()
    {
        if (EditorSession is null)
        {
            return;
        }

        EditorSession.DiscardChanges();
        RefreshDocumentSummary();
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private void ApplyTheme(ThemeMode mode)
    {
        Theme = mode;
        _themeService.Apply(mode);
        EffectiveTheme = _themeService.GetEffectiveTheme();
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
                // Persistence remains best-effort; failed saving of reading
                // preferences must never interrupt the viewer or editor loop.
            }
        });
    }

    private void OnEditorSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (EditorSession is null)
        {
            return;
        }

        if (e.PropertyName == nameof(EditorSessionViewModel.CurrentPath))
        {
            _currentPath = EditorSession.CurrentPath;
        }

        if (e.PropertyName is nameof(EditorSessionViewModel.SourceText)
            or nameof(EditorSessionViewModel.LastPersistedSource)
            or nameof(EditorSessionViewModel.FileName)
            or nameof(EditorSessionViewModel.CurrentPath))
        {
            RefreshDocumentSummary();
            RefreshWindowTitle();
            UpdateCommandStates();
        }
    }

    private void RefreshDocumentSummary()
    {
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(TitleFileDisplayName));
        OnPropertyChanged(nameof(HasDocumentTitle));
        OnPropertyChanged(nameof(WordCount));
        OnPropertyChanged(nameof(ReadTimeMinutes));
        OnPropertyChanged(nameof(IsDirty));
    }

    private void RefreshWindowTitle()
    {
        if (State != ViewState.Viewing)
        {
            WindowTitle = "MarkMello";
            return;
        }

        WindowTitle = string.IsNullOrWhiteSpace(FileName)
            ? "MarkMello"
            : $"{TitleFileDisplayName} — MarkMello";
    }

    private void UpdateCommandStates()
    {
        ReloadCommand.NotifyCanExecuteChanged();
        CloseFileCommand.NotifyCanExecuteChanged();
        ToggleEditModeCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    private static string NormalizeSuggestedFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Untitled.md";
        }

        return SupportedDocumentTypes.IsSupportedPath(fileName)
            ? fileName
            : $"{fileName}.md";
    }

    private void CloseOverlayCore()
    {
        ShellOverlay = ShellOverlayKind.None;
    }

    private string? CurrentDocumentPath => EditorSession?.CurrentPath ?? _currentPath ?? Document?.Path;

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

    private static string GetSaveFailureMessage(SaveDocumentResult? result)
        => result switch
        {
            SaveDocumentResult.InvalidPath invalidPath => $"Couldn't save to this path: {invalidPath.Path}",
            SaveDocumentResult.AccessDenied accessDenied => $"Access denied: {accessDenied.Path}",
            SaveDocumentResult.WriteError writeError => $"Couldn't save the document: {writeError.Message}",
            _ => "Couldn't save the document."
        };

    private static string? TryGetDirectory(string? path)
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

    private enum PendingDirtyActionKind
    {
        OpenFile,
        CreateNewDocument,
        CloseFile,
        Reload,
        LeaveEditMode,
        CloseWindow
    }

    private readonly record struct SaveExecutionOutcome(bool Cancelled, SaveDocumentResult? Result);
}
