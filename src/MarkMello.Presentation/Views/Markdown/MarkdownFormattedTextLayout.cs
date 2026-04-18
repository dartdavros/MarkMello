using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed class MarkdownFormattedTextLayout : IDisposable
{
    private readonly MarkdownDisplayLayoutModel _displayModel;
    private readonly MarkdownInlineCodePadMetrics _codePadMetrics;
    private readonly List<FormattedLine> _lines = new();

    public MarkdownFormattedTextLayout(
        MarkdownStyledText styledText,
        FontFamily baseFontFamily,
        FontFamily inlineCodeFontFamily,
        double baseFontSize,
        FontWeight baseFontWeight,
        FontStyle baseFontStyle,
        double lineHeight,
        double letterSpacing,
        TextWrapping textWrapping,
        double maxWidth,
        IBrush foreground,
        TextDecorationCollection? linkDecorations)
    {
        _displayModel = MarkdownDisplayLayoutModel.Create(styledText);
        var textProperties = new MarkdownTextRunPropertiesFactory(
            baseFontFamily,
            inlineCodeFontFamily,
            baseFontSize,
            baseFontWeight,
            baseFontStyle,
            foreground,
            linkDecorations);
        var padMetrics = MarkdownInlineCodePadMetrics.Create(
            inlineCodeFontFamily,
            baseFontSize * MarkdownTextRunPropertiesFactory.InlineCodeFontScale,
            baseFontWeight,
            baseFontStyle,
            foreground);
        _codePadMetrics = padMetrics;
        var source = new MarkdownTextSource(_displayModel, textProperties, padMetrics, maxWidth);
        var paragraphProperties = new GenericTextParagraphProperties(
            new GenericTextRunProperties(
                new Typeface(baseFontFamily, baseFontStyle, baseFontWeight),
                baseFontSize,
                textDecorations: null,
                foreground,
                backgroundBrush: null,
                BaselineAlignment.Baseline,
                CultureInfo.CurrentUICulture),
            TextAlignment.Left,
            textWrapping,
            lineHeight,
            letterSpacing);

        BuildLines(source, paragraphProperties);
    }

    public double WidthIncludingTrailingWhitespace { get; private set; }

    public double Height { get; private set; }

    public int CanonicalLength => _displayModel.CanonicalLength;

    public IReadOnlyList<MarkdownDisplayCodeBox> CodeBoxes => _displayModel.CodeBoxes;

    public void Draw(DrawingContext context)
    {
        foreach (var line in _lines)
        {
            line.TextLine.Draw(context, new Point(0, line.Y));
        }
    }

    public IReadOnlyList<Rect> GetCodeBoxRects(MarkdownDisplayCodeBox box)
    {
        if (box.DisplayLength <= 0 || _lines.Count == 0)
        {
            return Array.Empty<Rect>();
        }

        var displayEnd = box.DisplayStart + box.DisplayLength;
        var rects = new List<Rect>();

        foreach (var line in _lines)
        {
            var lineStart = line.TextLine.FirstTextSourceIndex;
            var lineEnd = lineStart + line.TextLine.Length;
            var overlapStart = Math.Max(box.DisplayStart, lineStart);
            var overlapEnd = Math.Min(displayEnd, lineEnd);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            foreach (var bounds in line.TextLine.GetTextBounds(overlapStart, overlapEnd - overlapStart))
            {
                var centeredY = line.Y + Math.Max(0, (line.TextLine.Height - _codePadMetrics.Height) / 2);
                rects.Add(new Rect(
                    bounds.Rectangle.X,
                    centeredY,
                    bounds.Rectangle.Width,
                    _codePadMetrics.Height));
            }
        }

        return rects;
    }

    public IReadOnlyList<Rect> GetSelectionRects(DocumentTextRange canonicalRange)
    {
        var canonicalStart = Math.Clamp(canonicalRange.Start, 0, CanonicalLength);
        var canonicalEnd = Math.Clamp(canonicalRange.End, canonicalStart, CanonicalLength);
        if (canonicalEnd <= canonicalStart)
        {
            return Array.Empty<Rect>();
        }

        var displayStart = _displayModel.GetDisplayStartForCanonicalCaret(canonicalStart);
        var displayEnd = _displayModel.GetDisplayEndForCanonicalCaret(canonicalEnd);
        return displayEnd <= displayStart
            ? Array.Empty<Rect>()
            : GetDisplayRects(displayStart, displayEnd - displayStart);
    }

    public int GetCanonicalCaretOffset(Point point)
    {
        if (_lines.Count == 0)
        {
            return 0;
        }

        var lineIndex = FindLineIndex(point.Y);
        var line = _lines[lineIndex];
        var localX = Math.Clamp(point.X, 0, Math.Max(line.TextLine.WidthIncludingTrailingWhitespace, 0));
        var hit = line.TextLine.GetCharacterHitFromDistance(localX);
        var displayCaret = line.TextLine.FirstTextSourceIndex + hit.FirstCharacterIndex + hit.TrailingLength;
        return _displayModel.GetCanonicalCaretForDisplayCaret(displayCaret);
    }

    public bool IsPointInsideText(Point point)
    {
        if (_lines.Count == 0)
        {
            return false;
        }

        var lineIndex = FindLineIndex(point.Y);
        var line = _lines[lineIndex];
        return point.Y >= line.Y
            && point.Y <= line.Y + line.TextLine.Height
            && point.X >= 0
            && point.X <= line.TextLine.WidthIncludingTrailingWhitespace;
    }

    public void Dispose()
    {
        foreach (var line in _lines)
        {
            line.TextLine.Dispose();
        }

        _lines.Clear();
    }

    private void BuildLines(MarkdownTextSource source, TextParagraphProperties paragraphProperties)
    {
        if (_displayModel.DisplayLength == 0)
        {
            return;
        }

        var formatter = TextFormatter.Current;
        TextLineBreak? previousLineBreak = null;
        var index = 0;
        var y = 0d;

        while (index < _displayModel.DisplayLength)
        {
            var line = formatter.FormatLine(source, index, source.MaxWidth, paragraphProperties, previousLineBreak);
            if (line is null)
            {
                break;
            }

            _lines.Add(new FormattedLine(line, y));
            WidthIncludingTrailingWhitespace = Math.Max(WidthIncludingTrailingWhitespace, line.WidthIncludingTrailingWhitespace);
            y += line.Height;

            var consumed = line.Length + line.NewLineLength;
            if (consumed <= 0)
            {
                break;
            }

            index = line.FirstTextSourceIndex + consumed;
            previousLineBreak = line.TextLineBreak;
        }

        Height = y;
    }

    private IReadOnlyList<Rect> GetDisplayRects(int displayStart, int displayLength)
    {
        if (displayLength <= 0 || _lines.Count == 0)
        {
            return Array.Empty<Rect>();
        }

        var displayEnd = displayStart + displayLength;
        var rects = new List<Rect>();

        foreach (var line in _lines)
        {
            var lineStart = line.TextLine.FirstTextSourceIndex;
            var lineEnd = lineStart + line.TextLine.Length;
            var overlapStart = Math.Max(displayStart, lineStart);
            var overlapEnd = Math.Min(displayEnd, lineEnd);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            foreach (var bounds in line.TextLine.GetTextBounds(overlapStart, overlapEnd - overlapStart))
            {
                rects.Add(bounds.Rectangle.Translate(new Vector(0, line.Y)));
            }
        }

        return rects;
    }

    private int FindLineIndex(double y)
    {
        if (y <= 0)
        {
            return 0;
        }

        for (var index = 0; index < _lines.Count; index++)
        {
            var line = _lines[index];
            if (y < line.Y + line.TextLine.Height || index == _lines.Count - 1)
            {
                return index;
            }
        }

        return _lines.Count - 1;
    }

    private readonly record struct FormattedLine(TextLine TextLine, double Y);
}

