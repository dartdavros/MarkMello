using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

public partial class MainWindow : Window
{
    private const double DefaultWindowWidth = 1280;
    private const double DefaultWindowHeight = 840;
    private const double TitleBarLeadingInset = 14;
    private const double MacOsTitleBarLeadingInset = 82;
    private const int WindowPlacementMarginPixels = 8;

    private readonly ShellViewModel _viewModel = default!;
    private readonly StartupSmokeTestOptions _startupSmokeTestOptions = StartupSmokeTestOptions.Disabled;
    private readonly IStartupMetrics? _startupMetrics;
    private readonly ISettingsStore? _settings;
    private readonly Task _startupInitializationTask = Task.CompletedTask;
    private WindowPlacement? _lastNormalWindowPlacement;
    private bool _restoresToMaximized;
    private Border? _windowBorder;
    private bool _allowConfirmedClose;
    private FindBarView? _findBar;
    private IFindHost? _findHost;

    public MainWindow()
    {
        InitializeComponent();
        ApplyPlatformTitleBarLayout();
    }

    public MainWindow(
        ShellViewModel viewModel,
        StartupSmokeTestOptions startupSmokeTestOptions,
        ISettingsStore settings,
        IStartupMetrics startupMetrics)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(startupSmokeTestOptions);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(startupMetrics);

        _viewModel = viewModel;
        _startupSmokeTestOptions = startupSmokeTestOptions;
        _settings = settings;
        _startupMetrics = startupMetrics;
        DataContext = viewModel;

        ConfigurePlatformChrome();
        InitializeComponent();
        ApplyPlatformTitleBarLayout();
        ApplyStartupWindowPlacement();
        SyncSidebarColumn();
        SyncOverlayWindowClasses();
        UpdateTitleBarMaximizeVisuals();
        UpdateWindowBorder();
        AttachFindBar();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AttachWindowsCaptionPressHook();

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
        SizeChanged += OnWindowSizeChanged;
        PositionChanged += OnWindowPositionChanged;
        ScalingChanged += OnWindowScalingChanged;
        PropertyChanged += OnWindowAvaloniaPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyInterfaceFontSize();
        _viewModel.CloseRequested += OnViewModelCloseRequested;

