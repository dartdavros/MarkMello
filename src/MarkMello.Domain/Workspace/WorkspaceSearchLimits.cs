namespace MarkMello.Domain.Workspace;

/// <summary>
/// Границы поиска по именам. Поиск обязан заканчиваться предсказуемо даже на папке,
/// куда случайно попал большой репозиторий (ADR-0007 Rule 8), поэтому лимиты —
/// часть контракта, а не деталь реализации.
/// </summary>
public sealed record WorkspaceSearchLimits(int MaxMatches, int MaxScannedEntries, int MaxDepth)
{
    public static WorkspaceSearchLimits Default { get; } = new(
        MaxMatches: 200,
        MaxScannedEntries: 20_000,
        MaxDepth: 12);
}
