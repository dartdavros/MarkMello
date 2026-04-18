using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed class MarkdownDisplayLayoutModel
{
    private const char LeftCodePaddingMarker = '\uE000';
    private const char RightCodePaddingMarker = '\uE001';

    private readonly int[] _displayCaretToCanonicalCaret;
    private readonly int[] _canonicalCaretToDisplayStart;
    private readonly int[] _canonicalCaretToDisplayEnd;

    private MarkdownDisplayLayoutModel(
        int canonicalLength,
        IReadOnlyList<MarkdownDisplaySegment> segments,
        IReadOnlyList<MarkdownDisplayCodeBox> codeBoxes,
        int[] displayCaretToCanonicalCaret,
        int[] canonicalCaretToDisplayStart,
        int[] canonicalCaretToDisplayEnd)
    {
        CanonicalLength = canonicalLength;
        Segments = segments;
        CodeBoxes = codeBoxes;
        _displayCaretToCanonicalCaret = displayCaretToCanonicalCaret;
        _canonicalCaretToDisplayStart = canonicalCaretToDisplayStart;
        _canonicalCaretToDisplayEnd = canonicalCaretToDisplayEnd;
    }

    public int CanonicalLength { get; }

    public int DisplayLength => _displayCaretToCanonicalCaret.Length - 1;

    public IReadOnlyList<MarkdownDisplaySegment> Segments { get; }

    public IReadOnlyList<MarkdownDisplayCodeBox> CodeBoxes { get; }

    public int GetDisplayStartForCanonicalCaret(int canonicalCaret)
        => _canonicalCaretToDisplayStart[Math.Clamp(canonicalCaret, 0, CanonicalLength)];

    public int GetDisplayEndForCanonicalCaret(int canonicalCaret)
        => _canonicalCaretToDisplayEnd[Math.Clamp(canonicalCaret, 0, CanonicalLength)];

    public int GetCanonicalCaretForDisplayCaret(int displayCaret)
        => _displayCaretToCanonicalCaret[Math.Clamp(displayCaret, 0, DisplayLength)];

    public int FindSegmentIndex(int displayIndex)
    {
        if (Segments.Count == 0)
        {
            return -1;
        }

        var low = 0;
        var high = Segments.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var candidate = Segments[mid];
            if (displayIndex < candidate.DisplayStart)
            {
                high = mid - 1;
                continue;
            }

            if (displayIndex >= candidate.DisplayEnd)
            {
                low = mid + 1;
                continue;
            }

            return mid;
        }

        return -1;
    }

    public static MarkdownDisplayLayoutModel Create(MarkdownStyledText styledText)
    {
        ArgumentNullException.ThrowIfNull(styledText);

        var builder = new Builder(styledText);
        return builder.Build();
    }

    private sealed class Builder
    {
        private readonly MarkdownStyledText _styledText;
        private readonly List<MarkdownDisplaySegment> _segments = new();
        private readonly List<MarkdownDisplayCodeBox> _codeBoxes = new();
        private readonly List<int> _displayCaretToCanonicalCaret = new() { 0 };
        private int _displayOffset;
        private int _canonicalOffset;

        public Builder(MarkdownStyledText styledText)
        {
            _styledText = styledText;
        }

        public MarkdownDisplayLayoutModel Build()
        {
            var text = _styledText.Text;
            var spans = _styledText.Spans;
            var spanIndex = 0;
            var index = 0;

            while (index < text.Length)
            {
                while (spanIndex < spans.Count && spans[spanIndex].Range.End <= index)
                {
                    spanIndex++;
                }

                MarkdownInlineStyleState style;
                var end = text.Length;
                if (spanIndex < spans.Count)
                {
                    var span = spans[spanIndex];
                    if (index < span.Range.Start)
                    {
                        style = MarkdownInlineStyleState.Default;
                        end = span.Range.Start;
                    }
                    else
                    {
                        style = span.Style;
                        end = span.Range.End;
                    }
                }
                else
                {
                    style = MarkdownInlineStyleState.Default;
                }

                if (end <= index)
                {
                    end = Math.Min(text.Length, index + 1);
                }

                if (style.IsCode)
                {
                    AppendCodeSegment(text[index..end], style);
                }
                else
                {
                    AppendTextSegments(text[index..end], style);
                }

                index = end;
            }

            var canonicalLength = _styledText.Text.Length;
            var canonicalCaretToDisplayStart = new int[canonicalLength + 1];
            var canonicalCaretToDisplayEnd = new int[canonicalLength + 1];
            Array.Fill(canonicalCaretToDisplayStart, -1);

            for (var displayCaret = 0; displayCaret < _displayCaretToCanonicalCaret.Count; displayCaret++)
            {
                var canonicalCaret = _displayCaretToCanonicalCaret[displayCaret];
                if (canonicalCaretToDisplayStart[canonicalCaret] < 0)
                {
                    canonicalCaretToDisplayStart[canonicalCaret] = displayCaret;
                }

                canonicalCaretToDisplayEnd[canonicalCaret] = displayCaret;
            }

            return new MarkdownDisplayLayoutModel(
                canonicalLength,
                _segments,
                _codeBoxes,
                _displayCaretToCanonicalCaret.ToArray(),
                canonicalCaretToDisplayStart,
                canonicalCaretToDisplayEnd);
        }

        private void AppendCodeSegment(string text, MarkdownInlineStyleState style)
        {
            var canonicalStart = _canonicalOffset;
            var displayStart = _displayOffset;

            AppendPadding(MarkdownDisplaySegmentKind.CodePaddingLeft, LeftCodePaddingMarker, style);
            AppendTextSegments(text, style);
            AppendPadding(MarkdownDisplaySegmentKind.CodePaddingRight, RightCodePaddingMarker, style);

            _codeBoxes.Add(new MarkdownDisplayCodeBox(
                new DocumentTextRange(canonicalStart, _canonicalOffset),
                displayStart,
                _displayOffset - displayStart));
        }

        private void AppendPadding(MarkdownDisplaySegmentKind kind, char marker, MarkdownInlineStyleState style)
        {
            _segments.Add(new MarkdownDisplaySegment(
                kind,
                _displayOffset,
                1,
                DocumentTextRange.Empty,
                marker.ToString(),
                style));

            _displayOffset++;
            _displayCaretToCanonicalCaret.Add(_canonicalOffset);
        }

        private void AppendTextSegments(string text, MarkdownInlineStyleState style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var chunkStart = 0;
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] != '\n')
                {
                    continue;
                }

                if (index > chunkStart)
                {
                    AppendTextRun(text[chunkStart..index], style);
                }

                AppendLineBreak();
                chunkStart = index + 1;
            }

            if (chunkStart < text.Length)
            {
                AppendTextRun(text[chunkStart..], style);
            }
        }

        private void AppendTextRun(string text, MarkdownInlineStyleState style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var canonicalRange = new DocumentTextRange(_canonicalOffset, _canonicalOffset + text.Length);
            _segments.Add(new MarkdownDisplaySegment(
                MarkdownDisplaySegmentKind.Text,
                _displayOffset,
                text.Length,
                canonicalRange,
                text,
                style));

            for (var index = 0; index < text.Length; index++)
            {
                _displayOffset++;
                _canonicalOffset++;
                _displayCaretToCanonicalCaret.Add(_canonicalOffset);
            }
        }

        private void AppendLineBreak()
        {
            var canonicalRange = new DocumentTextRange(_canonicalOffset, _canonicalOffset + 1);
            _segments.Add(new MarkdownDisplaySegment(
                MarkdownDisplaySegmentKind.LineBreak,
                _displayOffset,
                1,
                canonicalRange,
                "\n",
                MarkdownInlineStyleState.Default));

            _displayOffset++;
            _canonicalOffset++;
            _displayCaretToCanonicalCaret.Add(_canonicalOffset);
        }
    }
}

internal readonly record struct MarkdownDisplayCodeBox(
    DocumentTextRange CanonicalRange,
    int DisplayStart,
    int DisplayLength)
{
    public int DisplayEnd => DisplayStart + DisplayLength;
}

internal readonly record struct MarkdownDisplaySegment(
    MarkdownDisplaySegmentKind Kind,
    int DisplayStart,
    int DisplayLength,
    DocumentTextRange CanonicalRange,
    string Text,
    MarkdownInlineStyleState Style)
{
    public int DisplayEnd => DisplayStart + DisplayLength;
}

internal enum MarkdownDisplaySegmentKind
{
    Text,
    LineBreak,
    CodePaddingLeft,
    CodePaddingRight
}
