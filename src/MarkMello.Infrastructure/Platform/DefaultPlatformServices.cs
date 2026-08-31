using System.Diagnostics;
using System.Runtime.InteropServices;
using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Платформенный контекст: имя ОС, корзина и показ элемента в файловом менеджере.
/// Всё, что требует запуска внешних процессов или P/Invoke, живёт здесь, а не в Presentation.
/// </summary>
public sealed class DefaultPlatformServices : IPlatformServices
{
    public string PlatformName { get; } = DetectPlatform();

    /// <summary>
    /// Windows — shell32, macOS — NSFileManager, Linux — freedesktop trash spec.
    /// Если корзина недоступна, возвращается <see cref="TrashResult.Unsupported"/>:
    /// удалять безвозвратно молча нельзя, об этом обязан спросить вызывающий (ADR-0007 Rule 7).
    /// </summary>
    public ValueTask<TrashResult> MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path) || !(File.Exists(path) || Directory.Exists(path)))
        {
            return ValueTask.FromResult(TrashResult.Failed);
        }

        try
        {
            var moved = OperatingSystem.IsWindows()
                ? WindowsTrash.TryMoveToRecycleBin(path)
                : UnixTrash.TryMoveToTrash(path);

            // Не «Failed»: корзины может просто не быть — сетевой путь, урезанная система.
            // Пользователю предложат безвозвратное удаление отдельным вопросом.
            return ValueTask.FromResult(moved ? TrashResult.Trashed : TrashResult.Unsupported);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return ValueTask.FromResult(TrashResult.Unsupported);
        }
    }

    public ValueTask RevealInFileManagerAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // /select выделяет сам элемент, а не открывает его.
                Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (OperatingSystem.IsMacOS())
            {
                Start("open", $"-R \"{path}\"");
            }
            else
            {
                // xdg-open не умеет выделять элемент — открываем родительский каталог.
                var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Start("xdg-open", $"\"{directory}\"");
                }
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Нет файлового менеджера — не повод ронять окно.
        }

        return ValueTask.CompletedTask;
    }

    private static void Start(string fileName, string arguments)
        => Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false })?.Dispose();

    private static string DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }
        return "Unknown";
    }
}
