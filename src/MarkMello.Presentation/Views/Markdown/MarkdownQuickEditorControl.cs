using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Инлайн-редактор простого текста блока для режима просмотра без переключения в двухпанельный вид.
/// </summary>
public sealed class MarkdownQuickEditorControl : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    private bool _isCommitted;

    public event EventHandler? CommitRequested;
    public event EventHandler? CancelRequested;

    public MarkdownQuickEditorControl()
    {
        Classes.Add("mm-quick-inline-editor");
        AcceptsReturn = true;
        AcceptsTab = false;
        TextWrapping = TextWrapping.Wrap;
        UseLayoutRounding = true;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(this, ScrollBarVisibility.Auto);
        LostFocus += OnLostFocus;
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_isCommitted)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            var focused = topLevel?.FocusManager?.GetFocusedElement() as Visual;
            if (focused is not null && (ReferenceEquals(focused, this) || IsVisualDescendantOf(focused, this)))
            {
                return;
            }

            RequestCommit();
        }, DispatcherPriority.Background);
    }

    private static bool IsVisualDescendantOf(Visual child, Visual parent)
    {
        Visual? current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }
            current = current.GetVisualParent();
        }
        return false;
    }

    public void SetInitialCaret(int caretIndex)
    {
        var clamped = Math.Clamp(caretIndex, 0, Text?.Length ?? 0);
        CaretIndex = clamped;
        SelectionStart = clamped;
        SelectionEnd = clamped;
    }

    public void RequestCommit() => Commit();

    public void Commit()
    {
        if (_isCommitted)
        {
            return;
        }

        _isCommitted = true;
        CommitRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestCancel()
    {
        if (_isCommitted)
        {
            return;
        }

        _isCommitted = true;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestCancel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if ((e.KeyModifiers & KeyModifiers.Control) != 0 || (e.KeyModifiers & KeyModifiers.Shift) == 0)
            {
                // In single-line blocks (e.g. headings) or when Ctrl+Enter is pressed, commit the edit.
                // In multiline text, Shift+Enter adds newline; Enter commits if it's a single line block.
                var text = Text ?? string.Empty;
                if ((e.KeyModifiers & KeyModifiers.Control) != 0 || !text.Contains('\n'))
                {
                    RequestCommit();
                    e.Handled = true;
                    return;
                }
            }
        }

        var hasCmd = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (hasCmd && (e.Key == Key.S || e.Key == Key.Tab || e.Key == Key.E))
        {
            RequestCommit();
            // Don't mark as handled so window KeyBindings (Save, NextTab, ToggleEditMode) can trigger
            return;
        }

        base.OnKeyDown(e);
    }
}
