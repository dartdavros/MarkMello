namespace MarkMello.Domain.Workspace;

/// <summary>
/// Корень открытой папки. Транзиентная модель: живёт столько же, сколько folder session,
/// на диск не пишется и не превращается в project metadata (ADR-0007 Rule 10).
/// </summary>
public sealed record WorkspaceFolder(string RootPath, string DisplayName)
{
    /// <summary>
    /// Строит корень по абсолютному пути. Имя берётся из последнего сегмента;
    /// для корня диска (<c>C:\</c>) сегмента нет, поэтому используется сам путь.
    /// </summary>
    public static WorkspaceFolder Create(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalized = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalized.Length == 0)
        {
            normalized = rootPath;
        }

        var name = Path.GetFileName(normalized);
        return new WorkspaceFolder(
            normalized,
            string.IsNullOrWhiteSpace(name) ? rootPath : name);
    }
}
