using Avalonia;
using Avalonia.Controls;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal abstract class MarkdownDocumentSelectionFragmentBase : Control, IDisposable
{
    private DocumentTextRange _documentRange = DocumentTextRange.Empty;
    private DocumentTextRange _selectionRange = DocumentTextRange.Empty;
    private IReadOnlyList<DocumentTextRange> _searchHighlightRanges = Array.Empty<DocumentTextRange>();
    private DocumentTextRange? _activeSearchHighlight;

    public DocumentTextRange DocumentRange
    {
        get => _documentRange;
        set
        {
            _documentRange = value;
            InvalidateVisual();
        }
    }

    public DocumentTextRange SelectionRange
    {
        get => _selectionRange;
        set
        {
            if (_selectionRange == value)
            {
                return;
            }

            _selectionRange = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Search-match ranges intersecting this fragment, in document coordinates.
    /// Ranges are already intersected with <see cref="DocumentRange"/>.
    /// </summary>
    public IReadOnlyList<DocumentTextRange> SearchHighlightRanges
    {
        get => _searchHighlightRanges;
        set
        {
            var normalized = value ?? Array.Empty<DocumentTextRange>();
            if (_searchHighlightRanges.Count == normalized.Count
                && _searchHighlightRanges.SequenceEqual(normalized))
            {
                return;
            }

            _searchHighlightRanges = normalized;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The active search match (current result) intersecting this fragment,
    /// in document coordinates, or null when this fragment has no active match.
    /// </summary>
    public DocumentTextRange? ActiveSearchHighlight
    {
        get => _activeSearchHighlight;
        set
        {
            if (_activeSearchHighlight == value)
            {
                return;
            }

            _activeSearchHighlight = value;
            InvalidateVisual();
        }
    }

    public abstract int GetDocumentOffset(Point localPoint);

    public abstract DocumentTextRange GetDocumentWordRange(Point localPoint);

    public abstract bool TryGetLinkAt(Point localPoint, out MarkdownLinkSpan linkSpan);

    public abstract void Dispose();
}
