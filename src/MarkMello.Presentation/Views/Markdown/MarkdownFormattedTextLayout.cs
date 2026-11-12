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
        IReadOnlyDictionary<int, MarkdownInlineImageState>? inlineImages,
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
        var imageMetrics = MarkdownInlineImageMetrics.Create(
            baseFontFamily,
            baseFontSize,
            baseFontWeight,
            baseFontStyle,
            lineHeight,
            foreground);
        var mathMetrics = MarkdownMathMetrics.Create(baseFontFamily, baseFontSize, foreground, lineHeight);
        var source = new MarkdownTextSource(
            _displayModel,
            styledText.Images,
            styledText.Math,
            inlineImages ?? EmptyInlineImages,
            textProperties,
            padMetrics,
            imageMetrics,
            mathMetrics,
            maxWidth);
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

    public IReadOnlyList<MarkdownFormattedTextLineMetrics> GetLineMetrics()
    {
        if (_lines.Count == 0)
        {
            return Array.Empty<MarkdownFormattedTextLineMetrics>();
        }

        var result = new List<MarkdownFormattedTextLineMetrics>(_lines.Count);
        foreach (var line in _lines)
        {
            result.Add(new MarkdownFormattedTextLineMetrics(
                new Rect(
                    0,
                    line.Y,
                    Math.Max(1, line.TextLine.WidthIncludingTrailingWhitespace),
                    Math.Max(1, line.TextLine.Height))));
        }

        return result;
    }

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
        var displayCaret = hit.FirstCharacterIndex + hit.TrailingLength;
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

            // Length already includes trailing newline TextSource positions; adding NewLineLength
            // skips the next visual line and breaks multiline selection hit-testing.
            var consumed = line.Length;
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

    private static IReadOnlyDictionary<int, MarkdownInlineImageState> EmptyInlineImages { get; } =
        new Dictionary<int, MarkdownInlineImageState>();
}

internal readonly record struct MarkdownFormattedTextLineMetrics(Rect Bounds);

internal sealed class MarkdownTextSource : ITextSource
{
    private readonly MarkdownDisplayLayoutModel _displayModel;
    private readonly IReadOnlyList<MarkdownInlineImageSpan> _images;
    private readonly IReadOnlyList<MarkdownMathSpan> _math;
    private readonly IReadOnlyDictionary<int, MarkdownInlineImageState> _imageStates;
    private readonly MarkdownTextRunPropertiesFactory _propertiesFactory;
    private readonly MarkdownInlineCodePadMetrics _padMetrics;
    private readonly MarkdownInlineImageMetrics _imageMetrics;
    private readonly MarkdownMathMetrics _mathMetrics;

    public MarkdownTextSource(
        MarkdownDisplayLayoutModel displayModel,
        IReadOnlyList<MarkdownInlineImageSpan> images,
        IReadOnlyList<MarkdownMathSpan> math,
        IReadOnlyDictionary<int, MarkdownInlineImageState> imageStates,
        MarkdownTextRunPropertiesFactory propertiesFactory,
        MarkdownInlineCodePadMetrics padMetrics,
        MarkdownInlineImageMetrics imageMetrics,
        MarkdownMathMetrics mathMetrics,
        double maxWidth = 100_000)
    {
        _displayModel = displayModel;
        _images = images;
        _math = math;
        _imageStates = imageStates;
        _propertiesFactory = propertiesFactory;
        _padMetrics = padMetrics;
        _imageMetrics = imageMetrics;
        _mathMetrics = mathMetrics;
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
            MarkdownDisplaySegmentKind.Image => CreateImageRun(segment),
            MarkdownDisplaySegmentKind.Math => CreateMathRun(segment),
            MarkdownDisplaySegmentKind.CodePaddingLeft => MarkdownSpacerTextRun.Left(_padMetrics),
            MarkdownDisplaySegmentKind.CodePaddingRight => MarkdownSpacerTextRun.Right(_padMetrics),
            _ => new TextEndOfParagraph(1)
        };
    }

    private MarkdownMathTextRun CreateMathRun(MarkdownDisplaySegment segment)
    {
        if ((uint)segment.MathIndex >= (uint)_math.Count)
        {
            return MarkdownMathTextRun.Fallback(segment.Text, _mathMetrics);
        }

        var formula = _math[segment.MathIndex];
        return MarkdownMathTextRun.Create(formula, _mathMetrics);
    }

    private MarkdownInlineImageTextRun CreateImageRun(MarkdownDisplaySegment segment)
    {
        if ((uint)segment.ImageIndex >= (uint)_images.Count)
        {
            return MarkdownInlineImageTextRun.Placeholder(segment.Text, _imageMetrics, failed: true);
        }

        var image = _images[segment.ImageIndex];
        _imageStates.TryGetValue(image.Index, out var state);
        return MarkdownInlineImageTextRun.Create(image, state, _imageMetrics);
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
    double Baseline)
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
        var baseline = Math.Clamp(probe.Baseline, 0, height);
        return new MarkdownInlineCodePadMetrics(4, 4, height, baseline);
    }
}

