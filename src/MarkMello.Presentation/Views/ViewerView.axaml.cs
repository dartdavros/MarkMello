using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

public partial class ViewerView : UserControl
{
    private const double WheelStepMultiplier = 6.0;
    private ScrollViewer? _scroll;

    public ViewerView()
    {
        InitializeComponent();
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
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            _scroll = null;
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
        var nextOffset = Math.Clamp(_scroll.Offset.Y - (e.Delta.Y * wheelStep), 0, maxOffset);

        if (Math.Abs(nextOffset - _scroll.Offset.Y) <= double.Epsilon)
        {
            return;
        }

        _scroll.Offset = new Vector(_scroll.Offset.X, nextOffset);
        e.Handled = true;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroll is null || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var max = _scroll.ScrollBarMaximum.Y;
        var current = _scroll.Offset.Y;
        vm.ReadingProgress = max > 0 ? Math.Clamp(current / max * 100.0, 0, 100) : 0;
    }
}
