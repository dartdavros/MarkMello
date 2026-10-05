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
public sealed partial class MarkdownDocumentView : UserControl
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

    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, string?>(nameof(SourceText));

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

    // Путь текстовой карты для каждого фрагмента из _selectionFragments (тот же
    // порядок). Нужен, чтобы у переиспользованного блока обновить DocumentRange:
    // правка выше по документу сдвигает абсолютные offset'ы всех блоков ниже.
    private readonly List<string> _selectionFragmentPaths = [];
    private readonly List<DocumentTextRange> _searchMatches = [];
    private string _activeSearchQuery = string.Empty;
    private int _activeMatchIndex = -1;
    private readonly Dictionary<string, Control> _headingAnchorTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _headingAnchorCounts = new(StringComparer.Ordinal);

    // Заголовки в порядке документа. Индекс якорей строится из этого списка в
    // конце Rebuild: нумерация дублей зависит от порядка по всему документу и
    // не может считаться поблочно.
    private readonly List<(MarkdownHeadingBlock Block, Control Control)> _headingAnchorRegistrations = [];
    private readonly List<MarkdownSourceLineVisualAnchor> _sourceLineAnchors = [];
    private List<BuiltTopLevelBlock> _builtBlocks = [];
    private MarkdownDocumentTextMap _textMap = MarkdownDocumentTextMap.Empty;
    private bool _isPointerPressed;
    private bool _isDraggingSelection;
    private Point _pointerPressOrigin;
    private Point _pressedLocalPosition;
    private MarkdownDocumentSelectionFragmentBase? _pressedFragment;
    private MarkdownLinkSpan? _pressedLink;
    private bool _preserveSelectionOnRelease;
    private readonly MarkdownQuickInlineEditingController _quickInlineEditingController;
    private DispatcherTimer? _pendingQuickEditTimer;
    private Action? _pendingQuickEditAction;
    private MenuItem? _copyMenuItem;
    private MenuItem? _copyLinkMenuItem;
    private MenuItem? _copyTelegramMarkdownMenuItem;
    private MenuItem? _selectAllMenuItem;
    private MarkdownLinkSpan? _contextMenuLink;
    private IReadOnlyList<string> _contextMenuSelectedLinkUrls = Array.Empty<string>();
    private CancellationTokenSource? _readingPreferencesRefreshCts;
    private long _renderGeneration;
    private bool _hasPendingRenderedNotification;

    static MarkdownDocumentView()
    {
        DocumentProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.Rebuild());
        ImageSourceResolverProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.RebuildFromScratch());
        DocumentPaddingProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.ApplyDocumentPadding());
        ReadingPreferencesProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.RefreshForReadingPreferencesChange());
    }

    public MarkdownDocumentView()
    {
        _quickInlineEditingController = new MarkdownQuickInlineEditingController(this);
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

    public RenderedMarkdownDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
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

    public event EventHandler<MarkdownDocumentContentEditedEventArgs>? DocumentContentEdited;

    public void CommitQuickEditor() => _quickInlineEditingController.CommitQuickEditor();

    public void CancelQuickEditor() => _quickInlineEditingController.CancelQuickEditor();

    internal void RaiseDocumentContentEdited(string newContent)
        => DocumentContentEdited?.Invoke(this, new MarkdownDocumentContentEditedEventArgs(newContent));
}
