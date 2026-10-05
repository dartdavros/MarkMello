namespace MarkMello.Presentation.Views;

public sealed class MarkdownDocumentContentEditedEventArgs : EventArgs
{
    public MarkdownDocumentContentEditedEventArgs(string newContent)
    {
        ArgumentNullException.ThrowIfNull(newContent);
        NewContent = newContent;
    }

    public string NewContent { get; }
}
