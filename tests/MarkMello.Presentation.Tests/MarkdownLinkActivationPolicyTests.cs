using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownLinkActivationPolicyTests
{
    private static readonly MarkdownLinkSpan LinkA = new(new DocumentTextRange(2, 7), "https://example.com", null);
    private static readonly MarkdownLinkSpan LinkB = new(new DocumentTextRange(2, 7), "https://example.org", null);
    private static readonly MarkdownLinkSpan LinkOtherRange = new(new DocumentTextRange(8, 12), "https://example.com", null);

    [Fact]
    public void CanActivateLinkReturnsTrueWhenSelectionIsCollapsedAndSameLinkWasReleased()
    {
        var canActivate = MarkdownLinkActivationPolicy.CanActivateLink(
            isDraggingSelection: false,
            selectionAnchor: 4,
            selectionStart: 4,
            selectionEnd: 4,
            pressedLink: LinkA,
            releasedLink: LinkA);

        Assert.True(canActivate);
    }

    [Fact]
    public void CanActivateLinkReturnsFalseWhenSelectionExpanded()
    {
        var canActivate = MarkdownLinkActivationPolicy.CanActivateLink(
            isDraggingSelection: false,
            selectionAnchor: 4,
            selectionStart: 2,
            selectionEnd: 7,
            pressedLink: LinkA,
            releasedLink: LinkA);

        Assert.False(canActivate);
    }

    [Fact]
    public void CanActivateLinkReturnsFalseWhenPointerDragOccurred()
    {
        var canActivate = MarkdownLinkActivationPolicy.CanActivateLink(
            isDraggingSelection: true,
            selectionAnchor: 4,
            selectionStart: 4,
            selectionEnd: 4,
            pressedLink: LinkA,
            releasedLink: LinkA);

        Assert.False(canActivate);
    }

    [Fact]
    public void CanActivateLinkReturnsFalseWhenReleasedLinkDiffersByUrl()
    {
        var canActivate = MarkdownLinkActivationPolicy.CanActivateLink(
            isDraggingSelection: false,
            selectionAnchor: 4,
            selectionStart: 4,
            selectionEnd: 4,
            pressedLink: LinkA,
            releasedLink: LinkB);

        Assert.False(canActivate);
    }

    [Fact]
    public void CanActivateLinkReturnsFalseWhenReleasedLinkDiffersByRange()
    {
        var canActivate = MarkdownLinkActivationPolicy.CanActivateLink(
            isDraggingSelection: false,
            selectionAnchor: 4,
            selectionStart: 4,
            selectionEnd: 4,
            pressedLink: LinkA,
            releasedLink: LinkOtherRange);

        Assert.False(canActivate);
    }
}
