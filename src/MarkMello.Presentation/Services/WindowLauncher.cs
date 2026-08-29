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

    /// <summary>Открывает новое окно и показывает в нём указанную папку.</summary>
    void OpenFolderInNewWindow(string folderPath);
}

public sealed class WindowLauncher : IWindowLauncher
{
    /// <summary>Смещение каскада: новое окно не должно точно накрывать предыдущее.</summary>
    private const int CascadeOffset = 32;

    private readonly IServiceProvider _services;

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

            window.Activate();
            return true;
        }

        return false;
    }

    public void OpenFolderInNewWindow(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || GetLifetime() is not { } lifetime)
        {
            return;
        }

        // View-model создаётся до окна, чтобы успеть погасить стартовую активацию:
        // иначе новое окно откроет папку из аргументов процесса и восстановит её сессию,
        // а запрошенная папка ляжет поверх — с чужими вкладками.
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.SuppressStartupActivation();

        var window = ActivatorUtilities.CreateInstance<MainWindow>(_services, shell);
        var previous = lifetime.Windows.Count > 0 ? lifetime.Windows[^1] : null;
        ApplyCascade(window, previous);
        window.Show();

        // Папка открывается уже в новом окне: его view-model — своя, вкладки не общие.
        _ = shell.OpenFolderPathAsync(folderPath);
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

    private static bool PathsMatch(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
