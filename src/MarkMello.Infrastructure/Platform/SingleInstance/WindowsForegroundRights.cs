using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace MarkMello.Infrastructure.Platform.SingleInstance;

/// <summary>
/// Windows разрешает вывести окно на передний план только процессу, с которым
/// пользователь сейчас работает, — это вторичный процесс, запущенный кликом.
/// Он передаёт это право запущенному экземпляру, иначе окно лишь мигнёт на панели задач.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsForegroundRights
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);

    public static void GrantTo(NamedPipeClientStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);

        if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverProcessId))
        {
            _ = AllowSetForegroundWindow(serverProcessId);
        }
    }
}
