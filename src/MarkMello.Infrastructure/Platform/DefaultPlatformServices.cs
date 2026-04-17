using System.Runtime.InteropServices;
using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Дефолтная реализация. В M2 здесь появятся file association hooks,
/// command-line activation parsing и системные шорткаты.
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
