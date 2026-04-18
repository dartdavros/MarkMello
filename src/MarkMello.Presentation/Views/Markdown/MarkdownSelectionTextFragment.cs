using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed class MarkdownSelectionTextFragment : Control, IDisposable
{
    private MarkdownStyledText _styledText = MarkdownStyledText.Empty;
    private DocumentTextRange _documentRange = DocumentTextRange.Empty;
    private DocumentTextRange _selectionRange = DocumentTextRange.Empty;
    private FontFamily _fontFamily = FontFamily.Default;
    private IBrush? _baseForeground;
    private double _letterSpacing;
    private double _fontSize = 16;
    private FontWeight _fontWeight = FontWeight.Normal;
    private FontStyle _fontStyle = FontStyle.Normal;
    private double _lineHeight = double.NaN;
    private TextLayout? _textLayout;
    private double _layoutWidth = double.NaN;
    private TextWrapping _textWrapping = TextWrapping.Wrap;

    public MarkdownSelectionTextFragment()
    {
        ClipToBounds = false;
        Focusable = false;
        UseLayoutRounding = true;
        Cursor = TryCreateCursor(StandardCursorType.Ibeam);

        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        ResourcesChanged += OnResourcesChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
    }

    public MarkdownStyledText StyledText
    {
        get => _styledText;
        set
        {
            _styledText = value ?? MarkdownStyledText.Empty;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

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

    public FontFamily BaseFontFamily
    {
        get => _fontFamily;
        set
        {
            _fontFamily = value;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public double BaseFontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public FontWeight BaseFontWeight
    {
        get => _fontWeight;
        set
        {
            _fontWeight = value;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public FontStyle BaseFontStyle
    {
        get => _fontStyle;
        set
        {
            _fontStyle = value;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public double BaseLineHeight
    {
        get => _lineHeight;
        set
        {
            _lineHeight = value;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public TextWrapping LayoutTextWrapping
    {
        get => _textWrapping;
        set
        {
            _textWrapping = value;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Optional override for the body text colour. When null, falls back to
    /// MmTextBrush from the theme. Links always render with this colour;
    /// their accent comes from the underline stroke only.
    /// </summary>
    public IBrush? BaseForeground
    {
        get => _baseForeground;
        set
        {
            if (ReferenceEquals(_baseForeground, value))
            {
                return;
            }

            _baseForeground = value;
            InvalidateTextLayout();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Letter spacing in pixels applied to the base text run. Per-span overrides
    /// (e.g. code) inherit it. Positive values expand, negative values tighten.
    /// </summary>
    public double BaseLetterSpacing
    {
        get => _letterSpacing;
        set
        {
            if (Math.Abs(_letterSpacing - value) < 0.01)
            {
                return;
            }

            _letterSpacing = value;
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = GetOrCreateTextLayout(availableSize.Width);
        var width = double.IsInfinity(availableSize.Width)
            ? layout.WidthIncludingTrailingWhitespace
            : Math.Min(availableSize.Width, Math.Ceiling(layout.WidthIncludingTrailingWhitespace));

        return new Size(width, Math.Ceiling(layout.Height));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var layout = GetOrCreateTextLayout(Bounds.Width);

        // Paint order (bottom -> top):
        //   1. Inline code "pill" backgrounds (rounded rect per span).
        //   2. Selection highlight (on top of code backgrounds, behind glyphs).
        //   3. Text glyphs themselves.
        DrawInlineCodeBackgrounds(context, layout);
        DrawSelection(context, layout);
        layout.Draw(context, default);
    }

    public int GetDocumentOffset(Point localPoint)
    {
        var localOffset = GetLocalTextOffset(localPoint, preferPreviousCharacterAtBoundary: false);
        return Math.Clamp(DocumentRange.Start + localOffset, DocumentRange.Start, DocumentRange.End);
    }

    public DocumentTextRange GetDocumentWordRange(Point localPoint)
    {
        if (StyledText.Text.Length == 0 || DocumentRange.IsEmpty)
        {
            return DocumentTextRange.Empty;
        }

        var localOffset = GetLocalTextOffset(localPoint, preferPreviousCharacterAtBoundary: true);
        if ((uint)localOffset >= (uint)StyledText.Text.Length)
        {
            return DocumentTextRange.Empty;
        }

        var localRange = MarkdownWordNavigator.GetWordRange(StyledText.Text, localOffset);
        return localRange.IsEmpty
            ? DocumentTextRange.Empty
            : new DocumentTextRange(DocumentRange.Start + localRange.Start, DocumentRange.Start + localRange.End);
    }

    public bool TryGetLinkAt(Point localPoint, out MarkdownLinkSpan linkSpan)
    {
        linkSpan = default;
        if (StyledText.Links.Count == 0)
        {
            return false;
        }

        var layout = GetOrCreateTextLayout(Math.Max(Bounds.Width, 1));
        var hit = layout.HitTestPoint(localPoint);
        if (!hit.IsInside)
        {
            return false;
        }

        var localOffset = GetLocalTextOffset(localPoint, preferPreviousCharacterAtBoundary: true);
        if ((uint)localOffset >= (uint)StyledText.Text.Length)
        {
            return false;
        }

        foreach (var candidate in StyledText.Links)
        {
            if (candidate.Range.Contains(localOffset))
            {
                linkSpan = candidate;
                return true;
            }
        }

        return false;
    }


    private int GetLocalTextOffset(Point localPoint, bool preferPreviousCharacterAtBoundary)
    {
        if (StyledText.Text.Length == 0)
        {
            return 0;
        }

        var layout = GetOrCreateTextLayout(Math.Max(Bounds.Width, 1));
        var hit = layout.HitTestPoint(localPoint);
        var localOffset = Math.Clamp(hit.TextPosition, 0, StyledText.Text.Length);
        if (preferPreviousCharacterAtBoundary && localOffset == StyledText.Text.Length && localOffset > 0)
        {
            localOffset--;
        }

        return localOffset;
    }

    private void DrawSelection(DrawingContext context, TextLayout layout)
    {
        var selection = DocumentRange.Intersection(SelectionRange);
        if (selection.IsEmpty)
        {
            return;
        }

        var localStart = Math.Clamp(selection.Start - DocumentRange.Start, 0, StyledText.Text.Length);
        var localEnd = Math.Clamp(selection.End - DocumentRange.Start, localStart, StyledText.Text.Length);
        var selectionLength = localEnd - localStart;
        if (selectionLength <= 0)
        {
            return;
        }

        var selectionBrush = ResolveOptionalBrush("MmSelectionBrush")
            ?? ResolveOptionalBrush("MmAccentSoftBrush")
            ?? Brushes.LightBlue;

        foreach (var rect in layout.HitTestTextRange(localStart, selectionLength))
        {
            context.FillRectangle(selectionBrush, rect);
        }
    }

    private TextLayout GetOrCreateTextLayout(double availableWidth)
    {
        var normalizedWidth = NormalizeLayoutWidth(availableWidth);
        if (_textLayout is not null && Math.Abs(_layoutWidth - normalizedWidth) < 0.5)
        {
            return _textLayout;
        }

        InvalidateTextLayout();
        _layoutWidth = normalizedWidth;
        _textLayout = new TextLayout(
            StyledText.Text,
            new Typeface(BaseFontFamily, BaseFontStyle, BaseFontWeight),
            BaseFontSize,
            ResolveBaseTextBrush(),
            TextAlignment.Left,
            LayoutTextWrapping,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: normalizedWidth,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.IsNaN(BaseLineHeight) ? double.NaN : BaseLineHeight,
            letterSpacing: _letterSpacing,
            maxLines: 0,
            textStyleOverrides: BuildStyleOverrides());

        return _textLayout;
    }

    private List<ValueSpan<TextRunProperties>>? BuildStyleOverrides()
    {
        if (StyledText.Spans.Count == 0)
        {
            return null;
        }

        var overrides = new List<ValueSpan<TextRunProperties>>(StyledText.Spans.Count);
        foreach (var span in StyledText.Spans)
        {
            var range = span.Range;
            if (range.IsEmpty)
            {
                continue;
            }

            var properties = new GenericTextRunProperties(
                CreateTypeface(span.Style),
                BaseFontSize,
                span.Style.IsLink ? BuildLinkTextDecorations() : null,
                // All spans (plain, bold, italic, link, code) share the base
                // body text colour. Link accent is the underline stroke, and
                // inline code's pill is painted in Render, not here, so no
                // per-rune background fill is needed.
                ResolveBaseTextBrush(),
                null,
                BaselineAlignment.Baseline,
                CultureInfo.CurrentUICulture);

            overrides.Add(new ValueSpan<TextRunProperties>(range.Start, range.Length, properties));
        }

        return overrides.Count == 0 ? null : overrides;
    }

    private Typeface CreateTypeface(MarkdownInlineStyleState style)
    {
        var family = style.IsCode
            ? ResolveInlineCodeFontFamily()
            : BaseFontFamily;

        var weight = style.IsBold ? FontWeight.Bold : BaseFontWeight;
        var fontStyle = style.IsItalic ? FontStyle.Italic : BaseFontStyle;
        return new Typeface(family, fontStyle, weight);
    }

    private FontFamily ResolveInlineCodeFontFamily()
    {
        if (this.TryFindResource("MmDocumentMonoFontFamily", ActualThemeVariant, out var value)
            && value is FontFamily family)
        {
            return family;
        }

        // Fallback stack is a subset of what MmDocumentMonoFontFamily declares.
        return new FontFamily("Cascadia Code, Consolas, Menlo, monospace");
    }

    /// <summary>
    /// Links keep the body text colour; their accent is expressed by a 1px
    /// underline drawn in MmAccentBrush, positioned a bit below the baseline.
    /// </summary>
    private TextDecorationCollection BuildLinkTextDecorations()
    {
        var stroke = ResolveOptionalBrush("MmAccentBrush") ?? ResolveBaseTextBrush();
        return new TextDecorationCollection
        {
            new TextDecoration
            {
                Location = TextDecorationLocation.Underline,
                Stroke = stroke,
                StrokeThickness = 1,
                StrokeThicknessUnit = TextDecorationUnit.Pixel,
                StrokeOffset = 2,
                StrokeOffsetUnit = TextDecorationUnit.Pixel,
            }
        };
    }

    /// <summary>
    /// Base text colour for this fragment. If an explicit BaseForeground is
    /// supplied (e.g. soft text inside a blockquote or a small heading),
    /// it wins. Otherwise we use the standard body-text brush.
    /// </summary>
    private IBrush ResolveBaseTextBrush()
        => BaseForeground ?? ResolveOptionalBrush("MmTextBrush") ?? Brushes.Black;

    private void DrawInlineCodeBackgrounds(DrawingContext context, TextLayout layout)
    {
        if (StyledText.Spans.Count == 0)
        {
            return;
        }

        var fill = ResolveOptionalBrush("MmCodeBackgroundBrush");
        if (fill is null)
        {
            return;
        }

        var borderBrush = ResolveOptionalBrush("MmCodeBorderBrush");
        var pen = borderBrush is null ? null : new Pen(borderBrush, 1);

        // Horizontal inflation mirrors design's 1px 6px padding on the pill.
        const double horizontalPad = 4;
        const double verticalPad = 0.5;
        const double cornerRadius = 4;

        foreach (var span in StyledText.Spans)
        {
            if (!span.Style.IsCode || span.Range.IsEmpty)
            {
                continue;
            }

            foreach (var rect in layout.HitTestTextRange(span.Range.Start, span.Range.Length))
            {
                var inflated = new Rect(
                    rect.X - horizontalPad,
                    rect.Y - verticalPad,
                    rect.Width + horizontalPad * 2,
                    rect.Height + verticalPad * 2);

                context.DrawRectangle(fill, pen, inflated, cornerRadius, cornerRadius);
            }
        }
    }

    private IBrush? ResolveOptionalBrush(string resourceKey)
    {
        return this.TryFindResource(resourceKey, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : null;
    }

    private static double NormalizeLayoutWidth(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || availableWidth <= 0)
        {
            return 1;
        }

        if (double.IsInfinity(availableWidth))
        {
            return 100_000;
        }

        return availableWidth;
    }

    public void Dispose()
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        ResourcesChanged -= OnResourcesChanged;
        AttachedToVisualTree -= OnAttachedToVisualTree;
        PointerMoved -= OnPointerMoved;
        PointerExited -= OnPointerExited;

        InvalidateTextLayout();
        GC.SuppressFinalize(this);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
        => InvalidateForAppearanceChange();

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e)
        => InvalidateForAppearanceChange();

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        => InvalidateForAppearanceChange();


    private void OnPointerMoved(object? sender, PointerEventArgs e)
        => Cursor = StyledText.Links.Count > 0 && TryGetLinkAt(e.GetPosition(this), out _)
            ? TryCreateCursor(StandardCursorType.Hand)
            : TryCreateCursor(StandardCursorType.Ibeam);

    private void OnPointerExited(object? sender, PointerEventArgs e)
        => Cursor = TryCreateCursor(StandardCursorType.Ibeam);

    private void InvalidateForAppearanceChange()
    {
        InvalidateTextLayout();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static Cursor? TryCreateCursor(StandardCursorType cursorType)
    {
        try
        {
            return new Cursor(cursorType);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void InvalidateTextLayout()
    {
        _textLayout?.Dispose();
        _textLayout = null;
        _layoutWidth = double.NaN;
    }
}
