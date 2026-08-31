using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Перемещение в корзину Windows через <c>SHFileOperationW</c>.
///
/// Управляемого API для корзины в .NET нет: <c>File.Delete</c> удаляет безвозвратно,
/// а <c>Microsoft.VisualBasic.FileIO</c> тянет лишнюю зависимость и хуже предсказуем
/// под trimming. P/Invoke в shell32 совместим с AOT и делает ровно то, что нужно.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsTrash
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCTW
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperationW(ref SHFILEOPSTRUCTW fileOp);

    /// <summary>Возвращает false, если shell отказался переместить элемент в корзину.</summary>
    public static bool TryMoveToRecycleBin(string path)
    {
        // Список путей в pFrom обязан заканчиваться двойным нулём.
        var operation = new SHFILEOPSTRUCTW
        {
            wFunc = FO_DELETE,
            pFrom = path + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT
        };

        var result = SHFileOperationW(ref operation);
        return result == 0 && operation.fAnyOperationsAborted == 0;
    }
}
