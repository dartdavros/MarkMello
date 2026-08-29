namespace MarkMello.Domain.Workspace;

/// <summary>
/// Совпадение в дереве: сам элемент, путь относительно корня для подписи под строкой
/// и позиция совпадения в имени — подсветку считает домен, а не UI.
/// </summary>
public sealed record WorkspaceSearchHit(
    WorkspaceEntry Entry,
    string RelativeDirectory,
    int MatchStart,
    int MatchLength)
{
    public string NameBeforeMatch => Entry.Name[..MatchStart];

    public string MatchedName => Entry.Name.Substring(MatchStart, MatchLength);

    public string NameAfterMatch => Entry.Name[(MatchStart + MatchLength)..];

    /// <summary>Файл в корне папки подписи не получает: под ним не должно быть пустой строки.</summary>
    public bool HasRelativeDirectory => !string.IsNullOrEmpty(RelativeDirectory);
}
