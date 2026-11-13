using Avalonia;
using Avalonia.Controls;

namespace MarkMello.Presentation.Views;

// IOutlineNavigationHost for edit mode (ADR-0008 Rule 6).
public partial class EditWorkspaceView
{
    private int? _outlineCaretLine;

    // Кэш нужен только для подавления повторных событий; значение для чтения считается заново.
    public int? CurrentSourceLine => _editorTextBox is { } textBox
        ? GetSourceLineFromCharacterIndex(textBox.Text ?? string.Empty, textBox.CaretIndex)
        : null;

    private EventHandler? _currentSourceLineChanged;

    public event EventHandler? CurrentSourceLineChanged
    {
        add
        {
            _currentSourceLineChanged += value;
            // Новый подписчик читает значение сам; кэш сравнения синхронизируем с ним.
            _outlineCaretLine = CurrentSourceLine;
        }
        remove => _currentSourceLineChanged -= value;
    }

    /// <summary>
    /// Caret to the start of the heading's source line, line scrolled into the
    /// scroll-sync anchor position, focus into the editor. Lines past the end
    /// clamp to the end of the text. Returns false only when there is no editor;
    /// without layout the caret is still placed and the scroll is skipped.
    /// </summary>
    public bool TryNavigateToSourceLine(int sourceLine)
    {
        if (_editorTextBox is null)
        {
            return false;
        }

        var index = GetLineStartCharacterIndex(_editorTextBox.Text ?? string.Empty, sourceLine);
        _editorTextBox.SelectionStart = index;
        _editorTextBox.SelectionEnd = index;
        _editorTextBox.CaretIndex = index;
        ScrollEditorToOffset(index);
        FocusEditorAsync();
        return true;
    }

    private void AttachOutlineCaretTracking(TextBox textBox)
    {
        textBox.PropertyChanged += OnEditorTextBoxPropertyChangedForOutline;
        UpdateOutlineCaretLine(textBox);
    }

    private void DetachOutlineCaretTracking(TextBox textBox)
    {
        textBox.PropertyChanged -= OnEditorTextBoxPropertyChangedForOutline;
        _outlineCaretLine = null;
    }

    private void OnEditorTextBoxPropertyChangedForOutline(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.CaretIndexProperty && sender is TextBox textBox)
        {
            UpdateOutlineCaretLine(textBox);
        }
    }

    private void UpdateOutlineCaretLine(TextBox textBox)
    {
        // Rule 1 ADR-0008: без подписчика не считаем строку каретки (O(offset) на каждый шаг).
        if (_currentSourceLineChanged is null)
        {
            return;
        }

        var line = GetSourceLineFromCharacterIndex(textBox.Text ?? string.Empty, textBox.CaretIndex);
        if (line == _outlineCaretLine)
        {
            return;
        }

        _outlineCaretLine = line;
        _currentSourceLineChanged.Invoke(this, EventArgs.Empty);
    }
}
