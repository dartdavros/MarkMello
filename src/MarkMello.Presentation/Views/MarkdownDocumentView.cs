using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Presentation.Clipboard;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.Views.Markdown;
using MarkMello.Presentation.Views.Markdown.Minimap;
using System.Globalization;
using System.Text;
using System.Threading;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Native Markdown renderer для viewer mode.
/// В этой итерации переносит selection ownership на document level и покрывает:
/// headings, paragraphs, quote paragraph content, list paragraph content,
/// code blocks и table cells.
/// </summary>
public sealed class MarkdownDocumentView : UserControl
{
    public static readonly StyledProperty<RenderedMarkdownDocument?> DocumentProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, RenderedMarkdownDocument?>(nameof(Document));

    public static readonly StyledProperty<Thickness> DocumentPaddingProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, Thickness>(
            nameof(DocumentPadding),
            new Thickness(0));

    public static readonly StyledProperty<ReadingPreferences> ReadingPreferencesProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, ReadingPreferences>(
            nameof(ReadingPreferences),
            ReadingPreferences.Default);

    public static readonly StyledProperty<IImageSourceResolver?> ImageSourceResolverProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, IImageSourceResolver?>(nameof(ImageSourceResolver));

    private const double DragSelectionThreshold = 4;
    private const double CodeBlockHorizontalScrollBarReserve = 16;
    private static readonly DataFormat<byte[]> WindowsHtmlClipboardFormat = DataFormat.CreateBytesPlatformFormat("HTML Format");
    private static readonly DataFormat<byte[]> HtmlClipboardFormat = DataFormat.CreateBytesPlatformFormat("text/html");

