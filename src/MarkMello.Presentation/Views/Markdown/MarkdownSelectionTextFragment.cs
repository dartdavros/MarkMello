using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed class MarkdownSelectionTextFragment : MarkdownDocumentSelectionFragmentBase
{
    private MarkdownStyledText _styledText = MarkdownStyledText.Empty;
    private readonly Dictionary<int, MarkdownInlineImageState> _inlineImages = [];
    private readonly HashSet<int> _pendingInlineImages = [];
    private CancellationTokenSource _imageLoadCts = new();
    private FontFamily _fontFamily = FontFamily.Default;
    private IImageSourceResolver? _imageSourceResolver;
    private string? _baseDirectory;
    private IBrush? _baseForeground;
    private double _letterSpacing;
    private double _fontSize = 16;
    private FontWeight _fontWeight = FontWeight.Normal;
    private FontStyle _fontStyle = FontStyle.Normal;
    private double _lineHeight = double.NaN;
    private MarkdownFormattedTextLayout? _textLayout;
    private double _layoutWidth = double.NaN;
    private TextWrapping _textWrapping = TextWrapping.Wrap;
    private bool _disposed;

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
            RestartInlineImageLoading();
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public IImageSourceResolver? ImageSourceResolver
    {
        get => _imageSourceResolver;
        set
        {
            if (ReferenceEquals(_imageSourceResolver, value))
            {
                return;
            }

            _imageSourceResolver = value;
            RestartInlineImageLoading();
        }
    }

    public string? BaseDirectory
    {
        get => _baseDirectory;
        set
        {
            if (string.Equals(_baseDirectory, value, StringComparison.Ordinal))
            {
                return;
            }

            _baseDirectory = value;
            RestartInlineImageLoading();
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
            : LayoutTextWrapping == TextWrapping.NoWrap
                ? Math.Min(availableSize.Width, Math.Ceiling(layout.WidthIncludingTrailingWhitespace))
                : availableSize.Width;

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
        layout.Draw(context);
    }

    internal void RenderMiniature(DrawingContext context)
    {
        var layout = GetOrCreateTextLayout(Bounds.Width);
        DrawInlineCodeBackgrounds(context, layout);
        layout.Draw(context);
    }


    public override int GetDocumentOffset(Point localPoint)
    {
        var localOffset = GetLocalTextOffset(localPoint, preferPreviousCharacterAtBoundary: false);
        return Math.Clamp(DocumentRange.Start + localOffset, DocumentRange.Start, DocumentRange.End);
    }

    public override DocumentTextRange GetDocumentWordRange(Point localPoint)
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

    public override bool TryGetLinkAt(Point localPoint, out MarkdownLinkSpan linkSpan)
    {
        linkSpan = default;
        if (StyledText.Links.Count == 0)
        {
            return false;
        }

        var layout = GetOrCreateTextLayout(Math.Max(Bounds.Width, 1));
        if (!layout.IsPointInsideText(localPoint))
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
        var localOffset = Math.Clamp(layout.GetCanonicalCaretOffset(localPoint), 0, StyledText.Text.Length);
        if (preferPreviousCharacterAtBoundary && localOffset == StyledText.Text.Length && localOffset > 0)
        {
            localOffset--;
        }

        return localOffset;
    }

    private void DrawSelection(DrawingContext context, MarkdownFormattedTextLayout layout)
    {
        var selection = DocumentRange.Intersection(SelectionRange);
        if (selection.IsEmpty)
        {
            return;
        }

        var selectionBrush = ResolveOptionalBrush("MmSelectionBrush")
            ?? ResolveOptionalBrush("MmAccentSoftBrush")
            ?? Brushes.LightBlue;

        foreach (var rect in layout.GetSelectionRects(new DocumentTextRange(
                     Math.Clamp(selection.Start - DocumentRange.Start, 0, StyledText.Text.Length),
                     Math.Clamp(selection.End - DocumentRange.Start, 0, StyledText.Text.Length))))
        {
            context.FillRectangle(selectionBrush, rect);
        }
    }

    private MarkdownFormattedTextLayout GetOrCreateTextLayout(double availableWidth)
    {
        var normalizedWidth = NormalizeLayoutWidth(availableWidth);
        if (_textLayout is not null && Math.Abs(_layoutWidth - normalizedWidth) < 0.5)
        {
            return _textLayout;
        }

        InvalidateTextLayout();
        _layoutWidth = normalizedWidth;
        _textLayout = new MarkdownFormattedTextLayout(
            StyledText,
            _inlineImages,
            BaseFontFamily,
            ResolveInlineCodeFontFamily(),
            BaseFontSize,
            BaseFontWeight,
            BaseFontStyle,
            double.IsNaN(BaseLineHeight) ? double.NaN : BaseLineHeight,
            _letterSpacing,
            LayoutTextWrapping,
            normalizedWidth,
            ResolveBaseTextBrush(),
            BuildLinkTextDecorations());

        return _textLayout;
    }

    private FontFamily ResolveInlineCodeFontFamily()
    {
        if (this.TryFindResource("MmDocumentMonoFontFamily", ActualThemeVariant, out var value)
            && value is FontFamily family)
        {
            return family;
        }

        return new FontFamily("JetBrains Mono, Cascadia Code, Consolas, Menlo, monospace");
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

    private void DrawInlineCodeBackgrounds(DrawingContext context, MarkdownFormattedTextLayout layout)
    {
        if (layout.CodeBoxes.Count == 0)
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

        const double cornerRadius = 3;

        foreach (var codeBox in layout.CodeBoxes)
        {
            foreach (var rect in layout.GetCodeBoxRects(codeBox))
            {
                context.DrawRectangle(fill, pen, rect, cornerRadius, cornerRadius);
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

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _imageLoadCts.Cancel();
        _imageLoadCts.Dispose();
        DisposeInlineImages();

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
    {
        InvalidateForAppearanceChange();
        EnsureInlineImagesLoaded();
    }


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

    private void RestartInlineImageLoading()
    {
        if (_disposed)
        {
            return;
        }

        _imageLoadCts.Cancel();
        _imageLoadCts.Dispose();
        _imageLoadCts = new CancellationTokenSource();
        DisposeInlineImages();
        _pendingInlineImages.Clear();

        InvalidateTextLayout();
        InvalidateMeasure();
        InvalidateVisual();
        EnsureInlineImagesLoaded();
    }

    private void EnsureInlineImagesLoaded()
    {
        if (_disposed || _imageSourceResolver is null || VisualRoot is null || StyledText.Images.Count == 0)
        {
            return;
        }

        foreach (var image in StyledText.Images)
        {
            if (_inlineImages.ContainsKey(image.Index) || _pendingInlineImages.Contains(image.Index))
            {
                continue;
            }

            _pendingInlineImages.Add(image.Index);
            _ = LoadInlineImageAsync(image, _imageLoadCts.Token);
        }
    }

    private async Task LoadInlineImageAsync(MarkdownInlineImageSpan image, CancellationToken cancellationToken)
    {
        try
        {
            var loaded = await MarkdownImageLoader
                .TryLoadAsync(_imageSourceResolver, image.Url, _baseDirectory, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested || _disposed)
            {
                if (loaded is { } canceledImage)
                {
                    MarkdownImageLoader.DisposeLoadedImage(canceledImage.Image, canceledImage.BackingStream);
                }
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => CompleteInlineImageLoad(image.Index, loaded));
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => CompleteInlineImageLoad(image.Index, null));
        }
    }

    private void CompleteInlineImageLoad(int index, (IImage Image, Stream BackingStream)? loaded)
    {
        if (_disposed)
        {
            if (loaded is { } disposedImage)
            {
                MarkdownImageLoader.DisposeLoadedImage(disposedImage.Image, disposedImage.BackingStream);
            }
            return;
        }

        _pendingInlineImages.Remove(index);
        _inlineImages[index] = loaded is null
            ? MarkdownInlineImageState.FailedState
            : new MarkdownInlineImageState(loaded.Value.Image, loaded.Value.BackingStream, Failed: false);

        InvalidateTextLayout();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void DisposeInlineImages()
    {
        foreach (var state in _inlineImages.Values)
        {
            MarkdownImageLoader.DisposeLoadedImage(state.Image, state.BackingStream);
        }

        _inlineImages.Clear();
    }
}