internal readonly record struct MarkdownInlineImageMetrics(
    double MaxHeight,
    double Baseline,
    Typeface Typeface,
    double FontSize,
    IBrush Foreground)
{
    public static MarkdownInlineImageMetrics Create(
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        FontStyle fontStyle,
        double lineHeight,
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

        var normalizedLineHeight = double.IsNaN(lineHeight) || lineHeight <= 0
            ? Math.Max(fontSize * 1.25, probe.Height)
            : lineHeight;
        var maxHeight = Math.Max(12, normalizedLineHeight * 0.9);
        var baseline = Math.Clamp(
            probe.Baseline + Math.Max(0, (maxHeight - probe.Height) / 2),
            0,
            maxHeight);

        return new MarkdownInlineImageMetrics(
            maxHeight,
            baseline,
            new Typeface(fontFamily, fontStyle, fontWeight),
            Math.Max(10, fontSize * 0.75),
            foreground);
    }
}

internal sealed record MarkdownInlineImageState(IImage? Image, Stream? BackingStream, bool Failed)
{
    public static MarkdownInlineImageState FailedState { get; } = new(null, null, true);
}

internal sealed class MarkdownInlineImageTextRun : DrawableTextRun
{
    private const double PlaceholderHorizontalPadding = 8;
    private const double PlaceholderMinWidth = 34;
    private const double PlaceholderMaxWidth = 120;
    private static readonly IBrush DataImageBackground = new SolidColorBrush(Color.FromRgb(250, 250, 250));

    private readonly string _label;
    private readonly MarkdownInlineImageMetrics _metrics;
    private readonly IImage? _image;
    private readonly bool _failed;
    private readonly bool _drawImageBackground;
    private readonly Size _size;
    private readonly double _baseline;

    private MarkdownInlineImageTextRun(
        string label,
        MarkdownInlineImageMetrics metrics,
        IImage? image,
        bool failed,
        bool drawImageBackground)
    {
        _label = string.IsNullOrWhiteSpace(label) ? "image" : label;
        _metrics = metrics;
        _image = image;
        _failed = failed;
        _drawImageBackground = drawImageBackground;
        _size = ResolveSize(_label, metrics, image);
        _baseline = Math.Min(_size.Height, metrics.Baseline);
    }