    private readonly StackPanel _root = new()
    {
        Orientation = Orientation.Vertical,
        Spacing = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly Border _viewport = new()
    {
        Background = Brushes.Transparent,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private readonly List<MarkdownDocumentSelectionFragmentBase> _selectionFragments = [];
    private readonly Dictionary<string, Control> _headingAnchorTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _headingAnchorCounts = new(StringComparer.Ordinal);
    private readonly List<MarkdownSourceLineVisualAnchor> _sourceLineAnchors = [];
    private MarkdownDocumentTextMap _textMap = MarkdownDocumentTextMap.Empty;
    private bool _isPointerPressed;
    private bool _isDraggingSelection;
    private Point _pointerPressOrigin;
    private MarkdownDocumentSelectionFragmentBase? _pressedFragment;
    private MarkdownLinkSpan? _pressedLink;
    private bool _preserveSelectionOnRelease;
    private MenuItem? _copyMenuItem;
    private MenuItem? _copyLinkMenuItem;
    private MenuItem? _copyTelegramMarkdownMenuItem;
    private MenuItem? _selectAllMenuItem;
    private MarkdownLinkSpan? _contextMenuLink;
    private CancellationTokenSource? _readingPreferencesRefreshCts;
    private long _renderGeneration;
    private bool _hasPendingRenderedNotification;

    static MarkdownDocumentView()
    {
        DocumentProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.Rebuild());
        DocumentPaddingProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.ApplyDocumentPadding());
        ReadingPreferencesProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.RefreshForReadingPreferencesChange());
    }

    public MarkdownDocumentView()
    {
        Focusable = true;
        IsTabStop = true;
        UseLayoutRounding = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        _root.UseLayoutRounding = true;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        EnsureRootTransitions();

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        KeyDown += OnKeyDown;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;

        // Suppress outer ScrollViewer auto-scroll that would otherwise happen
        // when this (document-sized) control becomes focused. The event
        // bubbles up from the Focus() call; we swallow it ourselves.
        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Bubble);

        _viewport.Child = _root;
        ApplyDocumentPadding();
        Content = _viewport;

        EnsureContextMenu();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        EnsureRootTransitions();
        EnsureContextMenu();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedAfterDocumentRebuild;
        _hasPendingRenderedNotification = false;
        _readingPreferencesRefreshCts?.Cancel();
        _readingPreferencesRefreshCts?.Dispose();
        _readingPreferencesRefreshCts = null;
    }

    private void EnsureRootTransitions()
    {
        if (_root.Transitions is not null || !Dispatcher.UIThread.CheckAccess())
        {
            return;
        }

        _root.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(140),
                Easing = new CubicEaseOut()
            }
        ];
    }

    private void EnsureContextMenu()
    {
        if (ContextMenu is not null || !Dispatcher.UIThread.CheckAccess())
        {
            return;
        }

        ContextMenu = BuildContextMenu();
    }

    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // If the request originates on this control itself (e.g. from focus
        // change during a selection gesture), there is nothing to bring into
        // view -- the document already is the scroll content. Allowing it to
        // bubble causes the ScrollViewer to jump to the top of our bounds.
        if (ReferenceEquals(e.TargetObject, this))
        {
            e.Handled = true;
        }
    }

    public RenderedMarkdownDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public ReadingPreferences ReadingPreferences
    {
        get => GetValue(ReadingPreferencesProperty);
        set => SetValue(ReadingPreferencesProperty, value);
    }

    public Thickness DocumentPadding
    {
        get => GetValue(DocumentPaddingProperty);
        set => SetValue(DocumentPaddingProperty, value);
    }

    public IImageSourceResolver? ImageSourceResolver
    {
        get => GetValue(ImageSourceResolverProperty);
        set => SetValue(ImageSourceResolverProperty, value);
    }

    public int? SelectionAnchor { get; private set; }

    public int SelectionStart { get; private set; }

    public int SelectionEnd { get; private set; }

    public bool HasSelection => SelectionEnd > SelectionStart;

    public string SelectedText => HasSelection
        ? _textMap.GetText(new DocumentTextRange(SelectionStart, SelectionEnd))
        : string.Empty;

    public event EventHandler? DocumentRendered;

    public event EventHandler? DocumentRenderInvalidated;

    public event EventHandler<MarkdownFileLinkRequestedEventArgs>? MarkdownFileLinkRequested;

    internal bool TryGetVerticalOffsetForSourceLine(int sourceLine, out double offsetY)
    {
        offsetY = 0;
        if (sourceLine < 0)
        {
            return false;
        }

        var anchors = CreateMeasuredSourceLineAnchors();
        if (anchors.Count == 0)
        {
            return false;
        }

        anchors.Sort(static (left, right) =>
        {
            var sourceComparison = left.StartLine.CompareTo(right.StartLine);
            return sourceComparison != 0
                ? sourceComparison
                : left.Y.CompareTo(right.Y);
        });

        var selectedIndex = 0;
        for (var index = 0; index < anchors.Count; index++)
        {
            if (anchors[index].StartLine > sourceLine)
            {
                break;
            }

            selectedIndex = index;

            if (sourceLine <= anchors[index].EndLine)
            {
                break;
            }
        }

        var selected = anchors[selectedIndex];
        offsetY = selected.Y;

        if (sourceLine > selected.StartLine
            && sourceLine <= selected.EndLine
            && selectedIndex + 1 < anchors.Count)
        {
            var next = anchors[selectedIndex + 1];
            var lineSpan = Math.Max(1, selected.EndLine - selected.StartLine);
            var visualSpan = Math.Max(0, next.Y - selected.Y);
            var ratio = Math.Clamp((double)(sourceLine - selected.StartLine) / lineSpan, 0, 1);
            offsetY = selected.Y + visualSpan * ratio;
        }

        offsetY = Math.Max(0, offsetY);
        return true;
    }

    internal bool TryGetSourceLineForVerticalOffset(double offsetY, out int sourceLine)
    {
        sourceLine = 0;
        var anchors = CreateMeasuredSourceLineAnchors();
        if (anchors.Count == 0)
        {
            return false;
        }

        var selectedIndex = 0;
        var normalizedOffset = Math.Max(0, offsetY);
        for (var index = 0; index < anchors.Count; index++)
        {
            if (anchors[index].Y > normalizedOffset)
            {
                break;
            }

            selectedIndex = index;
        }

        var selected = anchors[selectedIndex];
        sourceLine = selected.StartLine;

        if (selected.EndLine > selected.StartLine && selectedIndex + 1 < anchors.Count)
        {
            var next = anchors[selectedIndex + 1];
            var visualSpan = next.Y - selected.Y;
            if (visualSpan > 1)
            {
                var ratio = Math.Clamp((normalizedOffset - selected.Y) / visualSpan, 0, 1);
                var lineSpan = selected.EndLine - selected.StartLine;
                sourceLine = selected.StartLine + (int)Math.Round(lineSpan * ratio, MidpointRounding.AwayFromZero);
            }
        }

        sourceLine = Math.Clamp(sourceLine, selected.StartLine, selected.EndLine);
        return true;
    }

    // Test-only: enumerates registered source-line anchor spans without
    // requiring the view to be laid out. Edit-mode scroll synchronization
    // needs every block with a SourceSpan (including diagrams) to register
    // here so the editor cursor can map to the preview block. Layout-
    // measured offsets live behind <see cref="CreateMeasuredSourceLineAnchors"/>.
    internal IReadOnlyList<MarkdownSourceSpan> EnumerateRegisteredSourceSpans()
    {
        var spans = new List<MarkdownSourceSpan>(_sourceLineAnchors.Count);
        foreach (var anchor in _sourceLineAnchors)
        {
            spans.Add(anchor.SourceSpan);
        }
        return spans;
    }

    private List<MarkdownSourceLineAnchorSnapshot> CreateMeasuredSourceLineAnchors()
    {
        var result = new List<MarkdownSourceLineAnchorSnapshot>(_sourceLineAnchors.Count);

        foreach (var anchor in _sourceLineAnchors)
        {
            if (anchor.Control.Bounds.Width <= 0 || anchor.Control.Bounds.Height <= 0)
            {
                continue;
            }

            var origin = anchor.Control.TranslatePoint(new Point(0, 0), this);
            if (origin is null)
            {
                continue;
            }

            result.Add(new MarkdownSourceLineAnchorSnapshot(
                anchor.SourceSpan.StartLine,
                anchor.SourceSpan.EndLine,
                Math.Max(0, origin.Value.Y)));
        }

        result.Sort(static (left, right) =>
        {
            var visualComparison = left.Y.CompareTo(right.Y);
            return visualComparison != 0
                ? visualComparison
                : left.StartLine.CompareTo(right.StartLine);
        });

        return result;
    }

    internal DocumentMiniatureSnapshot CreateMiniatureSnapshot()
    {
        if (Document is null || Bounds.Height <= 0 || Bounds.Width <= 0)
        {
            return DocumentMiniatureSnapshot.Empty;
        }

        return new DocumentMiniatureSnapshot(
            totalWidth: Math.Max(1, Bounds.Width),
            totalHeight: Math.Max(1, Bounds.Height));
    }

    internal void RenderMiniature(DrawingContext context, Rect targetBounds)
    {
        var snapshot = CreateMiniatureSnapshot();
        if (snapshot.IsEmpty || targetBounds.Width <= 0 || targetBounds.Height <= 0)
        {
            return;
        }

        var scaleX = targetBounds.Width / snapshot.TotalWidth;
        var scaleY = targetBounds.Height / snapshot.TotalHeight;

        using (context.PushClip(targetBounds))
        {
            foreach (var border in _root.GetVisualDescendants().OfType<Border>().Where(IsMiniatureStructuralBorder))
            {
                DrawBorderDecorationMiniature(context, border, targetBounds, scaleX, scaleY);
            }

            foreach (var fragment in _selectionFragments)
            {
                DrawControlMiniature(context, fragment, targetBounds, scaleX, scaleY);
            }

            foreach (var imageView in _root.GetVisualDescendants().OfType<MarkdownImageView>())
            {
                DrawImagePlaceholderMiniature(context, imageView, targetBounds, scaleX, scaleY);
            }

            foreach (var rule in _root.GetVisualDescendants().OfType<Border>().Where(static border => border.Classes.Contains("mm-md-hr")))
            {
                DrawHorizontalRuleMiniature(context, rule, targetBounds, scaleX, scaleY);
            }
        }
    }

    private void DrawControlMiniature(
        DrawingContext context,
        Control control,
        Rect targetBounds,
        double scaleX,
        double scaleY)
    {
        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            return;
        }

        var origin = control.TranslatePoint(new Point(0, 0), this);
        if (origin is null)
        {
            return;
        }

        var matrix = new Matrix(
            scaleX,
            0,
            0,
            scaleY,
            targetBounds.X + origin.Value.X * scaleX,
            targetBounds.Y + origin.Value.Y * scaleY);

        using (context.PushTransform(matrix))
        {
            if (control is MarkdownSelectionTextFragment textFragment)
            {
                textFragment.RenderMiniature(context);
                return;
            }

            control.Render(context);
        }
    }

    private void DrawBorderDecorationMiniature(
        DrawingContext context,
        Border border,
        Rect targetBounds,
        double scaleX,
        double scaleY)
    {
        var bounds = TranslateControlBounds(border);
        if (bounds is null)
        {
            return;
        }

        var target = MapMiniatureRect(bounds.Value, targetBounds, scaleX, scaleY);
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }

        var background = border.Background;
        var borderBrush = border.BorderBrush;
        var pen = borderBrush is null || IsEmptyThickness(border.BorderThickness)
            ? null
            : new Pen(borderBrush, 1);

        if (background is null && pen is null)
        {
            return;
        }

        context.DrawRectangle(background, pen, target, 1.5, 1.5);
    }

    private void DrawImagePlaceholderMiniature(
        DrawingContext context,
        Control imageView,
        Rect targetBounds,
        double scaleX,
        double scaleY)
    {
        var bounds = TranslateControlBounds(imageView);
        if (bounds is null)
        {
            return;
        }

        var target = MapMiniatureRect(bounds.Value, targetBounds, scaleX, scaleY);
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }

        var fill = LookupBrush("MmSurfaceRaisedBrush") ?? LookupBrush("MmCodeBackgroundBrush") ?? Brushes.Transparent;
        var stroke = LookupBrush("MmBorderSubtleBrush") ?? LookupBrush("MmTextFaintBrush");
        context.DrawRectangle(fill, stroke is null ? null : new Pen(stroke, 1), target, 1.5, 1.5);
    }

    private void DrawHorizontalRuleMiniature(
        DrawingContext context,
        Control rule,
        Rect targetBounds,
        double scaleX,
        double scaleY)
    {
        var bounds = TranslateControlBounds(rule);
        if (bounds is null)
        {
            return;
        }

        var target = MapMiniatureRect(bounds.Value, targetBounds, scaleX, scaleY);
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }

        var brush = LookupBrush("MmBorderBrush") ?? LookupBrush("MmTextFaintBrush") ?? Brushes.Gray;
        context.DrawRectangle(brush, null, target);
    }

    private Rect? TranslateControlBounds(Control control)
    {
        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            return null;
        }

        var origin = control.TranslatePoint(new Point(0, 0), this);
        return origin is null
            ? null
            : new Rect(origin.Value, control.Bounds.Size);
    }

    private static bool IsMiniatureStructuralBorder(Border border)
        => border.Classes.Contains("mm-md-quote")
            || border.Classes.Contains("mm-md-codeblock")
            || border.Classes.Contains("mm-md-table")
            || border.Classes.Contains("mm-md-table-header-cell")
            || border.Classes.Contains("mm-md-table-cell");

    private static bool IsEmptyThickness(Thickness thickness)
        => thickness.Left <= 0 && thickness.Top <= 0 && thickness.Right <= 0 && thickness.Bottom <= 0;

    private static Rect MapMiniatureRect(Rect sourceBounds, Rect targetBounds, double scaleX, double scaleY)
        => new(
            targetBounds.X + sourceBounds.X * scaleX,
            targetBounds.Y + sourceBounds.Y * scaleY,
            sourceBounds.Width * scaleX,
            Math.Max(1, sourceBounds.Height * scaleY));

    public void SelectAll()
    {
        if (_textMap.Text.Length == 0)
        {
            ClearSelection();
            return;
        }

        SelectionAnchor = 0;
        SelectionStart = 0;
        SelectionEnd = _textMap.Text.Length;
        ApplySelectionToFragments();
    }

    public void ClearSelection()
    {
        SelectionAnchor = null;
        SelectionStart = 0;
        SelectionEnd = 0;
        ApplySelectionToFragments();
    }

    public void SelectRange(DocumentTextRange range)
    {
        if (_textMap.Text.Length == 0 || range.IsEmpty)
        {
            ClearSelection();
            return;
        }

        var start = Math.Clamp(range.Start, 0, _textMap.Text.Length);
        var end = Math.Clamp(range.End, start, _textMap.Text.Length);
        if (end <= start)
        {
            ClearSelection();
            return;
        }

        SelectionAnchor = start;
        SelectionStart = start;
        SelectionEnd = end;
        ApplySelectionToFragments();
    }

    private void Rebuild()
    {
        DocumentRenderInvalidated?.Invoke(this, EventArgs.Empty);
        DisposeSelectionFragments();
        _root.Children.Clear();
        _headingAnchorTargets.Clear();
        _headingAnchorCounts.Clear();
        _sourceLineAnchors.Clear();
        ResetPointerState();

        var document = Document;
        _textMap = document is null ? MarkdownDocumentTextMap.Empty : MarkdownDocumentTextMap.Create(document);
        ClearSelection();

        var generation = ++_renderGeneration;
        _hasPendingRenderedNotification = false;

        if (document is null || document.Blocks.Count == 0)
        {
            return;
        }

        for (var index = 0; index < document.Blocks.Count; index++)
        {
            _root.Children.Add(BuildBlock(document.Blocks[index], $"b{index}", nested: false));
        }

        QueueDocumentRenderedNotification(generation);
    }

    private void QueueDocumentRenderedNotification(long generation)
    {
        _hasPendingRenderedNotification = true;
        LayoutUpdated -= OnLayoutUpdatedAfterDocumentRebuild;
        LayoutUpdated += OnLayoutUpdatedAfterDocumentRebuild;

        Dispatcher.UIThread.Post(
            () => CompleteDocumentRenderedNotification(generation),
            DispatcherPriority.Render);
    }

    private void OnLayoutUpdatedAfterDocumentRebuild(object? sender, EventArgs e)
        => CompleteDocumentRenderedNotification(_renderGeneration);

    private void CompleteDocumentRenderedNotification(long generation)
    {
        if (!_hasPendingRenderedNotification || generation != _renderGeneration || Document is null)
        {
            return;
        }

        _hasPendingRenderedNotification = false;
        LayoutUpdated -= OnLayoutUpdatedAfterDocumentRebuild;
        DocumentRendered?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshForReadingPreferencesChange()
    {
        DocumentRenderInvalidated?.Invoke(this, EventArgs.Empty);
        _readingPreferencesRefreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _readingPreferencesRefreshCts = cts;

        _root.Opacity = 0.9;
        _ = AnimateReadingPreferencesRefreshAsync(cts.Token);
    }

    private async Task AnimateReadingPreferencesRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(48, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Rebuild();
        _root.Opacity = 1;
    }

    private void ApplyDocumentPadding()
    {
        _viewport.Padding = DocumentPadding;
    }

    private void DisposeSelectionFragments()
    {
        foreach (var fragment in _selectionFragments)
        {
            fragment.Dispose();
        }

        _selectionFragments.Clear();
    }

    private Control BuildBlock(MarkdownBlock block, string path, bool nested, bool insideQuote = false)
    {
        var control = block switch
        {
            MarkdownHeadingBlock heading => BuildHeading(heading, path),
            MarkdownParagraphBlock paragraph => BuildParagraph(paragraph, path, nested, insideQuote),
            MarkdownQuoteBlock quote => BuildQuote(quote, path),
            MarkdownListBlock list => BuildList(list, path, insideQuote),
            MarkdownHorizontalRuleBlock => BuildHorizontalRule(),
            MarkdownCodeBlock code => BuildCodeBlock(code, path),
            MarkdownTableBlock table => BuildTable(table, path),
            MarkdownImageBlock image => BuildImageBlock(image),
            MarkdownDiagramBlock diagram => BuildDiagramBlock(diagram),
            _ => BuildFallback(block)
        };

        RegisterSourceLineAnchor(block, control);
        return control;
    }

    private void RegisterSourceLineAnchor(MarkdownBlock block, Control control)
    {
        if (block.SourceSpan is not { } sourceSpan)
        {
            return;
        }

        _sourceLineAnchors.Add(new MarkdownSourceLineVisualAnchor(control, sourceSpan));
    }

    private static MarkdownDiagramBlockView BuildDiagramBlock(MarkdownDiagramBlock block)
        => new(block);

    private MarkdownImageView BuildImageBlock(MarkdownImageBlock block)
        => new(
            resolver: ImageSourceResolver,
            url: block.Url,
            altText: block.AltText,
            title: block.Title,
            width: block.Width,
            height: block.Height,
            baseDirectory: Document?.BaseDirectory)
        {
            Margin = new Thickness(0, 12, 0, 22),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 1200,
        };

    private Control BuildHeading(MarkdownHeadingBlock block, string path)
    {
        var fontSize = GetHeadingFontSize(block.Level);
        var lineHeight = Math.Max(fontSize * 1.25, fontSize + 4);
        var margin = block.Level == 1
            ? new Thickness(0, 0, 0, 10)
            : block.Level == 2
                ? new Thickness(0, 28, 0, 14)
                : new Thickness(0, 18, 0, 10);

        // Design: h1 -> 700, h2+ -> 600. Previous code had this inverted.
        var weight = block.Level == 1 ? FontWeight.Bold : FontWeight.SemiBold;

        // Tighter tracking at larger sizes, matching -0.025em / -0.02em / -0.01em.
        var letterSpacing = block.Level switch
        {
            1 => fontSize * -0.025,
            2 => fontSize * -0.02,
            3 => fontSize * -0.01,
            _ => 0d
        };

        // h5 / h6 render with the soft text colour in the design.
        var baseForeground = block.Level >= 5 ? LookupBrush("MmTextSoftBrush") : null;

        var headingControl = BuildSelectionFragment(
            path,
            block.Inlines,
            margin,
            fontSize,
            lineHeight,
            weight,
            FontStyle.Normal,
            fallbackClassName: "mm-md-heading",
            baseForeground: baseForeground,
            letterSpacing: letterSpacing);

        RegisterHeadingAnchor(block, headingControl);
        return headingControl;
    }

    private Control BuildParagraph(MarkdownParagraphBlock block, string path, bool nested, bool insideQuote)
    {
        var fontStyle = insideQuote ? FontStyle.Italic : FontStyle.Normal;
        var baseForeground = insideQuote ? LookupBrush("MmTextSoftBrush") : null;

        return BuildSelectionFragment(
            path,
            block.Inlines,
            nested ? new Thickness(0, 0, 0, 10) : new Thickness(0, 0, 0, 18),
            ReadingPreferences.FontSize,
            GetBodyLineHeight(),
            FontWeight.Normal,
            fontStyle,
            fallbackClassName: "mm-md-paragraph",
            baseForeground: baseForeground);
    }

    private Border BuildQuote(MarkdownQuoteBlock block, string path)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        for (var index = 0; index < block.Blocks.Count; index++)
        {
            // Every descendant of this quote receives insideQuote: true so
            // nested lists and paragraphs pick up the italic/soft treatment.
            stack.Children.Add(BuildBlock(block.Blocks[index], $"{path}.b{index}", nested: true, insideQuote: true));
        }

        return new Border
        {
            Classes = { "mm-md-quote" },
            Child = stack
        };
    }

    private StackPanel BuildList(MarkdownListBlock block, string path, bool insideQuote = false)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 18)
        };

        for (var index = 0; index < block.Items.Count; index++)
        {
            panel.Children.Add(BuildListItem(block, block.Items[index], index, $"{path}.i{index}", insideQuote));
        }

        return panel;
    }

    private Grid BuildListItem(MarkdownListBlock list, MarkdownListItem item, int index, string path, bool insideQuote = false)
    {
        var bullet = BuildSelectionFragment(
            $"{path}.m",
            [new MarkdownTextInline(list.IsOrdered ? $"{index + 1}. " : "• ")],
            margin: default,
            ReadingPreferences.FontSize,
            GetBodyLineHeight(),
            FontWeight.Normal,
            FontStyle.Normal,
            fallbackClassName: "mm-md-list-bullet",
            textWrapping: TextWrapping.NoWrap);

        bullet.VerticalAlignment = VerticalAlignment.Top;

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        for (var blockIndex = 0; blockIndex < item.Blocks.Count; blockIndex++)
        {
            content.Children.Add(BuildBlock(item.Blocks[blockIndex], $"{path}.b{blockIndex}", nested: true, insideQuote: insideQuote));
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new(GridLength.Auto),
                new(new GridLength(1, GridUnitType.Star))
            },
            ColumnSpacing = 12
        };

        Grid.SetColumn(bullet, 0);
        Grid.SetColumn(content, 1);
        row.Children.Add(bullet);
        row.Children.Add(content);
        return row;
    }

    private static Grid BuildHorizontalRule()
    {
        // Design: 40% wide, horizontally centered. Avalonia has no percentage
        // widths, so we model it as a three-column grid in 3*,4*,3* ratio with
        // the rule in the middle column.
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,4*,3*"),
            Margin = new Thickness(0, 32, 0, 32),
        };
        var line = new Border { Classes = { "mm-md-hr" } };
        Grid.SetColumn(line, 1);
        grid.Children.Add(line);
        return grid;
    }

    private Border BuildCodeBlock(MarkdownCodeBlock block, string path)
    {
        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8
        };

        if (!string.IsNullOrWhiteSpace(block.Info))
        {
            body.Children.Add(new TextBlock
            {
                Text = block.Info,
                UseLayoutRounding = true,
                Classes = { "mm-md-code-info" }
            });
        }

        var codeFragment = BuildSelectionFragment(
            path,
            [new MarkdownTextInline(block.Code)],
            margin: default,
            fontSize: Math.Max(12, ReadingPreferences.FontSize - 2),
            lineHeight: Math.Max(16, (ReadingPreferences.FontSize - 2) * 1.5),
            fontWeight: FontWeight.Normal,
            fontStyle: FontStyle.Normal,
            fallbackClassName: "mm-md-codeblock-text",
            baseFontFamily: ResolveMonoFontFamily(),
            textWrapping: TextWrapping.NoWrap);

        body.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                Padding = new Thickness(0, 0, 0, CodeBlockHorizontalScrollBarReserve),
                Child = codeFragment
            }
        });

        var contentGrid = new Grid();
        contentGrid.Children.Add(body);

        var copyButton = CreateCodeCopyButton(block.Code);
        copyButton.HorizontalAlignment = HorizontalAlignment.Right;
        copyButton.VerticalAlignment = VerticalAlignment.Top;
        copyButton.Margin = new Thickness(0, 0, 0, 0);
        contentGrid.Children.Add(copyButton);

        return new Border
        {
            Classes = { "mm-md-codeblock" },
            Child = contentGrid
        };
    }

    private Button CreateCodeCopyButton(string code)
    {
        var button = new Button
        {
            Classes = { "mm-code-copy-button" },
            Content = GetLocalizedString("ContextCopy", "Copy"),
            IsTabStop = true
        };

        ToolTip.SetTip(button, GetLocalizedString("CodeCopyTooltip", "Copy code"));
        button.Click += async (_, e) =>
        {
            await CopyTextToClipboardAsync(code).ConfigureAwait(true);
            e.Handled = true;
        };

        return button;
    }

    private Control BuildTable(MarkdownTableBlock table, string path)
    {
        var columnCount = Math.Max(
            table.Header.Count,
            table.Rows.Count == 0 ? 0 : table.Rows.Max(static row => row.Count));

        if (columnCount == 0)
        {
            return BuildFallback(table);
        }

        var grid = new Grid
        {
            ColumnSpacing = 0,
            RowSpacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        var totalRows = table.Rows.Count + (table.Header.Count > 0 ? 1 : 0);
        for (var rowIndex = 0; rowIndex < totalRows; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Design `.mm-table` switches to the sans stack at 0.92em of body.
        var sansFontFamily = LookupFontFamily("MmDocumentSansFontFamily");
        var bodyCellFontSize = ReadingPreferences.FontSize * 0.92;
        var headerCellFontSize = ReadingPreferences.FontSize * 0.85;

        // Index of the last *data* row (not the header). Used to suppress
        // the trailing bottom border so the table does not end on a line.
        var lastDataRowIndex = table.Rows.Count > 0 ? totalRows - 1 : -1;

        var currentRow = 0;
        if (table.Header.Count > 0)
        {
            AddTableRow(
                grid, table.Header, currentRow,
                isHeader: true, isLastDataRow: false,
                pathPrefix: $"{path}.h",
                fontFamily: sansFontFamily,
                headerFontSize: headerCellFontSize,
                bodyFontSize: bodyCellFontSize);
            currentRow++;
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            AddTableRow(
                grid, table.Rows[rowIndex], currentRow,
                isHeader: false, isLastDataRow: currentRow == lastDataRowIndex,
                pathPrefix: $"{path}.r{rowIndex}.c",
                fontFamily: sansFontFamily,
                headerFontSize: headerCellFontSize,
                bodyFontSize: bodyCellFontSize);
            currentRow++;
        }

        return new Border
        {
            Classes = { "mm-md-table" },
            Child = grid,
            // Design `.mm-table` margin is 1.4em top and bottom.
            Margin = new Thickness(0, (int)(ReadingPreferences.FontSize * 1.4), 0, (int)(ReadingPreferences.FontSize * 1.4))
        };
    }

    private void AddTableRow(
        Grid grid,
        IReadOnlyList<MarkdownTableCell> cells,
        int rowIndex,
        bool isHeader,
        bool isLastDataRow,
        string pathPrefix,
        FontFamily fontFamily,
        double headerFontSize,
        double bodyFontSize)
    {
        for (var columnIndex = 0; columnIndex < grid.ColumnDefinitions.Count; columnIndex++)
        {
            var cell = columnIndex < cells.Count
                ? cells[columnIndex]
                : new MarkdownTableCell(Array.Empty<MarkdownInline>());

            Control content;
            if (isHeader)
            {
                // Design: 0.85em size, semibold, soft colour, 0.05em letter-spacing.
                // Note: design also specifies "text-transform: uppercase", which
                // Avalonia does not support without mutating the characters
                // themselves (and breaking copy semantics). We therefore keep
                // the original case and approximate the visual weight via
                // letter-spacing + soft colour + smaller size.
                content = BuildSelectionFragment(
                    $"{pathPrefix}{columnIndex}",
                    cell.Inlines,
                    margin: default,
                    fontSize: headerFontSize,
                    lineHeight: Math.Max(headerFontSize * 1.45, headerFontSize + 4),
                    fontWeight: FontWeight.SemiBold,
                    fontStyle: FontStyle.Normal,
                    fallbackClassName: "mm-md-table-header",
                    baseFontFamily: fontFamily,
                    baseForeground: LookupBrush("MmTextSoftBrush"),
                    letterSpacing: headerFontSize * 0.05);
            }
            else
            {
                content = BuildSelectionFragment(
                    $"{pathPrefix}{columnIndex}",
                    cell.Inlines,
                    margin: default,
                    fontSize: bodyFontSize,
                    lineHeight: Math.Max(bodyFontSize * 1.55, bodyFontSize + 4),
                    fontWeight: FontWeight.Normal,
                    fontStyle: FontStyle.Normal,
                    fallbackClassName: "mm-md-table-text",
                    baseFontFamily: fontFamily);
            }

            var border = new Border
            {
                Classes = { isHeader ? "mm-md-table-header-cell" : "mm-md-table-cell" },
                Child = content
            };

            if (!isHeader && isLastDataRow)
            {
                // Suppresses the border-bottom on the final row so the table
                // does not terminate on an orphan divider line.
                border.Classes.Add("mm-md-table-cell-last");
            }

            Grid.SetRow(border, rowIndex);
            Grid.SetColumn(border, columnIndex);
            grid.Children.Add(border);
        }
    }

    private TextBlock BuildFallback(MarkdownBlock block)
    {
        return new TextBlock
        {
            Text = MarkdownDocumentTextMap.ExtractPlainText(block),
            Classes = { "mm-md-paragraph" },
            FontFamily = ResolveBodyFontFamily(),
            FontSize = ReadingPreferences.FontSize,
            LineHeight = GetBodyLineHeight(),
            TextWrapping = TextWrapping.Wrap,
            UseLayoutRounding = true
        };
    }

    private Control BuildSelectionFragment(
        string path,
        IReadOnlyList<MarkdownInline> inlines,
        Thickness margin,
        double fontSize,
        double lineHeight,
        FontWeight fontWeight,
        FontStyle fontStyle,
        string fallbackClassName,
        FontFamily? baseFontFamily = null,
        TextWrapping textWrapping = TextWrapping.Wrap,
        IBrush? baseForeground = null,
        double letterSpacing = 0)
    {
        var styled = MarkdownStyledText.FromInlines(inlines);
        if (styled.Text.Length == 0)
        {
            return new Border
            {
                Height = 0,
                Margin = margin
            };
        }

        var resolvedFontFamily = baseFontFamily ?? ResolveBodyFontFamily();
        if (!_textMap.TryGetFragment(path, out var fragment))
        {
            var fallback = new TextBlock
            {
                Text = styled.Text,
                Margin = margin,
                FontFamily = resolvedFontFamily,
                FontSize = fontSize,
                FontWeight = fontWeight,
                FontStyle = fontStyle,
                LineHeight = lineHeight,
                LetterSpacing = letterSpacing,
                TextWrapping = textWrapping,
                UseLayoutRounding = true,
                Classes = { fallbackClassName }
            };

            if (baseForeground is not null)
            {
                fallback.Foreground = baseForeground;
            }

            return fallback;
        }

        if (MarkdownImageFlowFragment.TryCreate(inlines, out var imageItems))
        {
            var imageFlow = new MarkdownImageFlowFragment(imageItems)
            {
                Margin = margin,
                DocumentRange = fragment.Range,
                ImageSourceResolver = ImageSourceResolver,
                BaseDirectory = Document?.BaseDirectory,
                BaseFontFamily = resolvedFontFamily,
                BaseFontSize = fontSize,
                BaseLineHeight = lineHeight
            };

            imageFlow.Classes.Add(fallbackClassName);
            _selectionFragments.Add(imageFlow);
            imageFlow.SelectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
            return imageFlow;
        }

        var control = new MarkdownSelectionTextFragment
        {
            Margin = margin,
            StyledText = styled,
            DocumentRange = fragment.Range,
            BaseFontFamily = resolvedFontFamily,
            BaseFontSize = fontSize,
            BaseFontWeight = fontWeight,
            BaseFontStyle = fontStyle,
            BaseLineHeight = lineHeight,
            BaseForeground = baseForeground,
            BaseLetterSpacing = letterSpacing,
            LayoutTextWrapping = textWrapping,
            Cursor = TryCreateCursor(StandardCursorType.Ibeam)
        };

        control.Classes.Add(fallbackClassName);
        _selectionFragments.Add(control);
        control.SelectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
        return control;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsPointerInputFromScrollBarChrome(e.Source))
        {
            return;
        }

        if (IsPointerInputFromCodeCopyButton(e.Source))
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(this);
        if (currentPoint.Properties.IsRightButtonPressed)
        {
            _contextMenuLink = TryResolveLinkAtDocumentPoint(e.GetPosition(this), out var link)
                ? link
                : null;
            return;
        }

        if (!TryResolveFragment(e.GetPosition(this), out var fragment, out var localPosition))
        {
            return;
        }

        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Focus via NavigationMethod.Pointer so the act of starting a selection
        // does not raise RequestBringIntoView and make the ScrollViewer jump.
        Focus(NavigationMethod.Pointer);

        if (e.ClickCount >= 3)
        {
            CommitSelection(fragment.DocumentRange, preserveOnRelease: true);
            BeginPointerSession(e, fragment, localPosition, allowLinkActivation: false);
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            var wordRange = fragment.GetDocumentWordRange(localPosition);
            if (!wordRange.IsEmpty)
            {
                CommitSelection(wordRange, preserveOnRelease: true);
                BeginPointerSession(e, fragment, localPosition, allowLinkActivation: false);
                e.Handled = true;
                return;
            }
        }

        _isPointerPressed = true;
        _isDraggingSelection = false;
        _preserveSelectionOnRelease = false;
        _pointerPressOrigin = e.GetPosition(this);
        _pressedFragment = fragment;
        _pressedLink = fragment.TryGetLinkAt(localPosition, out var pressedLink)
            ? pressedLink
            : null;

        var anchor = fragment.GetDocumentOffset(localPosition);
        SelectionAnchor = anchor;
        SelectionStart = anchor;
        SelectionEnd = anchor;
        ApplySelectionToFragments();

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerPressed || SelectionAnchor is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (!_isDraggingSelection && Point.Distance(position, _pointerPressOrigin) < DragSelectionThreshold)
        {
            return;
        }

        _isDraggingSelection = true;
        var offset = ResolveDocumentOffset(position);
        SetSelection(SelectionAnchor.Value, offset);
        e.Handled = true;
    }

    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerPressed)
        {
            return;
        }

        await TryActivatePressedLinkAsync(e);

        if (!_isDraggingSelection && !_preserveSelectionOnRelease)
        {
            ClearSelection();
        }

        ResetPointerState();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ResetPointerState();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && HasSelection)
        {
            ClearSelection();
            e.Handled = true;
            return;
        }

        if (!HasCommandModifier(e.KeyModifiers))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.A:
                SelectAll();
                e.Handled = true;
                break;

            case Key.C:
                if (HasSelection)
                {
                    await CopySelectionToClipboardAsync();
                    e.Handled = true;
                }
                break;
        }
    }

    private void SetSelection(int firstOffset, int secondOffset)
    {
        var range = DocumentTextRange.FromBounds(firstOffset, secondOffset);
        SelectionStart = range.Start;
        SelectionEnd = range.End;
        ApplySelectionToFragments();
    }

    private void ApplySelectionToFragments()
    {
        var range = new DocumentTextRange(SelectionStart, SelectionEnd);
        foreach (var fragment in _selectionFragments)
        {
            fragment.SelectionRange = range;
        }
    }

    private async Task CopySelectionToClipboardAsync()
    {
        await CopyTextToClipboardAsync(SelectedText).ConfigureAwait(true);
    }

    private async Task CopyTextToClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await ClipboardExtensions.SetTextAsync(
            clipboard,
            text.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }

    private ContextMenu BuildContextMenu()
    {
        _copyMenuItem = new MenuItem
        {
            Header = GetLocalizedString("ContextCopy", "Copy"),
            InputGesture = new KeyGesture(Key.C, KeyModifiers.Control)
        };
        _copyMenuItem.Click += OnCopyMenuItemClick;

        _copyLinkMenuItem = new MenuItem
        {
            Header = GetLocalizedString("ContextCopyLink", "Copy link")
        };
        _copyLinkMenuItem.Click += OnCopyLinkMenuItemClick;

        _copyTelegramMarkdownMenuItem = new MenuItem
        {
            Header = GetLocalizedString("ContextCopyTelegramMarkdown", "Copy selection as Telegram Markdown")
        };
        _copyTelegramMarkdownMenuItem.Click += OnCopyTelegramMarkdownMenuItemClick;

        _selectAllMenuItem = new MenuItem
        {
            Header = GetLocalizedString("ContextSelectAll", "Select all"),
            InputGesture = new KeyGesture(Key.A, KeyModifiers.Control)
        };
        _selectAllMenuItem.Click += OnSelectAllMenuItemClick;

        var menu = new ContextMenu();
        menu.Items.Add(_copyMenuItem);
        menu.Items.Add(_copyLinkMenuItem);
        menu.Items.Add(_copyTelegramMarkdownMenuItem);
        menu.Items.Add(_selectAllMenuItem);
        menu.Opening += OnContextMenuOpening;
        menu.Closed += (_, _) => _contextMenuLink = null;
        return menu;
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        UpdateContextMenuHeaders();

        // Enable Copy only when there is a selection.
        // Enable Select All only when there is any text to select.
        if (_copyMenuItem is not null)
        {
            _copyMenuItem.IsEnabled = HasSelection;
        }

        if (_copyLinkMenuItem is not null)
        {
            _copyLinkMenuItem.IsEnabled = _contextMenuLink.HasValue;
        }

        if (_copyTelegramMarkdownMenuItem is not null)
        {
            _copyTelegramMarkdownMenuItem.IsEnabled = HasSelection && Document is { Blocks.Count: > 0 };
        }

        if (_selectAllMenuItem is not null)
        {
            _selectAllMenuItem.IsEnabled = _textMap.Text.Length > 0;
        }
    }

    private void UpdateContextMenuHeaders()
    {
        if (_copyMenuItem is not null)
        {
            _copyMenuItem.Header = GetLocalizedString("ContextCopy", "Copy");
        }

        if (_copyLinkMenuItem is not null)
        {
            _copyLinkMenuItem.Header = GetLocalizedString("ContextCopyLink", "Copy link");
        }

        if (_copyTelegramMarkdownMenuItem is not null)
        {
            _copyTelegramMarkdownMenuItem.Header = GetLocalizedString(
                "ContextCopyTelegramMarkdown",
                "Copy selection as Telegram Markdown");
        }

        if (_selectAllMenuItem is not null)
        {
            _selectAllMenuItem.Header = GetLocalizedString("ContextSelectAll", "Select all");
        }
    }

    private async void OnCopyMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (!HasSelection)
        {
            return;
        }

        await CopySelectionToClipboardAsync();
    }

    private void OnSelectAllMenuItemClick(object? sender, RoutedEventArgs e)
    {
        Focus(NavigationMethod.Pointer);
        SelectAll();
    }

    private async void OnCopyLinkMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (_contextMenuLink is not { } link)
        {
            return;
        }

        await CopyTextToClipboardAsync(link.Url).ConfigureAwait(true);
    }

    private async void OnCopyTelegramMarkdownMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (!HasSelection || Document is not { Blocks.Count: > 0 } document)
        {
            return;
        }

        var selectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
        var markdown = TelegramMarkdownFormatter.FormatSelection(document, selectionRange);
        var html = TelegramMarkdownFormatter.FormatSelectionHtml(document, selectionRange);
        await CopyTelegramMarkdownToClipboardAsync(markdown, html).ConfigureAwait(true);
    }

    private async Task CopyTelegramMarkdownToClipboardAsync(string markdown, string htmlFragment)
    {
        if (string.IsNullOrEmpty(markdown) && string.IsNullOrEmpty(htmlFragment))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var item = new DataTransferItem();
        if (!string.IsNullOrEmpty(markdown))
        {
            item.SetText(markdown.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        }

        if (!string.IsNullOrEmpty(htmlFragment))
        {
            item.Set(HtmlClipboardFormat, Encoding.UTF8.GetBytes(CreateHtmlClipboardDocument(htmlFragment)));
            item.Set(WindowsHtmlClipboardFormat, CreateWindowsHtmlClipboardPayload(htmlFragment));
        }

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(item);
        await clipboard.SetDataAsync(dataTransfer).ConfigureAwait(true);
        await clipboard.FlushAsync().ConfigureAwait(true);
    }

    private static string CreateHtmlClipboardDocument(string htmlFragment)
        => "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>" + htmlFragment + "</body></html>";

    private static byte[] CreateWindowsHtmlClipboardPayload(string htmlFragment)
    {
        const string startFragmentMarker = "<!--StartFragment-->";
        const string endFragmentMarker = "<!--EndFragment-->";
        const string headerTemplate = "Version:0.9\r\nStartHTML:0000000000\r\nEndHTML:0000000000\r\nStartFragment:0000000000\r\nEndFragment:0000000000\r\n";

        var htmlPrefix = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>" + startFragmentMarker;
        var htmlSuffix = endFragmentMarker + "</body></html>";
        var html = htmlPrefix + htmlFragment + htmlSuffix;

        var startHtml = Encoding.UTF8.GetByteCount(headerTemplate);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(htmlPrefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(htmlFragment);
        var endHtml = startHtml + Encoding.UTF8.GetByteCount(html);

        var header = string.Create(
            CultureInfo.InvariantCulture,
            $"Version:0.9\r\nStartHTML:{startHtml:D10}\r\nEndHTML:{endHtml:D10}\r\nStartFragment:{startFragment:D10}\r\nEndFragment:{endFragment:D10}\r\n");

        return Encoding.UTF8.GetBytes(header + html);
    }

    private async Task TryActivatePressedLinkAsync(PointerReleasedEventArgs e)
    {
        if (_pressedFragment is null)
        {
            return;
        }

        var releasePosition = e.GetPosition(_pressedFragment);
        MarkdownLinkSpan? releasedLink = _pressedFragment.TryGetLinkAt(releasePosition, out var hitLink)
            ? hitLink
            : null;

        if (!MarkdownLinkActivationPolicy.CanActivateLink(
                _isDraggingSelection,
                SelectionAnchor,
                SelectionStart,
                SelectionEnd,
                _pressedLink,
                releasedLink))
        {
            return;
        }

        var pressedLink = _pressedLink!.Value;

        if (TryScrollToHeadingAnchor(pressedLink.Url))
        {
            return;
        }

        if (MarkdownLocalFileLinkResolver.TryResolve(pressedLink.Url, Document?.BaseDirectory, out var targetPath))
        {
            MarkdownFileLinkRequested?.Invoke(
                this,
                new MarkdownFileLinkRequestedEventArgs(pressedLink.Url, targetPath));
            return;
        }

        if (!Uri.TryCreate(pressedLink.Url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is null)
        {
            return;
        }

        await launcher.LaunchUriAsync(uri);
    }

    private void RegisterHeadingAnchor(MarkdownHeadingBlock block, Control headingControl)
    {
        var baseAnchor = MarkdownHeadingAnchorSlugger.CreateAnchor(block.Inlines);
        if (string.IsNullOrEmpty(baseAnchor))
        {
            return;
        }

        var count = _headingAnchorCounts.TryGetValue(baseAnchor, out var currentCount)
            ? currentCount
            : 0;
        _headingAnchorCounts[baseAnchor] = count + 1;

        var anchor = count == 0
            ? baseAnchor
            : string.Create(CultureInfo.InvariantCulture, $"{baseAnchor}-{count}");

        _headingAnchorTargets.TryAdd(anchor, headingControl);
    }

    internal bool HasHeadingAnchor(string linkTarget)
        => MarkdownHeadingAnchorSlugger.TryNormalizeFragment(linkTarget, out var anchor)
            && _headingAnchorTargets.ContainsKey(anchor);

    private bool TryScrollToHeadingAnchor(string linkTarget)
    {
        if (!MarkdownHeadingAnchorSlugger.TryNormalizeFragment(linkTarget, out var anchor)
            || !_headingAnchorTargets.TryGetValue(anchor, out var target))
        {
            return false;
        }

        return TryScrollTargetIntoView(target);
    }

    private bool TryScrollTargetIntoView(Control target)
    {
        var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (scrollViewer is null)
        {
            return false;
        }

        var targetPoint = target.TranslatePoint(new Point(0, 0), scrollViewer);
        if (targetPoint is null)
        {
            return false;
        }

        const double topInset = 24;
        var nextOffsetY = Math.Clamp(
            scrollViewer.Offset.Y + targetPoint.Value.Y - topInset,
            0,
            scrollViewer.ScrollBarMaximum.Y);

        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextOffsetY);
        return true;
    }

    private void CommitSelection(DocumentTextRange range, bool preserveOnRelease)
    {
        if (range.IsEmpty)
        {
            ClearSelection();
            return;
        }

        SelectionAnchor = range.Start;
        SelectionStart = range.Start;
        SelectionEnd = range.End;
        _preserveSelectionOnRelease = preserveOnRelease;
        ApplySelectionToFragments();
    }

    private void BeginPointerSession(
        PointerPressedEventArgs e,
        MarkdownDocumentSelectionFragmentBase fragment,
        Point localPosition,
        bool allowLinkActivation)
    {
        _isPointerPressed = true;
        _isDraggingSelection = false;
        _pointerPressOrigin = e.GetPosition(this);
        _pressedFragment = fragment;
        _pressedLink = allowLinkActivation && fragment.TryGetLinkAt(localPosition, out var pressedLink)
            ? pressedLink
            : null;
        e.Pointer.Capture(this);
    }

    private int ResolveDocumentOffset(Point position)
    {
        if (!TryResolveFragment(position, out var fragment, out var localPoint))
        {
            return 0;
        }

        return fragment.GetDocumentOffset(localPoint);
    }

    private bool TryResolveFragment(
        Point position,
        out MarkdownDocumentSelectionFragmentBase fragment,
        out Point localPoint)
    {
        fragment = null!;
        localPoint = default;

        if (_selectionFragments.Count == 0)
        {
            return false;
        }

        var fragments = new List<MarkdownDocumentSelectionFragmentBase>(_selectionFragments.Count);
        var candidates = new List<MarkdownFragmentHitTestCandidate>(_selectionFragments.Count);

        foreach (var candidateFragment in _selectionFragments)
        {
            var translated = this.TranslatePoint(position, candidateFragment);
            if (translated is null)
            {
                continue;
            }

            fragments.Add(candidateFragment);
            candidates.Add(new MarkdownFragmentHitTestCandidate(
                new Rect(0, 0, Math.Max(candidateFragment.Bounds.Width, 1), Math.Max(candidateFragment.Bounds.Height, 1)),
                translated.Value));
        }

        var bestIndex = MarkdownFragmentHitTester.FindBestIndex(candidates);
        if (bestIndex < 0)
        {
            return false;
        }

        fragment = fragments[bestIndex];
        localPoint = ClampPointToFragment(fragment, candidates[bestIndex].LocalPoint);
        return true;
    }

    private bool TryResolveLinkAtDocumentPoint(Point documentPoint, out MarkdownLinkSpan link)
    {
        link = default;
        if (!TryResolveFragment(documentPoint, out var fragment, out var localPoint))
        {
            return false;
        }

        return fragment.TryGetLinkAt(localPoint, out link);
    }

    private static Point ClampPointToFragment(MarkdownDocumentSelectionFragmentBase fragment, Point point)
    {
        var width = Math.Max(fragment.Bounds.Width, 1);
        var height = Math.Max(fragment.Bounds.Height, 1);
        return new Point(
            Math.Clamp(point.X, 0, width),
            Math.Clamp(point.Y, 0, height - 1));
    }

    internal static bool IsPointerInputFromScrollBarChrome(object? source)
    {
        if (source is not Control control)
        {
            return false;
        }

        return control is ScrollBar || control.FindAncestorOfType<ScrollBar>() is not null;
    }

    private static bool IsPointerInputFromCodeCopyButton(object? source)
    {
        if (source is not Control control)
        {
            return false;
        }

        return control.Classes.Contains("mm-code-copy-button")
            || control.FindAncestorOfType<Button>()?.Classes.Contains("mm-code-copy-button") == true;
    }

    private static bool HasCommandModifier(KeyModifiers modifiers)
        => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

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

    private void ResetPointerState()
    {
        _isPointerPressed = false;
        _isDraggingSelection = false;
        _preserveSelectionOnRelease = false;
        _pointerPressOrigin = default;
        _pressedFragment = null;
        _pressedLink = null;
    }

    private FontFamily ResolveBodyFontFamily() => ReadingPreferences.FontFamily switch
    {
        FontFamilyMode.Sans => LookupFontFamily("MmDocumentSansFontFamily"),
        FontFamilyMode.Mono => LookupFontFamily("MmDocumentMonoFontFamily"),
        _ => LookupFontFamily("MmDocumentSerifFontFamily")
    };

    private FontFamily ResolveSansFontFamily() => LookupFontFamily("MmDocumentSansFontFamily");

    private FontFamily ResolveMonoFontFamily() => LookupFontFamily("MmDocumentMonoFontFamily");

    private FontFamily LookupFontFamily(string resourceKey)
    {
        if (this.TryFindResource(resourceKey, ActualThemeVariant, out var value) && value is FontFamily family)
        {
            return family;
        }

        // Fallbacks mirror the minimal tail of the stacks in Themes/Typography.axaml
        // so that rendering stays sensible if the ResourceDictionary is not yet attached.
        return resourceKey switch
        {
            "MmDocumentSerifFontFamily" => new FontFamily("Georgia, Cambria, serif"),
            "MmDocumentSansFontFamily" => new FontFamily("Segoe UI, system-ui, sans-serif"),
            "MmDocumentMonoFontFamily" => new FontFamily("Consolas, Menlo, monospace"),
            _ => FontFamily.Default
        };
    }

    private double GetBodyLineHeight() => Math.Max(ReadingPreferences.FontSize * ReadingPreferences.LineHeight, ReadingPreferences.FontSize + 4);

    private double GetHeadingFontSize(int level)
    {
        var baseSize = ReadingPreferences.FontSize;
        return level switch
        {
            1 => baseSize * 2.1,
            2 => baseSize * 1.5,
            3 => baseSize * 1.2,
            4 => baseSize * 1.05,
            5 => baseSize * 0.95,
            6 => baseSize * 0.95,
            _ => baseSize
        };
    }

    private IBrush? LookupBrush(string resourceKey)
        => this.TryFindResource(resourceKey, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : null;

    private static string GetLocalizedString(string key, string fallback)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource("Localization", null, out var resource) == true
            && resource is ILocalizationService localization)
        {
            var value = localization[key];
            return string.IsNullOrWhiteSpace(value) || value.StartsWith("[[", StringComparison.Ordinal)
                ? fallback
                : value;
        }

        return fallback;
    }
}

internal readonly record struct MarkdownSourceLineVisualAnchor(Control Control, MarkdownSourceSpan SourceSpan);

internal readonly record struct MarkdownSourceLineAnchorSnapshot(int StartLine, int EndLine, double Y);
