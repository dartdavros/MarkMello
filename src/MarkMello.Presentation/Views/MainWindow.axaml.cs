using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        ConfigurePlatformChrome();
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Opened += OnWindowOpened;
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
}
