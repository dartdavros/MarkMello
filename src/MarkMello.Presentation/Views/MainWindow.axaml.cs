using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;
using System.ComponentModel;

namespace MarkMello.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _allowConfirmedClose;

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        ConfigurePlatformChrome();
        InitializeComponent();
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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CloseRequested += OnViewModelCloseRequested;
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
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch
        {
            // Защита fast path: VM init не должна валить окно.
            // Реальный logging придёт вместе с infrastructure logging в M4+.
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Closing -= OnWindowClosing;
        SizeChanged -= OnWindowSizeChanged;
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
        if (!_viewModel.ShowCustomTitleBar || e.ClickCount != 1)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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

        if (IsPointerWithinOpenOverlay(source))
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
        => UpdateReadingProgressBarWidth();

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
        if (_allowConfirmedClose)
        {
            return;
        }

        if (_viewModel.TryQueueCloseRequest())
        {
            e.Cancel = true;
        }
    }

    private bool IsPointerWithinOpenOverlay(Visual source)
    {
        if (_viewModel.IsSettingsOpen)
        {
            var settingsPanel = this.FindControl<Border>("SettingsPanel");
            if (settingsPanel is not null && IsWithinVisual(source, settingsPanel))
            {
                return true;
            }

            var settingsTrigger = this.FindControl<ToggleButton>("SettingsTriggerButton");
            return settingsTrigger is not null && IsWithinVisual(source, settingsTrigger);
        }

        if (_viewModel.IsAppMenuOpen)
        {
            var appMenuPanel = this.FindControl<Border>("AppMenuPanel");
            if (appMenuPanel is not null && IsWithinVisual(source, appMenuPanel))
            {
                return true;
            }
        }

        if (_viewModel.IsAppSettingsOpen)
        {
            var appSettingsPanel = this.FindControl<Border>("AppSettingsPanel");
            if (appSettingsPanel is not null && IsWithinVisual(source, appSettingsPanel))
            {
                return true;
            }
        }

        if (_viewModel.IsAppAboutOpen)
        {
            var appAboutPanel = this.FindControl<Border>("AppAboutPanel");
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

    private async void OnAboutLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string rawUrl })
        {
            return;
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is null)
        {
            return;
        }

        await launcher.LaunchUriAsync(uri);
    }

    private void OnViewModelCloseRequested(object? sender, EventArgs e)
    {
        _allowConfirmedClose = true;
        Close();
    }
}
