namespace MarkMello.Domain.Workspace;

/// <summary>
/// Итог поиска. <paramref name="IsTruncated"/> отдельно от количества: пользователь
/// должен видеть, что выдача неполная, а не считать 200 совпадений полным ответом.
/// </summary>
public sealed record WorkspaceSearchResult(IReadOnlyList<WorkspaceSearchHit> Hits, bool IsTruncated)
{
    public static WorkspaceSearchResult Empty { get; } = new([], IsTruncated: false);
}
