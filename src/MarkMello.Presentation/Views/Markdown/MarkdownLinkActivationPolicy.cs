namespace MarkMello.Presentation.Views.Markdown;

internal static class MarkdownLinkActivationPolicy
{
    public static bool CanActivateLink(
        bool isDraggingSelection,
        int? selectionAnchor,
        int selectionStart,
        int selectionEnd,
        MarkdownLinkSpan? pressedLink,
        MarkdownLinkSpan? releasedLink)
    {
        if (isDraggingSelection || selectionAnchor is null || selectionStart != selectionEnd)
        {
            return false;
        }

        if (pressedLink is not MarkdownLinkSpan pressed || releasedLink is not MarkdownLinkSpan released)
        {
            return false;
        }

        return pressed.Range == released.Range
            && string.Equals(pressed.Url, released.Url, StringComparison.Ordinal);
    }
}