internal sealed class MarkdownTextSource : ITextSource
{
    private readonly MarkdownDisplayLayoutModel _displayModel;
    private readonly MarkdownTextRunPropertiesFactory _propertiesFactory;
    private readonly MarkdownInlineCodePadMetrics _padMetrics;

    public MarkdownTextSource(
        MarkdownDisplayLayoutModel displayModel,
        MarkdownTextRunPropertiesFactory propertiesFactory,
        MarkdownInlineCodePadMetrics padMetrics,
        double maxWidth = 100_000)
    {
        _displayModel = displayModel;
        _propertiesFactory = propertiesFactory;
        _padMetrics = padMetrics;
        MaxWidth = maxWidth;
    }

    public double MaxWidth { get; }

    public TextRun GetTextRun(int textSourceIndex)
    {
        if (textSourceIndex >= _displayModel.DisplayLength)
        {
            return new TextEndOfParagraph(1);
        }

        var segmentIndex = _displayModel.FindSegmentIndex(textSourceIndex);
        if (segmentIndex < 0)
        {
            return new TextEndOfParagraph(1);
        }

        var segment = _displayModel.Segments[segmentIndex];
        var localOffset = textSourceIndex - segment.DisplayStart;

        return segment.Kind switch
        {
            MarkdownDisplaySegmentKind.Text => new TextCharacters(
                segment.Text.AsMemory(localOffset),
                _propertiesFactory.Get(segment.Style)),
            MarkdownDisplaySegmentKind.LineBreak => new TextEndOfLine(1),
            MarkdownDisplaySegmentKind.CodePaddingLeft => MarkdownSpacerTextRun.Left(_padMetrics),
            MarkdownDisplaySegmentKind.CodePaddingRight => MarkdownSpacerTextRun.Right(_padMetrics),
            _ => new TextEndOfParagraph(1)
        };
    }
}

