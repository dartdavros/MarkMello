using System.Runtime.InteropServices;
using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Дефолтная реализация платформенного контекста для baseline M2.
/// Реальная регистрация file associations делается на уровне packaging/installer,
/// а runtime activation приходит в приложение как command-line path.
/// </summary>
public sealed class DefaultPlatformServices : IPlatformServices
{
    public string PlatformName { get; } = DetectPlatform();

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
