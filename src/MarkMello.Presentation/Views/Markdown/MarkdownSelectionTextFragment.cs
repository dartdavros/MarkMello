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
            ResolveTextBrush(),
            TextAlignment.Left,
            LayoutTextWrapping,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: normalizedWidth,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.IsNaN(BaseLineHeight) ? double.NaN : BaseLineHeight,
            letterSpacing: 0,
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
                span.Style.IsLink ? TextDecorations.Underline : null,
                ResolveForegroundBrush(span.Style),
                ResolveBackgroundBrush(span.Style),
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

    private IBrush ResolveForegroundBrush(MarkdownInlineStyleState style)
        => style.IsLink
            ? ResolveOptionalBrush("MmAccentBrush") ?? ResolveTextBrush()
            : ResolveTextBrush();

    private IBrush? ResolveBackgroundBrush(MarkdownInlineStyleState style)
        => style.IsCode
            ? ResolveOptionalBrush("MmSurfaceBrush")
            : null;

    private IBrush ResolveTextBrush()
        => ResolveOptionalBrush("MmTextBrush") ?? Brushes.Black;

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
