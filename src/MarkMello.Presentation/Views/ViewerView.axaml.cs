using Avalonia;
using Avalonia.Controls;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

public partial class ViewerView : UserControl
{
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
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll = null;
        }
        base.OnDetachedFromVisualTree(e);
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
