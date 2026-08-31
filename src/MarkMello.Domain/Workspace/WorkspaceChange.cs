namespace MarkMello.Domain.Workspace;

/// <summary>Что произошло с элементом на диске мимо приложения.</summary>
public enum WorkspaceChangeKind
{
    Created,
    Changed,
    Deleted,
    Renamed
}

/// <summary>
/// Внешнее изменение в открытой папке. <paramref name="PreviousPath"/> заполняется
/// только у переименования — по нему вкладка находит себя и следует за файлом.
/// </summary>
public sealed record WorkspaceChange(WorkspaceChangeKind Kind, string Path, string? PreviousPath = null)
{
    /// <summary>Каталог, содержимое которого нужно перечитать.</summary>
    public string AffectedDirectory => System.IO.Path.GetDirectoryName(Path) ?? Path;
}
