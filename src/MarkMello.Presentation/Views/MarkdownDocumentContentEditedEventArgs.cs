namespace MarkMello.Presentation.Views;

public sealed class MarkdownDocumentContentEditedEventArgs : EventArgs
{
    public MarkdownDocumentContentEditedEventArgs(string newContent, string? baseContent = null)
    {
        ArgumentNullException.ThrowIfNull(newContent);
        NewContent = newContent;
        BaseContent = baseContent;
    }

    public string NewContent { get; }

    /// <summary>
    /// Исходник, к которому относится правка. Получатель должен отбросить правку,
    /// если его текущий текст уже другой.
    /// </summary>
    public string? BaseContent { get; }
}