internal sealed class MarkdownTextRunPropertiesFactory
{
    internal const double InlineCodeFontScale = 0.92;

    private readonly Dictionary<MarkdownInlineStyleState, TextRunProperties> _cache = new();
    private readonly FontFamily _baseFontFamily;
    private readonly FontFamily _inlineCodeFontFamily;
    private readonly double _fontSize;
    private readonly FontWeight _fontWeight;
    private readonly FontStyle _fontStyle;
    private readonly IBrush _foreground;
    private readonly TextDecorationCollection? _linkDecorations;

    public MarkdownTextRunPropertiesFactory(
        FontFamily baseFontFamily,
        FontFamily inlineCodeFontFamily,
        double fontSize,
        FontWeight fontWeight,
        FontStyle fontStyle,
        IBrush foreground,
        TextDecorationCollection? linkDecorations)
    {
        _baseFontFamily = baseFontFamily;
        _inlineCodeFontFamily = inlineCodeFontFamily;
        _fontSize = fontSize;
        _fontWeight = fontWeight;
        _fontStyle = fontStyle;
        _foreground = foreground;
        _linkDecorations = linkDecorations;
    }

    public TextRunProperties Get(MarkdownInlineStyleState style)
    {
        if (_cache.TryGetValue(style, out var properties))
        {
            return properties;
        }

        var family = style.IsCode ? _inlineCodeFontFamily : _baseFontFamily;
        var fontSize = style.IsCode ? _fontSize * InlineCodeFontScale : _fontSize;
        var weight = style.IsBold ? FontWeight.Bold : _fontWeight;
        var fontStyle = style.IsItalic ? FontStyle.Italic : _fontStyle;
        properties = new GenericTextRunProperties(
            new Typeface(family, fontStyle, weight),
            fontSize,
            style.IsLink ? _linkDecorations : null,
            _foreground,
            backgroundBrush: null,
            BaselineAlignment.Baseline,
            CultureInfo.CurrentUICulture);
        _cache.Add(style, properties);
        return properties;
    }
}

internal readonly record struct MarkdownInlineCodePadMetrics(
    double LeftWidth,
    double RightWidth,
    double Height,
    double BaselineRatio)
{
    public static MarkdownInlineCodePadMetrics Create(
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        FontStyle fontStyle,
        IBrush foreground)
    {
        using var probe = new TextLayout(
            "M",
            new Typeface(fontFamily, fontStyle, fontWeight),
            fontSize,
            foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 0,
            textStyleOverrides: null);

        var height = Math.Max(1, probe.Height);
        var baselineRatio = Math.Clamp(probe.Baseline / height, 0, 1);
        return new MarkdownInlineCodePadMetrics(4, 4, height, baselineRatio);
    }
}

internal sealed class MarkdownSpacerTextRun : DrawableTextRun
{
    private readonly double _width;
    private readonly double _height;
    private readonly double _baselineRatio;

    private MarkdownSpacerTextRun(double width, double height, double baselineRatio)
    {
        _width = width;
        _height = height;
        _baselineRatio = baselineRatio;
    }

    public static MarkdownSpacerTextRun Left(MarkdownInlineCodePadMetrics metrics)
        => new(metrics.LeftWidth, metrics.Height, metrics.BaselineRatio);

    public static MarkdownSpacerTextRun Right(MarkdownInlineCodePadMetrics metrics)
        => new(metrics.RightWidth, metrics.Height, metrics.BaselineRatio);

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    public override Size Size => new(_width, _height);

    public override double Baseline => _baselineRatio;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
    }

    private sealed class EmptyTextRunProperties : TextRunProperties
    {
        public static EmptyTextRunProperties Instance { get; } = new();

        public override Typeface Typeface { get; } = new(Typeface.Default.FontFamily);

        public override double FontRenderingEmSize => 1;

        public override TextDecorationCollection? TextDecorations => null;

        public override IBrush? ForegroundBrush => null;

        public override IBrush? BackgroundBrush => null;

        public override BaselineAlignment BaselineAlignment => BaselineAlignment.Baseline;

        public override CultureInfo CultureInfo => CultureInfo.InvariantCulture;
    }
}
