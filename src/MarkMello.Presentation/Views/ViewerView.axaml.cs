using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views.Markdown.Minimap;
using System.ComponentModel;

namespace MarkMello.Presentation.Views;

public partial class ViewerView : UserControl, IFindHost
{
    private const double WheelStepMultiplier = 6.0;
    private const double KeyboardPageOverlap = 48.0;
    private ScrollViewer? _scroll;
    private MarkdownDocumentView? _documentView;
    private ContentControl? _minimapHost;
    private DocumentMinimapView? _minimap;
    private int _minimapBuildGeneration;
    private bool _isMinimapBuildQueued;
    private bool _hasRenderedDocument;
    private Size _lastMinimapExtent;
    private Size _lastMinimapViewport;
    private MainWindowViewModel? _viewModel;

    public ViewerView()
    {
        InitializeComponent();
    }

    // ---------- IFindHost ----------

    public string? ActiveQuery => _documentView?.ActiveSearchQuery;

    public int MatchIndex => _documentView?.MatchIndex ?? -1;

    public int MatchCount => _documentView?.MatchCount ?? 0;

    public event EventHandler? FindStateChanged;

    public void ApplyQuery(string? query) => _documentView?.ApplySearchQuery(query);

    public void FindNext() => _documentView?.FindNext();

    public void FindPrevious() => _documentView?.FindPrevious();

    public void ClearFind() => _documentView?.ApplySearchQuery(null);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        AttachViewModel(DataContext as MainWindowViewModel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroll = this.FindControl<ScrollViewer>("DocScroll");
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
            _scroll.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        }

        AddHandler(KeyDownEvent, OnViewerKeyDown, RoutingStrategies.Tunnel);

        _minimapHost = this.FindControl<ContentControl>("MinimapHost");
        if (_minimapHost is not null)
        {
            _minimapHost.IsHitTestVisible = false;
        }

        _documentView = this.FindControl<MarkdownDocumentView>("DocumentView");
        if (_documentView is not null)
        {
            _documentView.DocumentRendered += OnDocumentRendered;
            _documentView.DocumentRenderInvalidated += OnDocumentRenderInvalidated;
            _documentView.MarkdownFileLinkRequested += OnMarkdownFileLinkRequested;
            _documentView.SearchStateChanged += OnDocumentSearchStateChanged;
        }

        SizeChanged += OnViewerSizeChanged;
        ActualThemeVariantChanged += OnViewerAppearanceChanged;
        ResourcesChanged += OnViewerResourcesChanged;
        AttachViewModel(DataContext as MainWindowViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        SizeChanged -= OnViewerSizeChanged;
        ActualThemeVariantChanged -= OnViewerAppearanceChanged;
        ResourcesChanged -= OnViewerResourcesChanged;
        AttachViewModel(null);
        _minimapBuildGeneration++;
        _isMinimapBuildQueued = false;
        RemoveMinimap();
        _hasRenderedDocument = false;
        _lastMinimapExtent = default;
        _lastMinimapViewport = default;
        _minimapHost = null;

        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            _scroll = null;
        }

        RemoveHandler(KeyDownEvent, OnViewerKeyDown);

        if (_documentView is not null)
        {
            _documentView.DocumentRendered -= OnDocumentRendered;
            _documentView.DocumentRenderInvalidated -= OnDocumentRenderInvalidated;
            _documentView.MarkdownFileLinkRequested -= OnMarkdownFileLinkRequested;
            _documentView.SearchStateChanged -= OnDocumentSearchStateChanged;
            _documentView = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_scroll is null || Math.Abs(e.Delta.Y) <= double.Epsilon)
        {
            return;
        }

        // Preserve horizontal wheel gestures for nested controls such as
        // horizontally scrollable code blocks. We only take over primarily
        // vertical scrolling to match the faster browser-like reading feel.
        if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
        {
            return;
        }

        var maxOffset = _scroll.ScrollBarMaximum.Y;
        if (maxOffset <= 0)
        {
            return;
        }

        var baseStep = _scroll.SmallChange.Height > 0 ? _scroll.SmallChange.Height : 16.0;
        var wheelStep = baseStep * WheelStepMultiplier;
        var nextOffset = Math.Clamp(_scroll.Offset.Y - e.Delta.Y * wheelStep, 0, maxOffset);

        if (Math.Abs(nextOffset - _scroll.Offset.Y) <= double.Epsilon)
        {
            return;
        }

