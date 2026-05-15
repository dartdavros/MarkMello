using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

public partial class MainWindow : Window
{
    private const double DefaultWindowWidth = 1280;
    private const double DefaultWindowHeight = 840;
    private const int WindowPlacementMarginPixels = 8;

    private readonly MainWindowViewModel _viewModel = default!;
    private readonly StartupSmokeTestOptions _startupSmokeTestOptions = StartupSmokeTestOptions.Disabled;
    private readonly ISettingsStore? _settings;
    private readonly Task _startupInitializationTask = Task.CompletedTask;
    private WindowPlacement? _lastNormalWindowPlacement;
    private bool _allowConfirmedClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        StartupSmokeTestOptions startupSmokeTestOptions,
        ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(startupSmokeTestOptions);
        ArgumentNullException.ThrowIfNull(settings);

        _viewModel = viewModel;
        _startupSmokeTestOptions = startupSmokeTestOptions;
        _settings = settings;
        DataContext = viewModel;

        ConfigurePlatformChrome();
        InitializeComponent();
        ApplyStartupWindowPlacement();
        SyncOverlayWindowClasses();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
        SizeChanged += OnWindowSizeChanged;
        PositionChanged += OnWindowPositionChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CloseRequested += OnViewModelCloseRequested;

        _startupInitializationTask = InitializeStartupAsync();
    }

    /// <summary>
    /// Платформенные правила для Avalonia 12:
    /// - Windows: extended client area + BorderOnly, чтобы оставить resize border
    ///   и отрисовывать собственную title bar область из XAML.
    /// - macOS: сохраняем системные decorations, но расширяем client area под наш layout.
    ///   BorderOnly/None в 12.0.x для macOS пока проблемны по drag behavior.
    /// - Linux: native chrome (вариативность WM, не лезем).
    /// </summary>
    private void ConfigurePlatformChrome()
    {
        if (OperatingSystem.IsWindows())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 36;
            WindowDecorations = global::Avalonia.Controls.WindowDecorations.BorderOnly;
        }
        else if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 36;
            WindowDecorations = global::Avalonia.Controls.WindowDecorations.Full;
        }
        // Linux: ничего не переопределяем — пусть WM рисует свой chrome.
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        await _startupInitializationTask.ConfigureAwait(true);
        await CompleteStartupSmokeTestAsync().ConfigureAwait(true);
    }

    private async Task InitializeStartupAsync()
    {
        try
        {
            await _viewModel.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (_startupSmokeTestOptions.IsEnabled)
        {
            Console.Error.WriteLine(exception);
            ShutdownClassicDesktopLifetime(exitCode: 1);
        }
        catch
        {
            // Защита fast path: VM init не должна валить окно.
            // Реальный logging придёт вместе с infrastructure logging в M4+.
        }
    }

    private async Task CompleteStartupSmokeTestAsync()
    {
        if (!_startupSmokeTestOptions.IsEnabled)
        {
            return;
        }

        await Task.Delay(_startupSmokeTestOptions.ExitAfterOpenDelay).ConfigureAwait(true);
        ShutdownClassicDesktopLifetime(exitCode: 0);
    }

    internal static bool IsOverlayPopupInteractionSource(Visual source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is ComboBox or ComboBoxItem)
            {
                return true;
            }

            if (string.Equals(current.GetType().Name, "PopupRoot", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ShutdownClassicDesktopLifetime(int exitCode)
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
            return;
        }

        Environment.ExitCode = exitCode;
    }

    protected override void OnClosed(EventArgs e)
    {
        Closing -= OnWindowClosing;
        SizeChanged -= OnWindowSizeChanged;
        PositionChanged -= OnWindowPositionChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CloseRequested -= OnViewModelCloseRequested;
        base.OnClosed(e);
    }

    // ---------- Window control buttons (Windows only path) ----------

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_viewModel.ShowCustomTitleBar)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Double-click on the custom title bar toggles Maximized/Normal,
        // mirroring native Windows chrome behaviour.
        if (e.ClickCount == 2)
        {
            if (CanResize)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }

            e.Handled = true;
            return;
        }

        if (e.ClickCount != 1)
        {
            return;
        }

        try
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
        catch
        {
            // На неподдерживаемых платформах/состояниях окно просто не начнёт drag.
        }
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel.IsDirtyPromptOpen)
        {
            return;
        }

        if (!_viewModel.HasOpenOverlay || e.Source is not Visual source)
        {
            return;
        }

        if (IsPointerWithinOpenOverlay(source) || IsOverlayPopupInteractionSource(source))
        {
            return;
        }

        _viewModel.CloseOverlayCommand.Execute(null);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!HasSettingsShortcutModifier(e.KeyModifiers))
        {
            return;
        }

        if (e.PhysicalKey != PhysicalKey.Comma
            && e.Key != Key.OemComma
            && !string.Equals(e.KeySymbol, ",", StringComparison.Ordinal))
        {
            return;
        }

        _viewModel.ToggleSettingsCommand.Execute(null);
        e.Handled = true;
    }

    // ---------- Drag & drop ----------

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (TryGetSupportedDroppedFilePath(e) is not null)
        {
            _viewModel.IsDragHovering = true;
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetSupportedDroppedFilePath(e) is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _viewModel.IsDragHovering = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        _viewModel.IsDragHovering = false;

        var path = TryGetSupportedDroppedFilePath(e);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            await _viewModel.OpenDroppedFileAsync(path);
        }
        catch
        {
            // VM сама конвертирует ошибки в LoadError state.
        }
    }

    private static string? TryGetSupportedDroppedFilePath(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return null;
        }

        foreach (var item in files)
        {
            if (item is not IStorageFile file)
            {
                continue;
            }

            var path = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path) && SupportedDocumentTypes.IsSupportedPath(path))
            {
                return path;
            }
        }

        return null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.ShellOverlay)
            or nameof(MainWindowViewModel.IsSettingsOpen)
            or nameof(MainWindowViewModel.IsAppMenuOpen)
            or nameof(MainWindowViewModel.IsAppSettingsOpen)
            or nameof(MainWindowViewModel.IsAppAboutOpen)
            or nameof(MainWindowViewModel.HasOpenOverlay))
        {
            SyncOverlayWindowClasses();
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.ReadingProgress)
            || e.PropertyName == nameof(MainWindowViewModel.IsViewer))
        {
            UpdateReadingProgressBarWidth();
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsEditMode))
        {
            UpdateReadingProgressBarWidth();
        }
    }

    private static bool IsWithinVisual(Visual source, Visual target)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSettingsShortcutModifier(KeyModifiers modifiers)
        => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        CaptureLastNormalWindowPlacement();
        UpdateReadingProgressBarWidth();
    }

    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
        => CaptureLastNormalWindowPlacement();

    private void ApplyStartupWindowPlacement()
    {
        var savedPlacement = LoadWindowPlacementBestEffort();
        var screen = TryGetStartupScreen(savedPlacement);

        if (screen is null)
        {
            ApplyFallbackStartupPlacement(savedPlacement);
            return;
        }

        var startupPlacement = CalculateStartupWindowPlacement(
            savedPlacement,
            screen.WorkingArea,
            screen.Scaling,
            MinWidth,
            MinHeight);

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = startupPlacement.Width;
        Height = startupPlacement.Height;
        Position = new PixelPoint((int)startupPlacement.X, (int)startupPlacement.Y);
        _lastNormalWindowPlacement = startupPlacement with { IsMaximized = false };

        if (savedPlacement?.IsMaximized == true)
        {
            WindowState = WindowState.Maximized;
        }
    }

    internal static WindowPlacement CalculateStartupWindowPlacement(
        WindowPlacement? savedPlacement,
        PixelRect workingArea,
        double screenScaling,
        double minWidth,
        double minHeight)
    {
        var normalizedPlacement = WindowPlacement.Normalize(savedPlacement);
        var scaling = screenScaling > 0 && !double.IsNaN(screenScaling) && !double.IsInfinity(screenScaling)
            ? screenScaling
            : 1;

        var maxWidth = Math.Max(minWidth, (workingArea.Width - WindowPlacementMarginPixels * 2) / scaling);
        var maxHeight = Math.Max(minHeight, (workingArea.Height - WindowPlacementMarginPixels * 2) / scaling);

        var width = Math.Clamp(normalizedPlacement?.Width ?? DefaultWindowWidth, minWidth, maxWidth);
        var height = Math.Clamp(normalizedPlacement?.Height ?? DefaultWindowHeight, minHeight, maxHeight);
        var widthPixels = Math.Max(1, (int)Math.Ceiling(width * scaling));
        var heightPixels = Math.Max(1, (int)Math.Ceiling(height * scaling));

        var x = normalizedPlacement is null
            ? CenterInRange(workingArea.X, workingArea.Width, widthPixels)
            : ClampToWorkingRange((int)Math.Round(normalizedPlacement.X), workingArea.X, workingArea.Width, widthPixels);
        var y = normalizedPlacement is null
            ? CenterInRange(workingArea.Y, workingArea.Height, heightPixels)
            : ClampToWorkingRange((int)Math.Round(normalizedPlacement.Y), workingArea.Y, workingArea.Height, heightPixels);

        return new WindowPlacement(x, y, width, height, IsMaximized: false);
    }

    private static int CenterInRange(int origin, int availableSize, int itemSize)
        => origin + Math.Max(0, (availableSize - itemSize) / 2);

    private static int ClampToWorkingRange(int value, int origin, int availableSize, int itemSize)
    {
        var min = origin + WindowPlacementMarginPixels;
        var max = origin + availableSize - itemSize - WindowPlacementMarginPixels;

        if (max < min)
        {
            return origin;
        }

        return Math.Clamp(value, min, max);
    }

    private WindowPlacement? LoadWindowPlacementBestEffort()
    {
        if (_settings is null)
        {
            return null;
        }

        try
        {
            return _settings.LoadWindowPlacementAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private Screen? TryGetStartupScreen(WindowPlacement? savedPlacement)
    {
        try
        {
            var normalizedPlacement = WindowPlacement.Normalize(savedPlacement);
            if (normalizedPlacement is not null)
            {
                var savedPoint = new PixelPoint(
                    (int)Math.Round(normalizedPlacement.X),
                    (int)Math.Round(normalizedPlacement.Y));
                var savedScreen = Screens.ScreenFromPoint(savedPoint);
                if (savedScreen is not null)
                {
                    return savedScreen;
                }
            }

            return Screens.Primary;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyFallbackStartupPlacement(WindowPlacement? savedPlacement)
    {
        var normalizedPlacement = WindowPlacement.Normalize(savedPlacement);
        if (normalizedPlacement is null)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = Math.Max(MinWidth, normalizedPlacement.Width);
        Height = Math.Max(MinHeight, normalizedPlacement.Height);
        Position = new PixelPoint(
            (int)Math.Round(normalizedPlacement.X),
            (int)Math.Round(normalizedPlacement.Y));
        _lastNormalWindowPlacement = normalizedPlacement with { IsMaximized = false };

        if (normalizedPlacement.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void CaptureLastNormalWindowPlacement()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _lastNormalWindowPlacement = CaptureCurrentNormalWindowPlacement();
    }

    private WindowPlacement CaptureCurrentNormalWindowPlacement()
    {
        var width = Width > 0 && !double.IsNaN(Width) && !double.IsInfinity(Width)
            ? Width
            : Math.Max(MinWidth, Bounds.Width);
        var height = Height > 0 && !double.IsNaN(Height) && !double.IsInfinity(Height)
            ? Height
            : Math.Max(MinHeight, Bounds.Height);

        return new WindowPlacement(
            Position.X,
            Position.Y,
            Math.Max(MinWidth, width),
            Math.Max(MinHeight, height),
            IsMaximized: false);
    }

    private void SaveCurrentWindowPlacementBestEffort()
    {
        if (_settings is null)
        {
            return;
        }

        try
        {
            var placement = CreateWindowPlacementForPersistence();
            _settings.SaveWindowPlacementAsync(placement).AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Window placement persistence is best-effort and must never block closing.
        }
    }

    private WindowPlacement? CreateWindowPlacementForPersistence()
    {
        if (WindowState == WindowState.Normal)
        {
            return CaptureCurrentNormalWindowPlacement();
        }

        if (WindowState == WindowState.Maximized)
        {
            var normalPlacement = _lastNormalWindowPlacement ?? CaptureCurrentNormalWindowPlacement();
            return normalPlacement with { IsMaximized = true };
        }

        return _lastNormalWindowPlacement;
    }

    private void UpdateReadingProgressBarWidth()
    {
        var progressBar = this.FindControl<Border>("ReadingProgressBar");
        if (progressBar is null)
        {
            return;
        }

        if (!_viewModel.IsViewer || _viewModel.IsEditMode)
        {
            progressBar.Width = 0;
            return;
        }

        var bodyPanel = this.FindControl<Panel>("BodyPanel");
        var hostWidth = bodyPanel?.Bounds.Width ?? Bounds.Width;
        var progressRatio = Math.Clamp(_viewModel.ReadingProgress / 100.0, 0, 1);
        progressBar.Width = hostWidth * progressRatio;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_allowConfirmedClose && _viewModel.TryQueueCloseRequest())
        {
            e.Cancel = true;
            return;
        }

        SaveCurrentWindowPlacementBestEffort();
    }

    private bool IsPointerWithinOpenOverlay(Visual source)
    {
        if (_viewModel.IsSettingsOpen)
        {
            var settingsPanel = this.FindControl<Control>("SettingsPanel");
            if (settingsPanel is not null && IsWithinVisual(source, settingsPanel))
            {
                return true;
            }

            var settingsTrigger = this.FindControl<ToggleButton>("SettingsTriggerButton");
            return settingsTrigger is not null && IsWithinVisual(source, settingsTrigger);
        }

        if (_viewModel.IsAppMenuOpen)
        {
            var appMenuPanel = this.FindControl<Control>("AppMenuPanel");
            if (appMenuPanel is not null && IsWithinVisual(source, appMenuPanel))
            {
                return true;
            }
        }

        if (_viewModel.IsAppSettingsOpen)
        {
            var appSettingsPanel = this.FindControl<Control>("AppSettingsPanel");
            if (appSettingsPanel is not null && IsWithinVisual(source, appSettingsPanel))
            {
                return true;
            }
        }

        if (_viewModel.IsAppAboutOpen)
        {
            var appAboutPanel = this.FindControl<Control>("AppAboutPanel");
            if (appAboutPanel is not null && IsWithinVisual(source, appAboutPanel))
            {
                return true;
            }
        }

        var appMenuTrigger = this.FindControl<ToggleButton>("AppMenuTriggerButton");
        return appMenuTrigger is not null && IsWithinVisual(source, appMenuTrigger);
    }

    private void SyncOverlayWindowClasses()
    {
        Classes.Set("mm-overlay-open", _viewModel.HasOpenOverlay);
        Classes.Set("mm-reading-settings-open", _viewModel.IsSettingsOpen);
        Classes.Set("mm-app-menu-open", _viewModel.IsAppMenuOpen);
        Classes.Set("mm-app-settings-open", _viewModel.IsAppSettingsOpen);
        Classes.Set("mm-app-about-open", _viewModel.IsAppAboutOpen);
    }

    private void OnViewModelCloseRequested(object? sender, EventArgs e)
    {
        _allowConfirmedClose = true;
        Close();
    }
}
