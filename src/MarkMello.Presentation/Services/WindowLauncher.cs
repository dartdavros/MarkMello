using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Presentation.Services;

/// <summary>
/// Открытие окон. Вторая папка живёт в отдельном окне: multi-root workspace не вводится,
/// а два дерева в одном окне — это уже IDE (ADR-0007 Rule 11).
/// </summary>
public interface IWindowLauncher
{
    /// <summary>
    /// Если папка уже открыта в другом окне, выводит его вперёд и возвращает true.
    /// Второе окно на ту же папку не создаётся: это сбивало бы с толку.
    /// </summary>
    bool TryFocusWindowWithFolder(string folderPath);

    /// <summary>
    /// Открывает новое окно и показывает в нём указанную папку. Задача завершается, когда
    /// папка открыта: после этого в окно можно отправлять файлы из неё.
    /// </summary>
    Task OpenFolderInNewWindowAsync(string folderPath);

    /// <summary>
    /// Выводит вперёд окно, в чьей открытой папке лежит файл, и возвращает его shell.
    /// Null — файл не принадлежит ни одной открытой папке.
    /// </summary>
    ShellViewModel? FocusWindowForDocument(string documentPath);

    /// <summary>
    /// Запоминает окно как кандидата для <see cref="BringActivationTargetToFront"/>:
    /// документы из повторного запуска уходят в окно, с которым работали последним.
    /// </summary>
    void TrackWindow(Window window);

    /// <summary>
    /// Выводит вперёд окно-получатель повторного запуска (последнее активное),
    /// разворачивая его из свёрнутого, и возвращает его shell.
    /// </summary>
    ShellViewModel? BringActivationTargetToFront();
}

public sealed class WindowLauncher : IWindowLauncher
{
    /// <summary>Смещение каскада: новое окно не должно точно накрывать предыдущее.</summary>
    private const int CascadeOffset = 32;

    private readonly IServiceProvider _services;

    /// <summary>Состояние до сворачивания: развёрнутое окно должно вернуться развёрнутым.</summary>
    private readonly ConditionalWeakTable<Window, StrongBox<WindowState>> _restoreStates = new();

    private WeakReference<Window>? _lastActiveWindow;

    public WindowLauncher(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public bool TryFocusWindowWithFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || GetLifetime() is not { } lifetime)
        {
            return false;
        }

        foreach (var window in lifetime.Windows)
        {
            if (window.DataContext is not ShellViewModel { Workspace: { } workspace })
            {
                continue;
            }

            if (!PathsMatch(workspace.Folder.RootPath, folderPath))
            {
                continue;
            }

            BringToFront(window);
            return true;
        }

        return false;
    }

    public Task OpenFolderInNewWindowAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || GetLifetime() is not { } lifetime)
        {
            return Task.CompletedTask;
        }

        // View-model создаётся до окна, чтобы успеть погасить стартовую активацию:
        // иначе новое окно откроет папку из аргументов процесса и восстановит её сессию,
        // а запрошенная папка ляжет поверх — с чужими вкладками.
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.SuppressStartupActivation();

        var window = ActivatorUtilities.CreateInstance<MainWindow>(_services, shell);
        var previous = lifetime.Windows.Count > 0 ? lifetime.Windows[^1] : null;
        ApplyCascade(window, previous);
        TrackWindow(window);
        window.Show();

        // Папка открывается уже в новом окне: его view-model — своя, вкладки не общие.
        return shell.OpenFolderPathAsync(folderPath);
    }

    public ShellViewModel? FocusWindowForDocument(string documentPath)
    {
        if (string.IsNullOrWhiteSpace(documentPath) || GetLifetime() is not { } lifetime)
        {
            return null;
        }

        // Вложенные папки в разных окнах: файл уходит в окно самой глубокой из них.
        var window = lifetime.Windows
            .Where(candidate => candidate.DataContext is ShellViewModel { Workspace: { } workspace }
                && IsUnder(documentPath, workspace.Folder.RootPath))
            .OrderByDescending(static candidate => ((ShellViewModel)candidate.DataContext!).Workspace!.Folder.RootPath.Length)
            .FirstOrDefault();

        if (window?.DataContext is not ShellViewModel shell)
        {
            return null;
        }

        BringToFront(window);
        return shell;
    }

    public void TrackWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Новое окно считается последним, пока пользователь не переключился на другое.
        _lastActiveWindow = new WeakReference<Window>(window);
        window.Activated += (_, _) => _lastActiveWindow = new WeakReference<Window>(window);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty
                && e.GetNewValue<WindowState>() is var state and not WindowState.Minimized)
            {
                _restoreStates.AddOrUpdate(window, new StrongBox<WindowState>(state));
            }
        };
    }

    public ShellViewModel? BringActivationTargetToFront()
    {
        if (GetLifetime() is not { } lifetime)
        {
            return null;
        }

        var window = GetLastActiveWindow(lifetime)
            ?? (lifetime.Windows.Count > 0 ? lifetime.Windows[^1] : null);
        if (window?.DataContext is not ShellViewModel shell)
        {
            return null;
        }

        BringToFront(window);
        return shell;
    }

    /// <summary>Свёрнутое окно разворачивается в прежнее состояние, а не в «обычное».</summary>
    private void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = _restoreStates.TryGetValue(window, out var restore)
                ? restore.Value
                : WindowState.Normal;
        }

        window.Activate();
    }

    /// <summary>Закрытое окно может ещё жить в слабой ссылке — берём только из открытых.</summary>
    private Window? GetLastActiveWindow(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        if (_lastActiveWindow is null || !_lastActiveWindow.TryGetTarget(out var window))
        {
            return null;
        }

        return lifetime.Windows.Contains(window) ? window : null;
    }

    /// <summary>
    /// Каскад ограничен рабочей областью: если окно уехало бы за край, оно встаёт
    /// на позицию исходного, а не прячется за экраном.
    /// </summary>
    private static void ApplyCascade(Window window, Window? previous)
    {
        if (previous is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = previous.Width;
        window.Height = previous.Height;

        var candidate = new PixelPoint(
            previous.Position.X + CascadeOffset,
            previous.Position.Y + CascadeOffset);

        var workingArea = previous.Screens?.ScreenFromWindow(previous)?.WorkingArea;
        if (workingArea is { } area
            && (candidate.X + previous.Width > area.X + area.Width
                || candidate.Y + previous.Height > area.Y + area.Height))
        {
            candidate = previous.Position;
        }

        window.Position = candidate;
    }

    private static IClassicDesktopStyleApplicationLifetime? GetLifetime()
        => global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    private static bool IsUnder(string path, string root)
        => path.StartsWith(
            Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool PathsMatch(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
