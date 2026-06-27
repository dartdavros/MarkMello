namespace MarkMello.Presentation.Views;

public sealed class MarkdownFileLinkRequestedEventArgs : EventArgs
{
    public MarkdownFileLinkRequestedEventArgs(string originalLink, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalLink);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        OriginalLink = originalLink;
        TargetPath = targetPath;
    }

    public string OriginalLink { get; }

    public string TargetPath { get; }
}
