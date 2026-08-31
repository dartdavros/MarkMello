namespace MarkMello.Domain.Workspace;

/// <summary>
/// Служебные каталоги, которые дерево не показывает (ADR-0007 Rule 6).
/// Список — константа реализации: UI-переключателя в первой версии нет.
/// Проверка скрытого атрибута требует файловой системы и живёт в инфраструктуре;
/// здесь только то, что определяется именем.
/// </summary>
public static class WorkspaceEntryFilter
{
    private static readonly HashSet<string> IgnoredDirectoryNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            "node_modules",
            "bin",
            "obj"
        };

    public static bool IsIgnoredDirectoryName(string? name)
        => !string.IsNullOrWhiteSpace(name) && IgnoredDirectoryNames.Contains(name);

    /// <summary>Скрытым считается всё, что начинается с точки: единое правило для всех платформ.</summary>
    public static bool IsDotPrefixedName(string? name)
        => !string.IsNullOrWhiteSpace(name) && name[0] == '.';
}
