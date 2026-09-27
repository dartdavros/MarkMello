using System.Runtime.InteropServices;

namespace MarkMello.Presentation.Views;

public partial class MainWindow
{
    private const uint WmSize = 0x0005;
    private const int SizeMaximized = 2;

    /// <summary>
    /// Works around Avalonia 12 keeping a stale client size when a maximized window
    /// changes DPI. Windows moves such a window to a monitor with another scale
    /// (Win+Shift+Arrow, a monitor going away) and reports WM_SIZE before
    /// WM_DPICHANGED, so Avalonia converts the new client size with the old scaling.
    /// The bounds do not change, so no fresh WM_SIZE follows: the content keeps the
    /// wrong size, and the next layout pass even writes it into the restore bounds.
    /// ScalingChanged runs inside WM_DPICHANGED once the new scaling is in place and
    /// before any layout, so re-reporting the real client size here fixes both.
    /// </summary>
    private void OnWindowScalingChanged(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows()
            || TryGetPlatformHandle()?.Handle is not { } hWnd
            || !IsZoomed(hWnd)
            || !GetClientRect(hWnd, out var client))
        {
            return;
        }

        // Sent rather than posted: Avalonia has to take the size before its next layout pass.
        _ = SendMessage(hWnd, WmSize, SizeMaximized, (client.Bottom << 16) | (client.Right & 0xFFFF));
    }

    #pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    #pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct RECT(int Left, int Top, int Right, int Bottom);
}