        _startupInitializationTask = InitializeStartupAsync();
    }

    /// <summary>
    /// Platform chrome rules for Avalonia 12:
    /// - Windows: the XAML layout draws the title bar over the extended client area,
    ///   but the native frame stays. The window keeps WS_CAPTION, so DWM animates
    ///   minimize / maximize / restore and Windows itself maximizes onto the work area
    ///   of the current monitor. BorderOnly drops WS_CAPTION and, with it, all of that.
    /// - macOS: keep native decorations, but extend the client area under our layout.
    ///   BorderOnly/None still have problematic drag behaviour in 12.0.x.
    /// - Linux: keep native chrome because window manager behaviour varies widely.
    /// </summary>
    private void ConfigurePlatformChrome()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 36;
            WindowDecorations = global::Avalonia.Controls.WindowDecorations.Full;
        }
        // Linux: let the window manager draw its native chrome.
    }

    private void ApplyPlatformTitleBarLayout()
    {
        if (this.FindControl<DockPanel>("TitleBarContent") is { } titleBarContent)
        {
            titleBarContent.Margin = CalculateTitleBarContentMargin(OperatingSystem.IsMacOS());
        }
    }

    internal static Thickness CalculateTitleBarContentMargin(bool isMacOS)
        => isMacOS
            ? new Thickness(MacOsTitleBarLeadingInset, 0, TitleBarLeadingInset, 0)
            : new Thickness(TitleBarLeadingInset, 0, 0, 0);

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
            // Keep the fast path resilient: VM initialization should not crash the window.
            // Real logging belongs with the infrastructure logging work in M4+.
        }
    }

    private async Task CompleteStartupSmokeTestAsync()
    {
        if (!_startupSmokeTestOptions.IsEnabled)
        {
            return;
        }

        await MeasureFolderModeAsync().ConfigureAwait(true);
        await Task.Delay(_startupSmokeTestOptions.ExitAfterOpenDelay).ConfigureAwait(true);
        WriteStartupTimings();
        ShutdownClassicDesktopLifetime(exitCode: 0);
    }

    /// <summary>
    /// Замер folder mode: открытие папки и раскрытие первого каталога.
    /// Открывать папку иначе, чем руками через picker, нельзя, поэтому измерение
    /// живёт в том же smoke-режиме, что и тайминги старта, и никогда не включается
    /// в обычном запуске.
    /// </summary>
    private async Task MeasureFolderModeAsync()
    {
        if (_startupSmokeTestOptions.OpenFolderPath is not { } folderPath)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await _viewModel.OpenFolderPathAsync(folderPath).ConfigureAwait(true);
        Console.WriteLine($"[workspace] {"OpenFolder",-20} {stopwatch.Elapsed.TotalMilliseconds,8:F1} ms");

        if (_viewModel.Workspace is not { } workspace)
        {
            return;
        }

        var firstDirectory = workspace.Roots.FirstOrDefault(static node => node.IsDirectory);
        if (firstDirectory is null)
        {
            return;
        }

        stopwatch.Restart();
        await workspace.ExpandNodeAsync(firstDirectory).ConfigureAwait(true);
        Console.WriteLine($"[workspace] {"ExpandNode",-20} {stopwatch.Elapsed.TotalMilliseconds,8:F1} ms");
    }

    /// <summary>
    /// Печатает снимок startup-таймингов в stdout. Вызывается только в smoke-режиме,
    /// поэтому Release-сборку можно измерять теми же командами, что и CI-прогон.
    /// </summary>
    private void WriteStartupTimings()
    {
        if (_startupMetrics is null)
        {
            return;
        }

        foreach (var timing in _startupMetrics.Snapshot().StageTimings.OrderBy(static pair => pair.Key))
        {
            Console.WriteLine($"[startup] {timing.Key,-20} {timing.Value.TotalMilliseconds,8:F1} ms");
        }
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

    // ---------- Find bar (Ctrl+F) ----------

    private void AttachFindBar()
    {
        _findBar = this.FindControl<ContentControl>("FindBarHost")?.Content as FindBarView;
        if (_findBar is null)
        {
            return;
        }

        _findBar.FindNextRequested += OnFindBarFindNextRequested;
        _findBar.FindPreviousRequested += OnFindBarFindPreviousRequested;
        _findBar.CloseRequested += OnFindBarCloseRequested;
    }

    private void DetachFindBar()
    {
        if (_findBar is not null)
        {
            _findBar.FindNextRequested -= OnFindBarFindNextRequested;
            _findBar.FindPreviousRequested -= OnFindBarFindPreviousRequested;
            _findBar.CloseRequested -= OnFindBarCloseRequested;
            _findBar = null;
        }
    }

    private IFindHost? ResolveFindHost()
    {
        if (_findHost is { } cachedHost
            && cachedHost is Visual cachedVisual
            && cachedVisual.IsAttachedToVisualTree())
        {
            return cachedHost;
        }

        DetachFindHost();

        var bodyPanel = this.FindControl<Panel>("BodyPanel");
        if (bodyPanel is null)
        {
            return null;
        }

        _findHost = bodyPanel
            .GetVisualDescendants()
            .OfType<IFindHost>()
            .FirstOrDefault();
        if (_findHost is not null)
        {
            _findHost.FindStateChanged += OnFindHostStateChanged;
        }

        return _findHost;
    }

    private void DetachFindHost()
    {
        if (_findHost is not null)
        {
            _findHost.FindStateChanged -= OnFindHostStateChanged;
            _findHost = null;
        }
    }

    private void InvalidateFindHost() => DetachFindHost();

    private void SyncFindCountersFromHost()
    {
        var host = ResolveFindHost();
        _viewModel.FindMatchIndex = host?.MatchIndex ?? -1;
        _viewModel.FindMatchCount = host?.MatchCount ?? 0;
    }

    private void OnFindBarFindNextRequested(object? sender, EventArgs e)
    {
        ResolveFindHost()?.FindNext();
        SyncFindCountersFromHost();
    }

    private void OnFindBarFindPreviousRequested(object? sender, EventArgs e)
    {
        ResolveFindHost()?.FindPrevious();
        SyncFindCountersFromHost();
    }

    private void OnFindBarCloseRequested(object? sender, EventArgs e)
        => _viewModel.IsFindBarOpen = false;

    private void OnFindHostStateChanged(object? sender, EventArgs e)
        => SyncFindCountersFromHost();

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
        StopUpdateNotification();
        DetachFindBar();
        DetachFindHost();

        Closing -= OnWindowClosing;
        SizeChanged -= OnWindowSizeChanged;
        PositionChanged -= OnWindowPositionChanged;
        ScalingChanged -= OnWindowScalingChanged;
        PropertyChanged -= OnWindowAvaloniaPropertyChanged;
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

    // Our bar stands in for the system one only where the client area extends into the
    // decorations; elsewhere the window manager's own bar moves the window. On Windows
    // the drag area answers as the native caption (its role in MainWindow.axaml), so
    // Windows handles dragging and double-click itself and presses never reach here.
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ExtendClientAreaToDecorationsHint
            || e.ClickCount != 1
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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
            // Unsupported platforms or transient states simply do not start a drag.
        }
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanCloseOverlayOnOutsidePress() || e.Source is not Visual source)
        {
            return;
        }

        if (IsPointerWithinOpenOverlay(source) || IsOverlayPopupInteractionSource(source))
        {
            return;
        }

        _viewModel.CloseOverlayCommand.Execute(null);
    }

    private bool CanCloseOverlayOnOutsidePress()
        => !_viewModel.IsDirtyPromptOpen && _viewModel.HasOpenOverlay;

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var hasCmdOrCtrl = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var hasShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.None)
        {
            if (_viewModel.ReloadCommand.CanExecute(null))
            {
                _viewModel.ReloadCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (hasCmdOrCtrl)
        {
            if (hasShift)
            {
                if (MatchesKey(e, PhysicalKey.S, Key.S))
                {
                    CommitFocusedQuickEditor();
                    if (_viewModel.SaveAsCommand.CanExecute(null))
                    {
                        _viewModel.SaveAsCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.O, Key.O))
                {
                    if (_viewModel.OpenFolderCommand.CanExecute(null))
                    {
                        _viewModel.OpenFolderCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.Tab, Key.Tab))
                {
                    CommitFocusedQuickEditor();
                    if (_viewModel.ActivatePreviousTabCommand.CanExecute(null))
                    {
                        _viewModel.ActivatePreviousTabCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
            }
            else
            {
                if (MatchesKey(e, PhysicalKey.S, Key.S))
                {
                    CommitFocusedQuickEditor();
                    if (_viewModel.SaveCommand.CanExecute(null))
                    {
                        _viewModel.SaveCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.N, Key.N))
                {
                    if (_viewModel.CreateNewDocumentCommand.CanExecute(null))
                    {
                        _viewModel.CreateNewDocumentCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.O, Key.O))
                {
                    if (_viewModel.OpenFileCommand.CanExecute(null))
                    {
                        _viewModel.OpenFileCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.E, Key.E))
                {
                    CommitFocusedQuickEditor();
                    if (_viewModel.ToggleEditModeCommand.CanExecute(null))
                    {
                        _viewModel.ToggleEditModeCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.B, Key.B))
                {
                    if (_viewModel.ToggleSidebarCommand.CanExecute(null))
                    {
                        _viewModel.ToggleSidebarCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.W, Key.W))
                {
                    if (_viewModel.CloseActiveTabCommand.CanExecute(null))
                    {
                        _viewModel.CloseActiveTabCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.F, Key.F))
                {
                    if (_viewModel.ToggleFindBarCommand.CanExecute(null))
                    {
                        _viewModel.ToggleFindBarCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.R, Key.R))
                {
                    if (_viewModel.ReloadCommand.CanExecute(null))
                    {
                        _viewModel.ReloadCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.Comma, Key.OemComma) || string.Equals(e.KeySymbol, ",", StringComparison.Ordinal))
                {
                    if (_viewModel.ToggleSettingsCommand.CanExecute(null))
                    {
                        _viewModel.ToggleSettingsCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                if (MatchesKey(e, PhysicalKey.Tab, Key.Tab))
                {
                    CommitFocusedQuickEditor();
                    if (_viewModel.ActivateNextTabCommand.CanExecute(null))
                    {
                        _viewModel.ActivateNextTabCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
    }

    private void CommitFocusedQuickEditor()
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;
        for (var current = focused; current is not null; current = current.GetVisualParent())
        {
            if (current is MarkdownQuickEditorControl editor)
            {
                editor.Commit();
                break;
            }
        }
    }

    private static bool MatchesKey(KeyEventArgs e, PhysicalKey physicalKey, Key virtualKey)
        => e.PhysicalKey == physicalKey || e.Key == virtualKey;

    // ---------- Drag & drop ----------

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (TryGetDroppedTarget(e) is not null)
        {
            _viewModel.IsDragHovering = true;
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDroppedTarget(e) is not null
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

        var target = TryGetDroppedTarget(e);
        if (target is not { } dropped)
        {
            return;
        }

        try
        {
            // Каталог открывает workspace, файл — документ. Разделение здесь,
            // чтобы обе точки входа шли теми же путями, что picker и меню.
            await (dropped.IsDirectory
                ? _viewModel.OpenFolderPathAsync(dropped.Path)
                : _viewModel.OpenDroppedFileAsync(dropped.Path));
        }
        catch
        {
            // The VM converts failures into the LoadError state.
        }
    }

    private async void OnSidebarSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        try
        {
            if (SidebarLayout is { ColumnDefinitions: { Count: > 0 } columns })
            {
                _viewModel.SidebarWidth = WorkspaceSidebarWidth.Normalize(columns[0].Width.Value);
            }

            await _viewModel.PersistSidebarWidthAsync();
        }
        catch
        {
            // Не сохранили ширину — не повод ронять окно.
        }
    }

    private readonly record struct DroppedTarget(string Path, bool IsDirectory);

    private static DroppedTarget? TryGetDroppedTarget(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return null;
        }

        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            switch (item)
            {
                case IStorageFolder:
                    return new DroppedTarget(path, IsDirectory: true);

                case IStorageFile when SupportedDocumentTypes.IsSupportedPath(path):
                    return new DroppedTarget(path, IsDirectory: false);
            }
        }

        return null;
    }

    /// <summary>
    /// Ширина сайдбара живёт на колонке: GridSplitter двигает колонку, а не контент,
    /// поэтому фиксированная ширина у самого сайдбара оставляла рядом пустую полосу,
    /// а перетаскивание не доходило до view-model. Скрытый сайдбар схлопывает колонку
    /// в ноль — минимум из макета в этот момент не действует.
    /// </summary>
    private void SyncSidebarColumn()
    {
        if (SidebarLayout is not { ColumnDefinitions: { Count: > 0 } columns })
        {
            return;
        }

        var (minWidth, width) = CalculateSidebarColumn(_viewModel.ShowsSidebar, _viewModel.SidebarWidth);
        columns[0].MinWidth = minWidth;
        columns[0].Width = width;
    }

    /// <summary>
    /// Скрытый сайдбар схлопывает колонку в ноль вместе с минимумом: иначе от него
    /// осталась бы пустая полоса шириной 220.
    /// </summary>
    internal static (double MinWidth, GridLength Width) CalculateSidebarColumn(bool showsSidebar, double sidebarWidth)
        => showsSidebar
            ? (WorkspaceSidebarWidth.Minimum, new GridLength(WorkspaceSidebarWidth.Normalize(sidebarWidth)))
            : (0d, new GridLength(0));

    private void UpdateWindowBorder()
    {
        // GetControl throws when the name is missing, which is an authoring bug
        // in our own XAML rather than a runtime condition — better loud at
        // startup than a window that silently never gets its outline.
        var border = _windowBorder ??= this.GetControl<Border>("WindowBorder");

        border.BorderThickness = new Thickness(
            ShouldDrawWindowBorder(
                _viewModel.WindowBorderMode,
                OperatingSystem.IsWindows(),
                WindowState == WindowState.Maximized)
                ? 1
                : 0);
    }

    /// <summary>
    /// Whether the app draws its own window outline.
    ///
    /// Auto draws it only where MarkMello replaces the system chrome with its
    /// own — that is Windows, where a light window on a light background is
    /// otherwise indistinguishable from the one behind it. macOS and Linux keep
    /// native decorations and already have an edge.
    ///
    /// A maximized window never gets one: its edges sit against the screen
    /// bounds, so the outline would only eat a row of pixels.
    /// </summary>
    internal static bool ShouldDrawWindowBorder(WindowBorderMode mode, bool isWindows, bool isMaximized)
    {
        if (isMaximized)
        {
            return false;
        }

        return mode switch
        {
            WindowBorderMode.On => true,
            WindowBorderMode.Off => false,
            _ => isWindows
        };
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

    private void UpdateTitleBarMaximizeVisuals()
    {
        var isRestoreState = WindowState == WindowState.Maximized;

        if (this.FindControl<Control>("TitleBarMaximizeIcon") is { } maximizeIcon)
        {
            maximizeIcon.IsVisible = !isRestoreState;
        }

        if (this.FindControl<Control>("TitleBarRestoreIcon") is { } restoreIcon)
        {
            restoreIcon.IsVisible = isRestoreState;
        }

        if (this.FindControl<Button>("TitleBarMaximizeButton") is { } button)
        {
            ToolTip.SetTip(
                button,
                isRestoreState
                    ? _viewModel.TitleBarRestore
                    : _viewModel.TitleBarMaximize);
        }
    }

    private void OnWindowAvaloniaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            _restoresToMaximized = ResolveRestoresToMaximized(
                e.GetOldValue<WindowState>(),
                e.GetNewValue<WindowState>(),
                _restoresToMaximized);
            UpdateTitleBarMaximizeVisuals();
            UpdateWindowBorder();
        }
    }

    private static bool HasSettingsShortcutModifier(KeyModifiers modifiers)
        => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ScheduleNormalWindowPlacementCapture();
        UpdateReadingProgressBarWidth();
    }

    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
        => ScheduleNormalWindowPlacementCapture();

    // A maximize or minimize started by the OS (caption double-click, Win+Up, the
    // taskbar) reports the new position and size before Avalonia updates WindowState:
    // the resize even runs layout synchronously. Capturing right away would store the
    // maximized or off-screen bounds as the normal ones, so let the state change land first.
    private void ScheduleNormalWindowPlacementCapture()
        => Dispatcher.UIThread.Post(CaptureLastNormalWindowPlacement);

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
            var placement = ResolveWindowPlacementForPersistence(
                WindowState,
                _restoresToMaximized,
                _lastNormalWindowPlacement,
                CaptureCurrentNormalWindowPlacement());
            _settings.SaveWindowPlacementAsync(placement).AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Window placement persistence is best-effort and must never block closing.
        }
    }

    /// <summary>
    /// A minimized window comes back in the state it was minimized from: restoring a
    /// window minimized while maximized maximizes it again.
    /// </summary>
    internal static bool ResolveRestoresToMaximized(
        WindowState oldState,
        WindowState newState,
        bool restoresToMaximized)
        => newState == WindowState.Minimized
            ? oldState == WindowState.Maximized
            : restoresToMaximized;

    internal static WindowPlacement? ResolveWindowPlacementForPersistence(
        WindowState windowState,
        bool restoresToMaximized,
        WindowPlacement? lastNormalPlacement,
        WindowPlacement currentPlacement)
        => windowState switch
        {
            WindowState.Normal => currentPlacement,
            WindowState.Maximized => (lastNormalPlacement ?? currentPlacement) with { IsMaximized = true },
            // Closed from the taskbar while minimized: the next start opens the window
            // the way it would have come back from the taskbar.
            WindowState.Minimized => lastNormalPlacement is { } placement
                ? placement with { IsMaximized = restoresToMaximized }
                : null,
            _ => lastNormalPlacement
        };

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