        _scroll.Offset = new Vector(_scroll.Offset.X, nextOffset);
        e.Handled = true;
    }

    private void OnViewerKeyDown(object? sender, KeyEventArgs e)
    {
        if (_scroll is null || e.Handled || DataContext is not MainWindowViewModel { IsViewer: true, IsEditMode: false })
        {
            return;
        }

        if (HasCommandModifier(e.KeyModifiers) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        var nextOffsetY = GetKeyboardScrollOffset(
            e.Key,
            e.KeyModifiers,
            _scroll.Offset.Y,
            _scroll.ScrollBarMaximum.Y,
            _scroll.SmallChange.Height,
            _scroll.Viewport.Height);

        if (nextOffsetY is null || Math.Abs(nextOffsetY.Value - _scroll.Offset.Y) <= double.Epsilon)
        {
            return;
        }

        _scroll.Offset = new Vector(_scroll.Offset.X, nextOffsetY.Value);
        e.Handled = true;
    }

    internal static double? GetKeyboardScrollOffset(
        Key key,
        KeyModifiers modifiers,
        double currentOffset,
        double maximumOffset,
        double smallChange,
        double viewportHeight)
    {
        var max = Math.Max(0, maximumOffset);
        var current = Math.Clamp(currentOffset, 0, max);
        var lineStep = smallChange > 0 ? smallChange : 40.0;
        var pageStep = Math.Max(lineStep, viewportHeight - KeyboardPageOverlap);

        var target = key switch
        {
            Key.Down => current + lineStep,
            Key.Up => current - lineStep,
            Key.PageDown => current + pageStep,
            Key.PageUp => current - pageStep,
            Key.Home => 0,
            Key.End => max,
            Key.Space when modifiers.HasFlag(KeyModifiers.Shift) => current - pageStep,
            Key.Space => current + pageStep,
            _ => (double?)null,
        };

        return target is null ? null : Math.Clamp(target.Value, 0, max);
    }

    private static bool HasCommandModifier(KeyModifiers modifiers)
        => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private void OnDocumentRendered(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.MarkReadableDocumentRendered();
            RestorePendingScrollOffset(vm);
        }

        _hasRenderedDocument = true;
        FocusDocumentViewAsync();
        QueueMinimapBuild();

        // Keep the active search match in view after a document re-render.
        if (_documentView?.MatchIndex >= 0)
        {
            _documentView.ScrollToActiveMatch();
        }
    }

    /// <summary>
    /// Возврат на вкладку восстанавливает её позицию прокрутки. Делается после отрисовки:
    /// до неё ScrollBarMaximum ещё нулевой и любое смещение схлопнется в ноль.
    /// </summary>
    private void RestorePendingScrollOffset(MainWindowViewModel viewModel)
    {
        if (viewModel.TakePendingScrollOffset() is not { } offset || _scroll is null)
        {
            return;
        }

        if (offset <= 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_scroll is null)
                {
                    return;
                }

                var target = Math.Clamp(offset, 0, _scroll.ScrollBarMaximum.Y);
                _scroll.Offset = new Vector(_scroll.Offset.X, target);
            },
            DispatcherPriority.Background);
    }

    private void OnDocumentSearchStateChanged(object? sender, EventArgs e)
        => FindStateChanged?.Invoke(this, EventArgs.Empty);

    private void FocusDocumentViewAsync()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_documentView is not null && DataContext is MainWindowViewModel { IsViewer: true, IsEditMode: false })
            {
                _documentView.Focus(NavigationMethod.Unspecified);
            }
        }, DispatcherPriority.Background);
    }

    private void OnDocumentRenderInvalidated(object? sender, EventArgs e)
    {
        _hasRenderedDocument = false;
        _lastMinimapExtent = default;
        _lastMinimapViewport = default;
        _minimapBuildGeneration++;
        RemoveMinimap();
    }

    private async void OnMarkdownFileLinkRequested(object? sender, MarkdownFileLinkRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        await vm.OpenPathAsync(e.TargetPath).ConfigureAwait(true);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroll is null)
        {
            return;
        }

        var max = _scroll.ScrollBarMaximum.Y;
        var current = _scroll.Offset.Y;
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ReadingProgress = max > 0 ? Math.Clamp(current / max * 100.0, 0, 100) : 0;

            // Позиция уезжает во вкладку на каждое изменение: при переключении
            // вьюер уже показывает другой документ и спрашивать его поздно.
            vm.ReportScrollOffset(current);
        }

        if (_hasRenderedDocument && HasMinimapLayoutMetricsChanged())
        {
            QueueMinimapBuild();
        }

        UpdateMinimapScrollState();
        UpdateMinimapVisibility();
    }

    private void OnViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!_hasRenderedDocument)
        {
            return;
        }

        QueueMinimapBuild();
    }

    private void OnViewerAppearanceChanged(object? sender, EventArgs e)
    {
        if (!_hasRenderedDocument)
        {
            return;
        }

        QueueMinimapBuild();
    }

    private void OnViewerResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        if (!_hasRenderedDocument)
        {
            return;
        }

        QueueMinimapBuild();
    }

    private void AttachViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.ReadingPreferences))
        {
            return;
        }

        if (!_hasRenderedDocument)
        {
            return;
        }

        if (!ShouldShowMinimap())
        {
            RemoveMinimap();
            return;
        }

        QueueMinimapBuild();
    }

    private void QueueMinimapBuild()
    {
        _minimapBuildGeneration++;
        if (_isMinimapBuildQueued)
        {
            return;
        }

        _isMinimapBuildQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _isMinimapBuildQueued = false;
                BuildMinimapIfCurrent(_minimapBuildGeneration);
            },
            DispatcherPriority.Background);
    }

    private void BuildMinimapIfCurrent(int generation)
    {
        if (generation != _minimapBuildGeneration || !_hasRenderedDocument || _documentView is null || _scroll is null || _minimapHost is null)
        {
            return;
        }

        _lastMinimapExtent = _scroll.Extent;
        _lastMinimapViewport = _scroll.Viewport;

        if (!ShouldShowMinimap())
        {
            RemoveMinimap();
            return;
        }

        var snapshot = _documentView.CreateMiniatureSnapshot();
        if (!DocumentMinimapBuildPolicy.AllowsDetailedMiniature(snapshot))
        {
            RemoveMinimap();
            return;
        }

        var minimap = EnsureMinimap();
        minimap.SetSource(_documentView, snapshot);
        UpdateMinimapScrollState();
        UpdateMinimapVisibility();
    }

    private DocumentMinimapView EnsureMinimap()
    {
        if (_minimap is not null)
        {
            return _minimap;
        }

        var minimap = new DocumentMinimapView();
        minimap.ScrollRequested += OnMinimapScrollRequested;
        _minimap = minimap;

        if (_minimapHost is not null)
        {
            _minimapHost.Content = minimap;
            _minimapHost.IsHitTestVisible = true;
        }

        return minimap;
    }

    private void RemoveMinimap()
    {
        if (_minimap is not null)
        {
            _minimap.ScrollRequested -= OnMinimapScrollRequested;
            _minimap.ClearSource();
            _minimap = null;
        }

        if (_minimapHost is not null)
        {
            _minimapHost.Content = null;
            _minimapHost.IsHitTestVisible = false;
        }
    }

    private void OnMinimapScrollRequested(object? sender, DocumentMinimapScrollRequestedEventArgs e)
    {
        if (_scroll is null)
        {
            return;
        }

        var targetOffset = Math.Clamp(e.OffsetY, 0, _scroll.ScrollBarMaximum.Y);
        _scroll.Offset = new Vector(_scroll.Offset.X, targetOffset);
    }

    private void UpdateMinimapScrollState()
    {
        if (_scroll is null || _minimap is null)
        {
            return;
        }

        _minimap.ScrollOffset = _scroll.Offset.Y;
        _minimap.ScrollMaximum = _scroll.ScrollBarMaximum.Y;
        _minimap.ViewportHeight = _scroll.Viewport.Height;
    }

    private void UpdateMinimapVisibility()
    {
        if (_minimapHost is null || _minimap is null)
        {
            return;
        }

        var visible = ShouldShowMinimap();
        _minimapHost.IsVisible = visible;
        _minimapHost.IsHitTestVisible = visible;
    }

    private bool HasMinimapLayoutMetricsChanged()
    {
        if (_scroll is null)
        {
            return false;
        }

        return DocumentMinimapBuildPolicy.HasLayoutMetricsChanged(
            _lastMinimapExtent,
            _lastMinimapViewport,
            _scroll.Extent,
            _scroll.Viewport);
    }

    private bool ShouldShowMinimap()
    {
        if (_scroll is null)
        {
            return false;
        }

        var mode = DataContext is MainWindowViewModel vm
            ? vm.ReadingPreferences.DocumentMinimapMode
            : DocumentMinimapMode.Auto;

        return DocumentMinimapBuildPolicy.ShouldShow(
            mode,
            Bounds.Width,
            _scroll.Extent,
            _scroll.Viewport,
            _scroll.ScrollBarMaximum.Y);
    }
}
