namespace MarkMello.Domain.Workspace;

/// <summary>
/// Узел дерева файлов: каталог или файл. Дерево показывает все файлы,
/// но во вкладке открываются только поддерживаемые типы — остальные инертны
/// (ADR-0007 Rule 6), поэтому признак вынесен в модель, а не считается в UI.
/// </summary>
public sealed record WorkspaceEntry(string Path, string Name, bool IsDirectory, bool IsSupportedDocument)
{
    public static WorkspaceEntry ForDirectory(string path, string name)
        => new(path, name, IsDirectory: true, IsSupportedDocument: false);

    public static WorkspaceEntry ForFile(string path, string name)
        => new(path, name, IsDirectory: false, SupportedDocumentTypes.IsSupportedPath(path));
}
