using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;
using MarkMello.Presentation.Editing;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.Services;
using System.Reflection;
using System.ComponentModel;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// View model главного окна. Отвечает за state machine (NoDocument/Viewing/LoadError),
/// тему, reading preferences, команды open/reload, lazy edit mode и dirty/save flow.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly OpenDocumentUseCase _openDocument;
    private readonly SaveDocumentUseCase _saveDocument;
    private readonly IFilePicker _filePicker;
    private readonly ICommandLineActivation _commandLine;
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themeService;
    private readonly IStartupMetrics _startupMetrics;
    private readonly RenderMarkdownDocumentUseCase _renderMarkdown;
    private readonly OpenFolderUseCase _openFolder;
    private readonly ExpandFolderNodeUseCase _expandFolderNode;
    private readonly SearchWorkspaceFilesUseCase _searchWorkspaceFiles;
    private readonly WorkspaceFileOperationsUseCase _fileOperations;
    private readonly IPlatformServices _platform;
    private readonly Func<IWorkspaceWatcher> _watcherFactory;
    private readonly IWindowLauncher _windowLauncher;

    /// <summary>
    /// Проверка существования пути при восстановлении сессии. Отдельно от файловой системы
    /// дерева, потому что нужна и без открытой папки; в тестах подменяется.
    /// </summary>
    private readonly Func<string, bool> _fileExists;
    private readonly IImageSourceResolver? _imageSourceResolver;
    private readonly Func<IEditorPreviewScheduler>? _previewSchedulerFactory;

    private bool _documentModelReadyMarked;
    private bool _readableDocumentMarked;
    private bool _secondaryFeaturesMarked;
    private bool _editorActivationMarked;
    private string? _currentPath;
    private Func<Task>? _pendingDirtyAction;
    private readonly bool _showCustomTitleBar = OperatingSystem.IsWindows();
    private readonly string _aboutVersion;
    private readonly string _aboutLicense = "GPLv3";
    private ReadingPreferences _documentReadingPreferences = GetDocumentRenderingPreferences(ReadingPreferences.Default);
    private WindowBorderMode _windowBorderMode = WindowBorderMode.Auto;
    private bool _isWindowBorderLoaded;

    public event EventHandler? CloseRequested;

    public ShellViewModel(
        OpenDocumentUseCase openDocument,
        SaveDocumentUseCase saveDocument,
        IFilePicker filePicker,
        ICommandLineActivation commandLine,
        ILocalizationService localization,
        ISettingsStore settings,
        IThemeService themeService,
        IStartupMetrics startupMetrics,
        RenderMarkdownDocumentUseCase renderMarkdown,
        IUpdateService updateService,
        OpenFolderUseCase openFolder,
        ExpandFolderNodeUseCase expandFolderNode,
        SearchWorkspaceFilesUseCase searchWorkspaceFiles,
        WorkspaceFileOperationsUseCase fileOperations,
        IPlatformServices platform,
        Func<IWorkspaceWatcher> watcherFactory,
        IWindowLauncher windowLauncher,
        Func<string, bool>? fileExists = null,
        IImageSourceResolver? imageSourceResolver = null,
        Func<IEditorPreviewScheduler>? previewSchedulerFactory = null,
        DeferredUpdateCheck? deferredUpdateCheck = null)
    {
        _openDocument = openDocument;
        _saveDocument = saveDocument;
        _filePicker = filePicker;
        _commandLine = commandLine;
        _localization = localization;
        _settings = settings;
        _themeService = themeService;
        _startupMetrics = startupMetrics;
        _renderMarkdown = renderMarkdown;
        _updateService = updateService;
        _deferredUpdateCheck = deferredUpdateCheck;
        _openFolder = openFolder;
        _expandFolderNode = expandFolderNode;
        _searchWorkspaceFiles = searchWorkspaceFiles;
        _fileOperations = fileOperations;
        _platform = platform;
        _watcherFactory = watcherFactory;
        _windowLauncher = windowLauncher;
        _fileExists = fileExists ?? (static path => File.Exists(path) || Directory.Exists(path));
        _imageSourceResolver = imageSourceResolver;
        _previewSchedulerFactory = previewSchedulerFactory;
        _aboutVersion = GetProductVersion();
        InitializeOpenDocuments();
        _localization.PropertyChanged += OnLocalizationChanged;
        _commandLine.FileActivated += OnFileActivated;
        RefreshUpdateStatusTexts();
    }

    /// <summary>
    /// Handler for runtime «open this file» signals emitted by the platform
    /// after startup. On macOS Finder sends an Apple Event to the already-
    /// running process; cold-start activations come back through
    /// <see cref="ICommandLineActivation.GetActivationFilePath"/> instead.
    /// </summary>
    private async void OnFileActivated(object? sender, FileActivationEventArgs e)
    {
        try
        {
            await OpenPathAsync(e.Path).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // The open use-case already surfaces user-visible errors via
            // the view-state machine; the event handler must not throw
            // back into Avalonia's lifetime dispatch loop.
        }
    }

    public IImageSourceResolver? ImageSourceResolver => _imageSourceResolver;

    public string this[string key] => _localization[key];

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
    [NotifyPropertyChangedFor(nameof(IsAppAboutOpen))]
    [NotifyPropertyChangedFor(nameof(IsAppOverlayOpen))]
    [NotifyPropertyChangedFor(nameof(HasOpenOverlay))]
    [NotifyPropertyChangedFor(nameof(AppMenuOverlayContent))]
    [NotifyPropertyChangedFor(nameof(AppSettingsOverlayContent))]
    [NotifyPropertyChangedFor(nameof(AppAboutOverlayContent))]
    [NotifyPropertyChangedFor(nameof(ReadingSettingsOverlayContent))]
    private ShellOverlayKind _shellOverlay = ShellOverlayKind.None;

    [ObservableProperty]
    private double _readingProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FindResultLabel))]
    private bool _isFindBarOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FindResultLabel))]
    private string _findQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FindResultLabel))]
    private int _findMatchIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FindResultLabel))]
    private int _findMatchCount;

    [ObservableProperty]
    private ThemeMode _theme = ThemeMode.System;

    [ObservableProperty]
    private ReadingPreferences _readingPreferences = ReadingPreferences.Default;

    private bool _alwaysOpenDocumentsInEditMode;

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
    [NotifyPropertyChangedFor(nameof(ShowsAppMenuControl))]
    [NotifyPropertyChangedFor(nameof(IsAppMenuOpen))]
    [NotifyPropertyChangedFor(nameof(IsAppSettingsOpen))]
    [NotifyPropertyChangedFor(nameof(IsAppAboutOpen))]
    [NotifyPropertyChangedFor(nameof(IsAppOverlayOpen))]
    [NotifyPropertyChangedFor(nameof(HasOpenOverlay))]
    [NotifyPropertyChangedFor(nameof(AppMenuOverlayContent))]
    [NotifyPropertyChangedFor(nameof(AppSettingsOverlayContent))]
    [NotifyPropertyChangedFor(nameof(AppAboutOverlayContent))]
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

    public bool IsWelcome => State == ViewState.NoDocument && !ShowsSidebar;

    public bool IsViewer => State == ViewState.Viewing;

    public bool IsError => State == ViewState.LoadError;

    public bool IsDirty => EditorSession?.IsDirty == true;

    public bool ShowCustomTitleBar => _showCustomTitleBar;

    public bool IsSettingsOpen => ShellOverlay == ShellOverlayKind.ReadingSettings;

    public bool ShowsAppMenuControl => !IsEditMode;

    public bool IsAppMenuOpen => ShowsAppMenuControl && ShellOverlay == ShellOverlayKind.AppMenu;

    public bool IsAppSettingsOpen => ShellOverlay == ShellOverlayKind.AppSettings;

    public bool IsAppAboutOpen => ShowsAppMenuControl && ShellOverlay == ShellOverlayKind.AppAbout;

    public bool IsAppOverlayOpen => IsAppSettingsOpen || (ShowsAppMenuControl
        && ShellOverlay is ShellOverlayKind.AppMenu or ShellOverlayKind.AppAbout);

    public bool HasOpenOverlay => IsSettingsOpen || IsAppOverlayOpen;

    public object? AppMenuOverlayContent => IsAppMenuOpen ? this : null;

    public object? AppSettingsOverlayContent => IsAppSettingsOpen ? this : null;

    public object? AppAboutOverlayContent => IsAppAboutOpen ? this : null;

    public object? ReadingSettingsOverlayContent => IsSettingsOpen && IsViewer ? this : null;

    public bool ShowsReadingStatus => IsViewer && !IsEditMode;

    public string FindResultLabel
    {
        get
        {
            if (FindMatchCount == 0)
            {
                return FindQuery.Length > 0
                    ? _localization["FindNoResults"]
                    : _localization.Format("FindResultCount", 0, 0);
            }

            return _localization.Format("FindResultCount", FindMatchIndex + 1, FindMatchCount);
        }
    }

    public bool ShowsMoonThemeIcon => EffectiveTheme == ThemeMode.Light;

    public bool ShowsSunThemeIcon => EffectiveTheme == ThemeMode.Dark;

    public ReadingPreferences DocumentReadingPreferences => _documentReadingPreferences;

    public bool ShowsEditPencilIcon => !IsEditMode;

    public bool ShowsReadEyeIcon => IsEditMode;

    public bool ShowsEditToggle => State == ViewState.Viewing && Document is not null;

    public bool AlwaysOpenDocumentsInEditMode
    {
        get => _alwaysOpenDocumentsInEditMode;
        set
        {
            if (!SetProperty(ref _alwaysOpenDocumentsInEditMode, value))
            {
                return;
            }

            PersistAlwaysOpenDocumentsInEditMode(value);
        }
    }

    public string EditToggleLabel => IsEditMode ? _localization["ModeReading"] : _localization["ModeEdit"];

    public string EditShortcutLabel => IsEditMode ? _localization["ModeReadShortcut"] : _localization["ModeEditShortcut"];

    public string AboutVersion => _aboutVersion;

    public string AboutLicense => _aboutLicense;

    public bool HasDirtyPromptError => !string.IsNullOrWhiteSpace(DirtyPromptErrorMessage);

    /// <summary>
    /// Рамка окна. Хранится отдельно от <see cref="ReadingPreferences"/>: это
    /// настройка оболочки, а не чтения документа.
    /// </summary>
    public WindowBorderMode WindowBorderMode
    {
        get => _windowBorderMode;
        set
        {
            if (_windowBorderMode == value)
            {
                return;
            }

            _windowBorderMode = value;
            OnPropertyChanged();
            RaiseWindowBorderSelectionChanged();

            if (_isWindowBorderLoaded)
            {
                _ = _settings.SaveWindowBorderModeAsync(value).AsTask();
            }
        }
    }

    public bool IsWindowBorderAutoSelected
    {
        get => WindowBorderMode == WindowBorderMode.Auto;
        set => SelectWindowBorderMode(value, WindowBorderMode.Auto, nameof(IsWindowBorderAutoSelected));
    }

    public bool IsWindowBorderOnSelected
    {
        get => WindowBorderMode == WindowBorderMode.On;
        set => SelectWindowBorderMode(value, WindowBorderMode.On, nameof(IsWindowBorderOnSelected));
    }

    public bool IsWindowBorderOffSelected
    {
        get => WindowBorderMode == WindowBorderMode.Off;
        set => SelectWindowBorderMode(value, WindowBorderMode.Off, nameof(IsWindowBorderOffSelected));
    }

    private void SelectWindowBorderMode(bool isChecked, WindowBorderMode mode, string propertyName)
    {
        if (!isChecked)
        {
            // Unchecking the active segment would leave the group with no
            // selection; the segmented control only ever moves between options.
            OnPropertyChanged(propertyName);
            return;
        }

        WindowBorderMode = mode;
    }

    private void RaiseWindowBorderSelectionChanged()
    {
        OnPropertyChanged(nameof(IsWindowBorderAutoSelected));
        OnPropertyChanged(nameof(IsWindowBorderOnSelected));
        OnPropertyChanged(nameof(IsWindowBorderOffSelected));
    }

    public DocumentMinimapMode SelectedDocumentMinimapMode
    {
        get => ReadingPreferences.DocumentMinimapMode;
        set
        {
            if (ReadingPreferences.DocumentMinimapMode == value)
            {
                return;
            }

            ApplyReadingPreferences(ReadingPreferences with { DocumentMinimapMode = value });
        }
    }

    public bool IsDocumentMinimapAutoSelected
    {
        get => ReadingPreferences.DocumentMinimapMode == DocumentMinimapMode.Auto;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsDocumentMinimapAutoSelected));
                return;
            }

            SelectedDocumentMinimapMode = DocumentMinimapMode.Auto;
        }
    }

    public bool IsDocumentMinimapOnSelected
    {
        get => ReadingPreferences.DocumentMinimapMode == DocumentMinimapMode.On;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsDocumentMinimapOnSelected));
                return;
            }

            SelectedDocumentMinimapMode = DocumentMinimapMode.On;
        }
    }

    public bool IsDocumentMinimapOffSelected
    {
        get => ReadingPreferences.DocumentMinimapMode == DocumentMinimapMode.Off;
        set
        {
            if (!value)
            {
                OnPropertyChanged(nameof(IsDocumentMinimapOffSelected));
                return;
            }

            SelectedDocumentMinimapMode = DocumentMinimapMode.Off;
        }
    }

    public int WordCount => EditorSession?.WordCount ?? CountWords(Document?.Content);

    public int ReadTimeMinutes => Math.Max(1, (int)Math.Round(WordCount / 220.0));

    public string NextThemeHint => EffectiveTheme == ThemeMode.Light
        ? _localization["ThemeSwitchToDark"]
        : _localization["ThemeSwitchToLight"];

    private bool _suppressStartupActivation;

    /// <summary>
    /// Окно открыто не запуском процесса, а из уже работающего приложения:
    /// стартовые аргументы к нему не относятся.
    /// </summary>
    public void SuppressStartupActivation() => _suppressStartupActivation = true;

    public async Task InitializeAsync()
    {
        ReadingPreferences = await _settings.LoadPreferencesAsync().ConfigureAwait(true);

        _alwaysOpenDocumentsInEditMode = await _settings
            .LoadAlwaysOpenDocumentsInEditModeAsync()
            .ConfigureAwait(true);
        OnPropertyChanged(nameof(AlwaysOpenDocumentsInEditMode));

        var savedLanguage = await _settings.LoadLanguageAsync().ConfigureAwait(true);
        ApplyLanguageSelection(savedLanguage, persist: false);

        var savedTheme = await _settings.LoadThemeAsync().ConfigureAwait(true);
        ApplyTheme(savedTheme);

        WindowBorderMode = await _settings.LoadWindowBorderModeAsync().ConfigureAwait(true);
        _isWindowBorderLoaded = true;

        // Аргументы командной строки принадлежат запуску процесса, а не каждому окну:
        // второе окно получает свою папку от launcher'а и стартовую активацию пропускает.
        if (_suppressStartupActivation)
        {
            return;
        }

        // Каталог в аргументах открывает папку, файл — документ. Порядок важен:
        // «MarkMello docs notes.md» должен показать и дерево, и запрошенный документ.
        var folderPath = _commandLine.GetActivationFolderPath();
        if (!string.IsNullOrEmpty(folderPath))
        {
            await OpenFolderPathAsync(folderPath).ConfigureAwait(true);
        }

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

        SetDirtyPromptError(null);

        var outcome = await SaveEditorAsync(promptForPathWhenMissing: true, forceSaveAs: false).ConfigureAwait(true);
        if (outcome.Cancelled)
        {
            return;
        }

        if (outcome.Result is not SaveDocumentResult.Success success)
        {
            SetDirtyPromptError(outcome.Result);
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
        MarkSecondaryFeaturesReady();

        IsFindBarOpen = false;
        ShellOverlay = IsSettingsOpen
            ? ShellOverlayKind.None
            : ShellOverlayKind.ReadingSettings;
    }

    [RelayCommand]
    private void ToggleFindBar()
    {
        if (IsFindBarOpen)
        {
            IsFindBarOpen = false;
            return;
        }

        CloseOverlayCore();
        IsFindBarOpen = true;
    }

    [RelayCommand]
    private void CloseFindBar()
    {
        IsFindBarOpen = false;
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
        if (!ShowsAppMenuControl)
        {
            CloseAppOverlayCore();
            return;
        }

        MarkSecondaryFeaturesReady();

        IsFindBarOpen = false;
        ShellOverlay = IsAppOverlayOpen
            ? ShellOverlayKind.None
            : ShellOverlayKind.AppMenu;
    }

    [RelayCommand]
    private void OpenAppSettings()
    {
        if (!ShowsAppMenuControl)
        {
            CloseAppOverlayCore();
            return;
        }

        MarkSecondaryFeaturesReady();

        IsFindBarOpen = false;
        ShellOverlay = ShellOverlayKind.AppSettings;
    }

    [RelayCommand]
    private void OpenAbout()
    {
        if (!ShowsAppMenuControl)
        {
            CloseAppOverlayCore();
            return;
        }

        MarkSecondaryFeaturesReady();

        IsFindBarOpen = false;
        ShellOverlay = ShellOverlayKind.AppAbout;
    }

    [RelayCommand]
    private void ReturnToAppMenu()
    {
        if (!ShowsAppMenuControl)
        {
            CloseAppOverlayCore();
            return;
        }

        MarkSecondaryFeaturesReady();

        ShellOverlay = ShellOverlayKind.AppMenu;
    }

    [RelayCommand]
    private void ReturnToAppSettings()
    {
        if (!ShowsAppMenuControl)
        {
            CloseAppOverlayCore();
            return;
        }

        MarkSecondaryFeaturesReady();

        ShellOverlay = ShellOverlayKind.AppSettings;
    }

    [RelayCommand]
    private void CloseOverlay()
    {
        CloseOverlayCore();
    }

    [RelayCommand]
    private void ClearError()
    {
        if (IsFindBarOpen)
        {
            IsFindBarOpen = false;
            return;
        }

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
            ClearLoadError();
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

        // Грязной может быть любая вкладка, а не только активная: показываем её пользователю
        // и спрашиваем про неё, после разрешения запрос на закрытие повторяется — так окно
        // проходит по всем несохранённым вкладкам по очереди.
        if (FindFirstDirtyTab() is not { } dirtyTab)
        {
            return false;
        }

        if (!ReferenceEquals(OpenDocuments.ActiveTab, dirtyTab))
        {
            _ = RestoreTabAsync(dirtyTab);
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
        if (value != ViewState.Viewing)
        {
            IsFindBarOpen = false;
        }

        OnPropertyChanged(nameof(HasDocumentTitle));
        OnPropertyChanged(nameof(ShowsReadingStatus));
        OnPropertyChanged(nameof(ShowsEditToggle));
        OnPropertyChanged(nameof(ReadingSettingsOverlayContent));

        // Пустое состояние зависит от State, а вкладка регистрируется до перехода
        // в Viewing — без этого уведомления заглушка оставалась поверх документа.
        OnPropertyChanged(nameof(IsEmptyDocumentSurface));
        OnPropertyChanged(nameof(IsWelcome));

        RefreshWindowTitle();
        UpdateCommandStates();
    }

    partial void OnIsEditModeChanged(bool value)
    {
        SyncActiveTabEditorState();

        IsFindBarOpen = false;

        if (value)
        {
            CloseAppOverlayCore();
        }

        OnPropertyChanged(nameof(EditToggleLabel));
        OnPropertyChanged(nameof(EditShortcutLabel));
        OnPropertyChanged(nameof(ShowsEditPencilIcon));
        OnPropertyChanged(nameof(ShowsReadEyeIcon));
        OnPropertyChanged(nameof(ShowsReadingStatus));
        OnPropertyChanged(nameof(ShowsAppMenuControl));
        OnPropertyChanged(nameof(ShowsFloatingAppMenuButton));
        OnPropertyChanged(nameof(IsAppMenuOpen));
        OnPropertyChanged(nameof(IsAppSettingsOpen));
        OnPropertyChanged(nameof(IsAppAboutOpen));
        OnPropertyChanged(nameof(IsAppOverlayOpen));
        OnPropertyChanged(nameof(HasOpenOverlay));
        OnPropertyChanged(nameof(AppMenuOverlayContent));
        OnPropertyChanged(nameof(AppSettingsOverlayContent));
        OnPropertyChanged(nameof(AppAboutOverlayContent));
        OnPropertyChanged(nameof(ActiveDocumentContent));
        UpdateCommandStates();
    }

    partial void OnEditorSessionChanging(EditorSessionViewModel? oldValue, EditorSessionViewModel? newValue)
    {
        if (oldValue is null)
        {
            return;
        }

        oldValue.PropertyChanged -= OnEditorSessionPropertyChanged;

        // Сессия принадлежит вкладке: выбрасываем её только если вкладка её больше не держит.
        // Иначе переключение вкладок убивало бы несохранённые правки соседней.
        if (!OpenDocuments.Tabs.Any(tab => ReferenceEquals(tab.EditorSession, oldValue)))
        {
            oldValue.Dispose();
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

        SyncActiveTabEditorState();

        RefreshDocumentSummary();
        RefreshWindowTitle();
        UpdateCommandStates();
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
        ClearLoadError();
        CloseOverlayCore();
        EditorSession = new EditorSessionViewModel(
            GetUntitledFileName(),
            string.Empty,
            ReadingPreferences,
            _renderMarkdown,
            _imageSourceResolver,
            _localization,
            CreatePreviewScheduler());

        if (!_editorActivationMarked)
        {
            _editorActivationMarked = true;
            _startupMetrics.Mark(StartupStage.EditorActivation);
        }

        EditorSession.UpdateReadingPreferences(ReadingPreferences);
        EditorSession.SetStatusMessage(string.Empty);
        IsEditMode = true;
        TrackNewDocumentTab();
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private async Task CloseFileCoreAsync()
    {
        CloseOverlayCore();

        if (OpenDocuments.ActiveTab is { } tab)
        {
            await RemoveTabAsync(tab).ConfigureAwait(true);
            return;
        }

        CloseFileCore();
    }

    /// <summary>
    /// Полная очистка document surface вместе со всеми вкладками.
    /// Используется там, где уходит весь контекст: закрытие папки, фатальная ошибка.
    /// </summary>
    private void CloseFileCore()
    {
        CloseOverlayCore();
        OpenDocuments.Activate(null);
        OpenDocuments.Tabs.Clear();
        IsEditMode = false;
        EditorSession = null;
        Document = null;
        RenderedDocument = RenderedMarkdownDocument.Empty;
        _currentPath = null;
        State = ViewState.NoDocument;
        ReadingProgress = 0;
        ClearLoadError();
        SyncWorkspaceActiveDocument();
        RefreshWindowTitle();
        UpdateCommandStates();
        RefreshTabState();
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
                _imageSourceResolver,
                _localization,
                CreatePreviewScheduler());
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

            case OpenDocumentResult.NotFound:
            case OpenDocumentResult.AccessDenied:
            case OpenDocumentResult.ReadError:
            case OpenDocumentResult.UnsupportedType:
                FailOpenResult(result);
                break;
        }
    }

    private void ApplyLoadedDocument(MarkdownSource source, bool preserveEditModeAfterLoad)
    {
        var rendered = _renderMarkdown.Execute(
            source.Content,
            baseDirectory: TryGetDirectory(source.Path));

        // Вкладку переключаем до того, как трогаем EditorSession: иначе сброс сессии
        // прилетит в предыдущую вкладку и заберёт с собой её несохранённые правки.
        var loadedTab = TrackLoadedDocumentTab(source, rendered);

        Document = source;
        RenderedDocument = rendered;
        _currentPath = source.Path;
        State = ViewState.Viewing;
        ReadingProgress = 0;
        ClearLoadError();

        if (preserveEditModeAfterLoad || AlwaysOpenDocumentsInEditMode)
        {
            if (loadedTab.EditorSession is null)
            {
                EditorSession = new EditorSessionViewModel(
                    source,
                    ReadingPreferences,
                    _renderMarkdown,
                    _imageSourceResolver,
                    _localization,
                    CreatePreviewScheduler());
            }
            else
            {
                loadedTab.EditorSession.ApplyLoadedDocument(source);
                EditorSession = loadedTab.EditorSession;
            }

            IsEditMode = true;

            if (!_editorActivationMarked)
            {
                _editorActivationMarked = true;
                _startupMetrics.Mark(StartupStage.EditorActivation);
            }
        }
        else
        {
            IsEditMode = false;
            EditorSession = null;
        }

        if (!_documentModelReadyMarked)
        {
            _documentModelReadyMarked = true;
            _startupMetrics.Mark(StartupStage.DocumentModelReady);
        }

        SyncWorkspaceActiveDocument();
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private void PersistAlwaysOpenDocumentsInEditMode(bool value)
    {
        try
        {
            _settings.SaveAlwaysOpenDocumentsInEditModeAsync(value).AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Settings persistence is best-effort and must not interrupt editing.
        }
    }

    private void MarkSecondaryFeaturesReady()
    {
        if (_secondaryFeaturesMarked)
        {
            return;
        }

        _secondaryFeaturesMarked = true;
        _startupMetrics.Mark(StartupStage.SecondaryFeatures);
    }

    public void MarkReadableDocumentRendered()
    {
        if (_readableDocumentMarked || State != ViewState.Viewing || RenderedDocument.Blocks.Count == 0)
        {
            return;
        }

        _readableDocumentMarked = true;
        _startupMetrics.Mark(StartupStage.ReadableDocument);
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
                _imageSourceResolver,
                _localization,
                CreatePreviewScheduler());
        }
        else
        {
            EditorSession.ApplySavedDocument(source);
        }

        RetargetActiveTab(source);
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    private void FailOpenResult(OpenDocumentResult result)
    {
        CloseOverlayCore();
        IsEditMode = false;
        EditorSession = null;
        SetLoadError(result);
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

    /// <summary>
    /// Каждая editor-сессия получает собственный планировщик preview: отложенный
    /// рендер прошлой сессии не должен долетать до новой. Без фабрики (unit-тесты)
    /// сессия работает синхронно.
    /// </summary>
    private IEditorPreviewScheduler? CreatePreviewScheduler()
        => _previewSchedulerFactory?.Invoke();

    private void QueueDirtyAction(PendingDirtyActionKind kind, Func<Task> action)
    {
        if (IsDirtyPromptOpen)
        {
            return;
        }

        _pendingDirtyAction = action;
        SetDirtyPrompt(kind);
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
        ClearDirtyPromptState();
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
        => await _filePicker
            .PickSaveMarkdownFileAsync(NormalizeSuggestedFileName(currentFileName))
            .ConfigureAwait(true);

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
        OnPropertyChanged(nameof(WordCountStatusLabel));
        OnPropertyChanged(nameof(ReadTimeStatusLabel));
        OnPropertyChanged(nameof(IsDirty));
        SyncActiveTabDirtyState();
    }

    private void RefreshWindowTitle()
    {
        var folderSegment = Workspace is { } workspace
            ? $"{workspace.RootDisplayName} — "
            : string.Empty;

        if (State != ViewState.Viewing || string.IsNullOrWhiteSpace(FileName))
        {
            WindowTitle = $"{folderSegment}MarkMello";
            return;
        }

        WindowTitle = $"{TitleFileDisplayName} — {folderSegment}MarkMello";
    }

    private void UpdateCommandStates()
    {
        ReloadCommand.NotifyCanExecuteChanged();
        CloseFileCommand.NotifyCanExecuteChanged();
        ToggleEditModeCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        RefreshUpdateCommandStates();
    }

    private static string GetProductVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ShellViewModel).Assembly;

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var buildMetadataIndex = informationalVersion.IndexOf('+');
            return buildMetadataIndex >= 0
                ? informationalVersion[..buildMetadataIndex]
                : informationalVersion;
        }

        var version = assembly.GetName().Version;
        return version is null
            ? "1.0.0"
            : $"{version.Major}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}";
    }

    private void CloseOverlayCore()
    {
        ShellOverlay = ShellOverlayKind.None;
    }

    private void CloseAppOverlayCore()
    {
        if (ShellOverlay is ShellOverlayKind.AppMenu or ShellOverlayKind.AppSettings or ShellOverlayKind.AppAbout)
        {
            ShellOverlay = ShellOverlayKind.None;
        }
    }

    /// <summary>Путь активного документа. Публичен: по нему дерево подсвечивает строку.</summary>
    public string? CurrentDocumentPath => EditorSession?.CurrentPath ?? _currentPath ?? Document?.Path;

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
