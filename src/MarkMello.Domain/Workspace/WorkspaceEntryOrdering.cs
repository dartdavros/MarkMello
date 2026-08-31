namespace MarkMello.Domain.Workspace;

/// <summary>
/// Порядок строк в дереве: сначала каталоги, затем файлы, внутри группы —
/// по имени без учёта регистра. Вынесено из инфраструктуры, чтобы порядок
/// можно было проверить тестом без файловой системы.
/// </summary>
public sealed class WorkspaceEntryOrdering : IComparer<WorkspaceEntry>
{
    public static WorkspaceEntryOrdering Instance { get; } = new();

    private WorkspaceEntryOrdering() { }

    public int Compare(WorkspaceEntry? x, WorkspaceEntry? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        if (x.IsDirectory != y.IsDirectory)
        {
            return x.IsDirectory ? -1 : 1;
        }

        var byName = StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name);
        return byName != 0
            ? byName
            : StringComparer.Ordinal.Compare(x.Path, y.Path);
    }
}
