using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Корзина на macOS и Linux.
///
/// macOS — <c>NSFileManager trashItemAtURL:</c> через ObjC runtime: приложение и так
/// линкует libobjc, а <c>osascript</c> потребовал бы у пользователя разрешения на Automation
/// ради удаления собственного файла.
///
/// Linux — freedesktop trash spec вручную: файл переезжает в <c>~/.local/share/Trash/files</c>
/// вместе с <c>.trashinfo</c>. Внешний <c>gio</c> есть не везде, а спека простая и стабильная.
/// </summary>
internal static class UnixTrash
{
    public static bool TryMoveToTrash(string path)
        => OperatingSystem.IsMacOS() ? TryMoveToMacTrash(path) : TryMoveToFreedesktopTrash(path);

    // ---------- macOS ----------

    [SupportedOSPlatform("macos")]
    private static bool TryMoveToMacTrash(string path)
    {
        try
        {
            var fileManager = objc_msgSend_retIntPtr(
                objc_getClass("NSFileManager"),
                sel_registerName("defaultManager"));

            var url = objc_msgSend_retIntPtr_arg(
                objc_getClass("NSURL"),
                sel_registerName("fileURLWithPath:"),
                CreateNsString(path));

            if (fileManager == IntPtr.Zero || url == IntPtr.Zero)
            {
                return false;
            }

            // trashItemAtURL:resultingItemURL:error: — nil для обоих выходных параметров.
            return objc_msgSend_trash(
                fileManager,
                sel_registerName("trashItemAtURL:resultingItemURL:error:"),
                url,
                IntPtr.Zero,
                IntPtr.Zero);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("macos")]
    private static IntPtr CreateNsString(string value)
    {
        var nsString = objc_msgSend_retIntPtr(objc_getClass("NSString"), sel_registerName("alloc"));
        return objc_msgSend_utf8(
            nsString,
            sel_registerName("initWithUTF8String:"),
            Encoding.UTF8.GetBytes(value + '\0'));
    }

    // ObjC runtime принимает имена классов и селекторов как ASCII-строки.
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_retIntPtr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_retIntPtr_arg(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_utf8(IntPtr receiver, IntPtr selector, byte[] utf8);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_trash(
        IntPtr receiver,
        IntPtr selector,
        IntPtr url,
        IntPtr resultingUrl,
        IntPtr error);

    // ---------- Linux ----------

    private static bool TryMoveToFreedesktopTrash(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            return false;
        }

        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var trashRoot = string.IsNullOrWhiteSpace(dataHome)
            ? Path.Combine(home, ".local", "share", "Trash")
            : Path.Combine(dataHome, "Trash");

        var filesDirectory = Path.Combine(trashRoot, "files");
        var infoDirectory = Path.Combine(trashRoot, "info");

        try
        {
            Directory.CreateDirectory(filesDirectory);
            Directory.CreateDirectory(infoDirectory);

            var name = BuildUniqueName(filesDirectory, Path.GetFileName(path));
            var target = Path.Combine(filesDirectory, name);

            // .trashinfo пишем до переноса: файл без метаданных корзина считает мусором.
            File.WriteAllText(
                Path.Combine(infoDirectory, name + ".trashinfo"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[Trash Info]\nPath={Uri.EscapeDataString(Path.GetFullPath(path))}\nDeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n"));

            if (Directory.Exists(path))
            {
                Directory.Move(path, target);
            }
            else
            {
                File.Move(path, target);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Корзина на другом томе или недоступна — вызывающая сторона переспросит.
            return false;
        }
    }

    private static string BuildUniqueName(string directory, string name)
    {
        if (!File.Exists(Path.Combine(directory, name)) && !Directory.Exists(Path.Combine(directory, name)))
        {
            return name;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);

        for (var index = 1; ; index++)
        {
            var candidate = $"{stem}.{index}{extension}";
            var fullPath = Path.Combine(directory, candidate);

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return candidate;
            }
        }
    }
}
