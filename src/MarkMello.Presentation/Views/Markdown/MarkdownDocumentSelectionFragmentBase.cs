using Avalonia;
using Avalonia.Controls;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal abstract class MarkdownDocumentSelectionFragmentBase : Control, IDisposable
{
    private DocumentTextRange _documentRange = DocumentTextRange.Empty;
    private DocumentTextRange _selectionRange = DocumentTextRange.Empty;

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

    public abstract int GetDocumentOffset(Point localPoint);

    public abstract DocumentTextRange GetDocumentWordRange(Point localPoint);

    public abstract bool TryGetLinkAt(Point localPoint, out MarkdownLinkSpan linkSpan);

    public abstract void Dispose();
}
