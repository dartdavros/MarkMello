using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MarkMello.Presentation.Views;

public sealed partial class MarkdownDocumentView
{
    /// <summary>Отступ сверху, как у якорных переходов: заголовок «текущий», когда дошёл до этой линии.</summary>
    private const double OutlineActivationInset = 32;

    internal bool TryScrollToHeadingSourceLine(int sourceLine)
    {
        foreach (var (block, control) in _headingAnchorRegistrations)
        {
            if (block.SourceSpan?.StartLine == sourceLine)
            {
                return TryScrollTargetIntoView(control);
            }
        }

        return false;
    }

    internal int? GetCurrentHeadingSourceLine()
    {
        var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (scrollViewer is null)
        {
            return null;
        }

        int? current = null;
        foreach (var (block, control) in _headingAnchorRegistrations)
        {
            if (block.SourceSpan is not { } span
                || control.TranslatePoint(new Point(0, 0), scrollViewer) is not { } point)
            {
                continue;
            }

            if (point.Y > OutlineActivationInset)
            {
                break;
            }

            current = span.StartLine;
        }

        return current;
    }
}
