namespace MarkMello.Presentation.Views;

/// <summary>
/// Search surface implemented by the active content view (reader or editor).
/// The window-level find bar forwards query and navigation commands here and
/// reads the match counter state back for display.
/// </summary>
public interface IFindHost
{
    /// <summary>Active query, or null when find is not active.</summary>
    string? ActiveQuery { get; }

    /// <summary>Zero-based index of the current match, or -1 when none.</summary>
    int MatchIndex { get; }

    /// <summary>Total number of matches for the active query.</summary>
    int MatchCount { get; }

    /// <summary>Raised when the match set or current match changes.</summary>
    event EventHandler? FindStateChanged;

    void ApplyQuery(string? query);

    void FindNext();

    void FindPrevious();

    void ClearFind();
}
