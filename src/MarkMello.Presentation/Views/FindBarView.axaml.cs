using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MarkMello.Presentation.Views;

public partial class FindBarView : UserControl
{
    private TextBox? _findInput;

    public FindBarView()
    {
        InitializeComponent();
    }

    public event EventHandler? FindNextRequested;

    public event EventHandler? FindPreviousRequested;

    public event EventHandler? CloseRequested;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _findInput = this.FindControl<TextBox>("FindInput");
        FocusInputAsync();
    }

    private void FocusInputAsync()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _findInput?.Focus();
            _findInput?.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void OnFindInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            FindPreviousRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            FindNextRequested?.Invoke(this, EventArgs.Empty);
        }

        e.Handled = true;
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
        => FindPreviousRequested?.Invoke(this, EventArgs.Empty);

    private void OnNextClick(object? sender, RoutedEventArgs e)
        => FindNextRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseClick(object? sender, RoutedEventArgs e)
        => CloseRequested?.Invoke(this, EventArgs.Empty);
}
