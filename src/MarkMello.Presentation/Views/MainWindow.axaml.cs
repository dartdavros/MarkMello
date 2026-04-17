using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    // ---------- Drag & drop ----------

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (HasFiles(e))
        {
            _viewModel.IsDragHovering = true;
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _viewModel.IsDragHovering = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        _viewModel.IsDragHovering = false;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        foreach (var item in files)
        {
            if (item is IStorageFile file)
            {
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        await _viewModel.OpenDroppedFileAsync(path);
                    }
                    catch
                    {
                        // VM сама конвертирует ошибки в LoadError state.
                    }
                    return;
                }
            }
        }
    }

    private static bool HasFiles(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return false;
        }

        foreach (var _ in files)
        {
            return true;
        }

        return false;
    }
}