    public static MarkdownInlineImageTextRun Create(
        MarkdownInlineImageSpan image,
        MarkdownInlineImageState? state,
        MarkdownInlineImageMetrics metrics)
        => new(
            image.PlaceholderText,
            metrics,
            state?.Image,
            state?.Failed == true,
            image.Url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase));

    public static MarkdownInlineImageTextRun Placeholder(string label, MarkdownInlineImageMetrics metrics, bool failed)
        => new(label, metrics, null, failed, drawImageBackground: false);

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    public override Size Size => _size;

    public override double Baseline => _baseline;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        var rect = new Rect(origin, _size);
        if (_image is not null)
        {
            if (_drawImageBackground)
            {
                drawingContext.DrawRectangle(DataImageBackground, null, rect, 2, 2);
            }

            drawingContext.DrawImage(_image, new Rect(_image.Size), rect);
            return;
        }

        var fill = _failed ? Brushes.Transparent : Brushes.LightGray;
        var pen = new Pen(Brushes.Gray, 1);
        drawingContext.DrawRectangle(fill, pen, rect, 3, 3);

        using var textLayout = new TextLayout(
            _failed ? "image" : _label,
            _metrics.Typeface,
            _metrics.FontSize,
            _metrics.Foreground,
            TextAlignment.Center,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: Math.Max(1, rect.Width - PlaceholderHorizontalPadding),
            maxHeight: Math.Max(1, rect.Height),
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        var textOrigin = new Point(
            rect.X + Math.Max(0, (rect.Width - textLayout.Width) / 2),
            rect.Y + Math.Max(0, (rect.Height - textLayout.Height) / 2));
        textLayout.Draw(drawingContext, textOrigin);
    }

    private static Size ResolveSize(string label, MarkdownInlineImageMetrics metrics, IImage? image)
    {
        if (image is not null)
        {
            var natural = image.Size;
            var scale = Math.Min(1d, metrics.MaxHeight / Math.Max(1, natural.Height));
            return new Size(
                Math.Max(1, natural.Width * scale),
                Math.Max(1, natural.Height * scale));
        }

        var width = Math.Clamp(
            label.Length * Math.Max(5, metrics.FontSize * 0.55) + PlaceholderHorizontalPadding * 2,
            PlaceholderMinWidth,
            PlaceholderMaxWidth);
        return new Size(width, Math.Max(14, metrics.MaxHeight * 0.85));
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

internal readonly record struct MarkdownMathMetrics(double FontSize, IBrush Foreground, double FallbackFontSize, double LineHeight, double Descent)
{
    public static MarkdownMathMetrics Create(FontFamily fontFamily, double baseFontSize, IBrush foreground, double lineHeight)
    {
        var descent = MeasureBodyDescent(fontFamily, baseFontSize);
        return new(
            Math.Max(10, baseFontSize),
            foreground,
            Math.Max(9, baseFontSize * 0.85),
            double.IsNaN(lineHeight) || lineHeight <= 0 ? baseFontSize * 1.5 : lineHeight,
            descent);
    }

    /// <summary>
    /// Body text descent for the current font size. An inline formula box does
    /// not carry a descent of its own, so the real value is measured from the
    /// same font the paragraph uses; guessing a ratio would mis-seat formulas
    /// whenever the reading font or size changes.
    /// </summary>
    private static double MeasureBodyDescent(FontFamily fontFamily, double fontSize)
    {
        using var probe = new TextLayout(
            "np",
            new Typeface(fontFamily),
            Math.Max(1, fontSize),
            Brushes.Black,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        return Math.Max(0, probe.Height - probe.Baseline);
    }
}

/// <summary>
/// A single text run that paints a LaTeX formula. Occupies one canonical
/// character (see <see cref="MarkdownMathSpan"/>), so the text layout, caret
/// mapping and selection stay unchanged — only the glyph at that position is
/// replaced by the rendered formula.
/// </summary>
internal sealed class MarkdownMathTextRun : DrawableTextRun
{
    private readonly string _latex;
    private readonly MarkdownMathMetrics _metrics;
    private readonly Size _size;
    private readonly double _baseline;
    private readonly MarkdownMathLayout? _layout;

    private MarkdownMathTextRun(string latex, MarkdownMathMetrics metrics)
    {
        _latex = latex;
        _metrics = metrics;
        _layout = MarkdownMathRenderer.Measure(latex, metrics.FontSize, metrics.Foreground, isDisplay: false);

        if (_layout is { IsValid: true } layout)
        {
            // Avalonia seats a drawable run with offsetY = (line baseline - run
            // baseline), so reporting the engine ascent as the run baseline lands
            // the math baseline exactly on the text baseline. Pad the box by the
            // text descent only so the formula's own below-baseline part (a
            // subscript, a fraction denominator) cannot be clipped by layout.
            var descender = _metrics.Descent;
            var boxHeight = Math.Max(layout.Size.Height, _metrics.FontSize) + descender;
            _size = new Size(layout.Size.Width, boxHeight);
            _baseline = Math.Clamp(layout.Baseline, 0, boxHeight);
        }
        else
        {
            // Unparseable formula: show the raw LaTeX so nothing is lost
            // (owner decision: fall back to source, not a hard error).
            _size = ResolveFallbackSize(latex, metrics);
            _baseline = _metrics.FontSize;
        }
    }

    public static MarkdownMathTextRun Create(MarkdownMathSpan formula, MarkdownMathMetrics metrics)
        => new(formula.Latex, metrics);

    public static MarkdownMathTextRun Fallback(string latex, MarkdownMathMetrics metrics)
        => new(latex, metrics);

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    public override Size Size => _size;

    public override double Baseline => _baseline;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        if (_layout is { IsValid: true } layout)
        {
            // origin is the top-left of the run's box; Draw expects the same,
            // and places the formula's top at that point. The run box is
            // taller than a text line for fractions, so Avalonia positions its
            // top relative to the run Baseline (see MarkdownMathTextRun.Baseline).
            MarkdownMathRenderer.Draw(
                drawingContext,
                _latex,
                layout.Size,
                origin,
                _metrics.FontSize,
                _metrics.Foreground,
                isDisplay: false);
            return;
        }

        DrawSourceFallback(drawingContext, origin);
    }

    private void DrawSourceFallback(DrawingContext drawingContext, Point origin)
    {
        using var textLayout = new TextLayout(
            _latex,
            new Typeface(new FontFamily("Consolas, Menlo, monospace")),
            _metrics.FallbackFontSize,
            _metrics.Foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: Math.Max(1, _size.Width),
            maxHeight: Math.Max(1, _size.Height),
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        textLayout.Draw(drawingContext, origin);
    }

    private static Size ResolveFallbackSize(string latex, MarkdownMathMetrics metrics)
    {
        using var probe = new TextLayout(
            latex.Length == 0 ? " " : latex,
            new Typeface(new FontFamily("Consolas, Menlo, monospace")),
            metrics.FallbackFontSize,
            metrics.Foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        return new Size(Math.Max(1, probe.Width), Math.Max(1, probe.Height));
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

internal sealed class MarkdownSpacerTextRun : DrawableTextRun{
    private readonly double _width;
    private readonly double _height;
    private readonly double _baseline;

    private MarkdownSpacerTextRun(double width, double height, double baseline)
    {
        _width = width;
        _height = height;
        _baseline = baseline;
    }

    public static MarkdownSpacerTextRun Left(MarkdownInlineCodePadMetrics metrics)
        => new(metrics.LeftWidth, metrics.Height, metrics.Baseline);

    public static MarkdownSpacerTextRun Right(MarkdownInlineCodePadMetrics metrics)
        => new(metrics.RightWidth, metrics.Height, metrics.Baseline);

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    public override Size Size => new(_width, _height);

    public override double Baseline => _baseline;

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
