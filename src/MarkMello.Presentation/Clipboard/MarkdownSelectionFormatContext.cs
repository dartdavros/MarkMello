using MarkMello.Domain;

namespace MarkMello.Presentation.Clipboard;

internal sealed class MarkdownSelectionFormatContext
{
    private readonly MarkdownDocumentTextMap? _textMap;
    private readonly DocumentTextRange _selectionRange;

    private MarkdownSelectionFormatContext(MarkdownDocumentTextMap? textMap, DocumentTextRange selectionRange)
    {
        _textMap = textMap;
        _selectionRange = selectionRange;
    }

    public bool IsSelection => _textMap is not null;

    public static MarkdownSelectionFormatContext ForDocument() => new(null, DocumentTextRange.Empty);

    public static bool TryCreateForSelection(
        RenderedMarkdownDocument document,
        DocumentTextRange selectionRange,
        out MarkdownSelectionFormatContext context)
    {
        context = ForDocument();
        if (document.Blocks.Count == 0 || selectionRange.IsEmpty)
        {
            return false;
        }

        var textMap = MarkdownDocumentTextMap.Create(document);
        if (textMap.Text.Length == 0)
        {
            return false;
        }

        var start = Math.Clamp(selectionRange.Start, 0, textMap.Text.Length);
        var end = Math.Clamp(selectionRange.End, start, textMap.Text.Length);
        if (end <= start)
        {
            return false;
        }

        context = new MarkdownSelectionFormatContext(textMap, new DocumentTextRange(start, end));
        return true;
    }

    public bool TryGetFragmentLocalRange(string path, out DocumentTextRange? localRange)
    {
        if (!IsSelection)
        {
            localRange = null;
            return true;
        }

        if (TryGetLocalSelection(path, out _, out var selectedRange))
        {
            localRange = selectedRange;
            return true;
        }

        localRange = null;
        return false;
    }

    public bool TryGetFragmentText(string path, string text, out string selectedText)
    {
        if (!IsSelection)
        {
            selectedText = text;
            return selectedText.Length > 0;
        }

        if (!TryGetLocalSelection(path, out var fragment, out var selectedRange))
        {
            selectedText = string.Empty;
            return false;
        }

        selectedText = MarkdownClipboardTextHelpers.Slice(fragment.Text, selectedRange);
        return selectedText.Length > 0;
    }

    public bool TryGetLocalSelection(
        string path,
        out MarkdownDocumentTextFragment fragment,
        out DocumentTextRange localRange)
    {
        fragment = null!;
        localRange = DocumentTextRange.Empty;

        if (_textMap is null || !_textMap.TryGetFragment(path, out fragment))
        {
            return false;
        }

        var intersection = fragment.Range.Intersection(_selectionRange);
        if (intersection.IsEmpty)
        {
            return false;
        }

        localRange = new DocumentTextRange(
            intersection.Start - fragment.Range.Start,
            intersection.End - fragment.Range.Start);
        return true;
    }
}
