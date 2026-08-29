using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.Editing;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

public partial class EditWorkspaceView : UserControl
{
    private const double ScrollSyncViewportAnchorRatio = 0.38;
    private const double ScrollSyncMinViewportAnchorY = 24;
    private const double ScrollSyncHitTestX = 2;
    private const int MaxScrollSyncAttachAttempts = 4;

    private TextBox? _editorTextBox;
    private TextPresenter? _editorTextPresenter;
    private ScrollViewer? _editorScrollViewer;
    private ScrollViewer? _previewScrollViewer;
    private MarkdownDocumentView? _previewDocumentView;
    private bool _isSynchronizingScroll;

    public EditWorkspaceView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DataContextChanged += OnDataContextChanged;
        ApplySplitRatio();
        AttachScrollSynchronizationAsync();
        FocusEditorAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DataContextChanged -= OnDataContextChanged;
        DetachScrollSynchronization();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ApplySplitRatio();
        SynchronizePreviewToEditor();
    }

    private void AttachScrollSynchronizationAsync(int attempt = 0)
    {
        Dispatcher.UIThread.Post(() => AttachScrollSynchronization(attempt), DispatcherPriority.Background);
    }

    private void AttachScrollSynchronization(int attempt)
    {
        if (VisualRoot is null)
        {
            return;
        }

        DetachScrollSynchronization();

        _editorTextBox = this.FindControl<TextBox>("EditorTextBox");
        _previewScrollViewer = this.FindControl<ScrollViewer>("PreviewScrollViewer");
        _previewDocumentView = this.FindControl<MarkdownDocumentView>("PreviewDocumentView");
        var editorVisuals = _editorTextBox?
            .GetVisualDescendants()
            .ToArray();
        _editorScrollViewer = editorVisuals?
            .OfType<ScrollViewer>()
            .FirstOrDefault();
        _editorTextPresenter = editorVisuals?
            .OfType<TextPresenter>()
            .FirstOrDefault(static presenter => presenter.Name == "PART_TextPresenter")
            ?? editorVisuals?
                .OfType<TextPresenter>()
                .FirstOrDefault();

        if (_editorScrollViewer is null
            || _editorTextPresenter is null
            || _previewScrollViewer is null
            || _previewDocumentView is null)
        {
            if (attempt < MaxScrollSyncAttachAttempts)
            {
                AttachScrollSynchronizationAsync(attempt + 1);
            }

            return;
        }

        _editorScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        _previewScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        _previewDocumentView.DocumentRendered += OnPreviewDocumentRendered;
        _previewDocumentView.DocumentRenderInvalidated += OnPreviewDocumentRenderInvalidated;

        SynchronizePreviewToEditor();
    }

    private void DetachScrollSynchronization()
    {
        if (_editorScrollViewer is not null)
        {
            _editorScrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
        }

        if (_previewScrollViewer is not null)
        {
            _previewScrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
        }

        if (_previewDocumentView is not null)
        {
            _previewDocumentView.DocumentRendered -= OnPreviewDocumentRendered;
            _previewDocumentView.DocumentRenderInvalidated -= OnPreviewDocumentRenderInvalidated;
        }

        _editorTextBox = null;
        _editorTextPresenter = null;
        _editorScrollViewer = null;
        _previewScrollViewer = null;
        _previewDocumentView = null;
        _isSynchronizingScroll = false;
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.OffsetProperty || _isSynchronizingScroll)
        {
            return;
        }

        if (ReferenceEquals(sender, _editorScrollViewer))
        {
            SynchronizePreviewToEditor();
            return;
        }

        if (ReferenceEquals(sender, _previewScrollViewer))
        {
            SynchronizeEditorToPreview();
        }
    }

    private void OnPreviewDocumentRendered(object? sender, EventArgs e)
        => SynchronizePreviewToEditor();

    private void OnPreviewDocumentRenderInvalidated(object? sender, EventArgs e)
    {
        // The preview is about to rebuild and its source-line anchors are stale.
        // The rendered event will restore synchronization after the new layout pass.
    }

    private void SynchronizePreviewToEditor()
    {
        if (_previewScrollViewer is null
            || _previewDocumentView is null
            || !TryGetEditorSourcePositionAtViewportAnchor(out var sourcePosition)
            || !_previewDocumentView.TryGetVerticalOffsetForSourcePosition(sourcePosition, out var previewDocumentOffsetY)
            || !TryGetViewportRelativeOriginY(_previewDocumentView, _previewScrollViewer, out var previewDocumentOriginY))
        {
            return;
        }

        var targetOffsetY = _previewScrollViewer.Offset.Y
            + previewDocumentOriginY
            + previewDocumentOffsetY
            - GetViewportAnchorY(_previewScrollViewer);
        SetSynchronizedVerticalOffset(_previewScrollViewer, targetOffsetY);
    }

    private void SynchronizeEditorToPreview()
    {
        if (_previewScrollViewer is null
            || _previewDocumentView is null
            || !TryGetViewportRelativeOriginY(_previewDocumentView, _previewScrollViewer, out var previewDocumentOriginY))
        {
            return;
        }

        var previewDocumentOffsetY = Math.Max(
            0,
            GetViewportAnchorY(_previewScrollViewer) - previewDocumentOriginY);

        if (!_previewDocumentView.TryGetSourcePositionForVerticalOffset(previewDocumentOffsetY, out var sourcePosition)
            || !TryGetEditorVerticalOffsetForSourcePosition(sourcePosition, out var editorOffsetY))
        {
            return;
        }

        SetSynchronizedVerticalOffset(_editorScrollViewer!, editorOffsetY);
    }

    /// <summary>
    /// Позиция в исходнике под якорем вьюпорта редактора, выраженная дробно:
    /// номер логической строки плюс доля пройденного по ней пути.
    ///
    /// Строка markdown-абзаца при мягком переносе занимает в редакторе много
    /// визуальных строк. Округление до её номера теряло бы всё продвижение
    /// внутри абзаца — именно из-за этого preview отставал тем сильнее, чем
    /// дальше по документу уехал редактор.
    /// </summary>
    private bool TryGetEditorSourcePositionAtViewportAnchor(out double sourcePosition)
    {
        sourcePosition = 0;
        if (_editorTextBox is null
            || _editorTextPresenter is null
            || _editorScrollViewer is null
            || !TryGetViewportRelativeOriginY(_editorTextPresenter, _editorScrollViewer, out var presenterOriginY))
        {
            return false;
        }

        var text = _editorTextBox.Text ?? string.Empty;
        var localY = Math.Clamp(
            GetViewportAnchorY(_editorScrollViewer) - presenterOriginY,
            0,
            Math.Max(0, _editorTextPresenter.Bounds.Height - 1));
        var localX = Math.Clamp(
            ScrollSyncHitTestX,
            0,
            Math.Max(0, _editorTextPresenter.Bounds.Width - 1));

        var hit = _editorTextPresenter.TextLayout.HitTestPoint(new Point(localX, localY));
        var characterIndex = Math.Clamp(hit.TextPosition, 0, text.Length);
        var lastLine = Math.Max(0, CountSourceLines(text) - 1);
        var sourceLine = Math.Clamp(GetSourceLineFromCharacterIndex(text, characterIndex), 0, lastLine);

        sourcePosition = sourceLine + GetProgressWithinSourceLine(text, sourceLine, localY);
        return true;
    }

    private bool TryGetEditorVerticalOffsetForSourcePosition(double sourcePosition, out double offsetY)
    {
        offsetY = 0;
        if (_editorTextBox is null
            || _editorTextPresenter is null
            || _editorScrollViewer is null
            || !TryGetViewportRelativeOriginY(_editorTextPresenter, _editorScrollViewer, out var presenterOriginY))
        {
            return false;
        }

        var text = _editorTextBox.Text ?? string.Empty;
        var lastLine = Math.Max(0, CountSourceLines(text) - 1);
        var sourceLine = (int)Math.Clamp(Math.Floor(sourcePosition), 0, lastLine);
        var progress = Math.Clamp(sourcePosition - sourceLine, 0, 1);

        GetSourceLineTops(text, sourceLine, out var lineTop, out var nextLineTop);
        var localY = nextLineTop > lineTop
            ? lineTop + ((nextLineTop - lineTop) * progress)
            : lineTop;

        offsetY = _editorScrollViewer.Offset.Y
            + presenterOriginY
            + localY
            - GetViewportAnchorY(_editorScrollViewer);
        return true;
    }

    /// <summary>
    /// Доля [0..1] пройденного по логической строке на высоте
    /// <paramref name="localY"/> внутри text presenter.
    /// </summary>
    private double GetProgressWithinSourceLine(string text, int sourceLine, double localY)
    {
        GetSourceLineTops(text, sourceLine, out var lineTop, out var nextLineTop);
        if (nextLineTop <= lineTop)
        {
            return 0;
        }

        return Math.Clamp((localY - lineTop) / (nextLineTop - lineTop), 0, 1);
    }

    /// <summary>
    /// Вертикальные границы логической строки внутри text presenter: её верх и
    /// верх следующей. Обе границы берутся за один проход по тексту — метод
    /// вызывается на каждое событие скролла.
    /// </summary>
    private void GetSourceLineTops(string text, int sourceLine, out double lineTop, out double nextLineTop)
    {
        var layout = _editorTextPresenter!.TextLayout;
        var lineStart = GetLineStartCharacterIndex(text, sourceLine);
        lineTop = layout.HitTestTextPosition(lineStart).Y;

        var lineBreak = text.IndexOf('\n', lineStart);
        if (lineBreak < 0)
        {
            // Последняя строка: её низ, иначе завершающий абзац остался бы без
            // разрешения внутри себя.
            var end = layout.HitTestTextPosition(text.Length);
            nextLineTop = end.Y + end.Height;
            return;
        }

        nextLineTop = layout.HitTestTextPosition(lineBreak + 1).Y;
    }

    private static bool TryGetViewportRelativeOriginY(Control control, Visual relativeTo, out double originY)
    {
        originY = 0;
        var origin = control.TranslatePoint(new Point(0, 0), relativeTo);
        if (origin is null)
        {
            return false;
        }

        originY = origin.Value.Y;
        return true;
    }

    private static double GetViewportAnchorY(ScrollViewer scrollViewer)
    {
        var viewportHeight = Math.Max(0, scrollViewer.Bounds.Height);
        if (viewportHeight <= 0)
        {
            return ScrollSyncMinViewportAnchorY;
        }

        if (viewportHeight <= ScrollSyncMinViewportAnchorY * 2)
        {
            return viewportHeight * 0.5;
        }

        return Math.Clamp(
            viewportHeight * ScrollSyncViewportAnchorRatio,
            ScrollSyncMinViewportAnchorY,
            viewportHeight - ScrollSyncMinViewportAnchorY);
    }

    private static int GetSourceLineFromCharacterIndex(string text, int characterIndex)
    {
        var normalizedIndex = Math.Clamp(characterIndex, 0, text.Length);
        var line = 0;
        for (var index = 0; index < normalizedIndex; index++)
        {
            if (text[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static int GetLineStartCharacterIndex(string text, int sourceLine)
    {
        if (string.IsNullOrEmpty(text) || sourceLine <= 0)
        {
            return 0;
        }

        var currentLine = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            currentLine++;
            if (currentLine >= sourceLine)
            {
                return Math.Min(text.Length, index + 1);
            }
        }

        return text.Length;
    }

    private static int CountSourceLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var count = 1;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private void SetSynchronizedVerticalOffset(ScrollViewer scrollViewer, double offsetY)
    {
        var maximumY = Math.Max(0, scrollViewer.ScrollBarMaximum.Y);
        var normalizedY = Math.Clamp(offsetY, 0, maximumY);
        if (Math.Abs(scrollViewer.Offset.Y - normalizedY) < 0.5)
        {
            return;
        }

        _isSynchronizingScroll = true;
        try
        {
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, normalizedY);
        }
        finally
        {
            _isSynchronizingScroll = false;
        }
    }

    private void OnFormatButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EditorSessionViewModel session)
        {
            return;
        }

        if (sender is not Button button || button.Tag is not string rawKind)
        {
            return;
        }

        if (!Enum.TryParse<MarkdownEditorFormatKind>(rawKind, ignoreCase: true, out var kind))
        {
            return;
        }

        var editor = this.FindControl<TextBox>("EditorTextBox");
        if (editor is null)
        {
            return;
        }

        var selectionStart = Math.Min(editor.SelectionStart, editor.SelectionEnd);
        var selectionEnd = Math.Max(editor.SelectionStart, editor.SelectionEnd);
        if (TryBlockUnsafeProtectedRangeEdit(editor, session, new DocumentTextRange(selectionStart, selectionEnd)))
        {
            return;
        }

        var result = MarkdownEditorFormatter.Apply(session.SourceText, kind, selectionStart, selectionEnd);

        editor.Text = result.Text;
        editor.SelectionStart = result.SelectionStart;
        editor.SelectionEnd = result.SelectionEnd;
        editor.CaretIndex = result.SelectionEnd;
        editor.Focus();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor || DataContext is not EditorSessionViewModel session)
        {
            return;
        }

        if (!TryGetMutationRangeForKey(editor, e, out var editRange))
        {
            return;
        }

        if (TryBlockUnsafeProtectedRangeEdit(editor, session, editRange))
        {
            e.Handled = true;
        }
    }

    private void OnEditorTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)
            || sender is not TextBox editor
            || DataContext is not EditorSessionViewModel session)
        {
            return;
        }

        var editRange = GetSelectionRange(editor);
        if (TryBlockUnsafeProtectedRangeEdit(editor, session, editRange))
        {
            e.Handled = true;
        }
    }

    private static bool TryGetMutationRangeForKey(TextBox editor, KeyEventArgs e, out DocumentTextRange editRange)
    {
        editRange = DocumentTextRange.Empty;
        var selectionRange = GetSelectionRange(editor);

        if (HasCommandModifier(e.KeyModifiers))
        {
            if (e.Key is Key.V or Key.X)
            {
                editRange = selectionRange;
                return true;
            }

            return false;
        }

        if (!selectionRange.IsEmpty)
        {
            if (e.Key is Key.Back or Key.Delete or Key.Enter or Key.Space)
            {
                editRange = selectionRange;
                return true;
            }

            return false;
        }

        var textLength = editor.Text?.Length ?? 0;
        var caret = Math.Clamp(editor.CaretIndex, 0, textLength);
        switch (e.Key)
        {
            case Key.Back when caret > 0:
                editRange = new DocumentTextRange(caret - 1, caret);
                return true;
            case Key.Delete when caret < textLength:
                editRange = new DocumentTextRange(caret, caret + 1);
                return true;
            case Key.Enter:
            case Key.Space:
                editRange = new DocumentTextRange(caret, caret);
                return true;
            default:
                return false;
        }
    }

    private static DocumentTextRange GetSelectionRange(TextBox editor)
        => DocumentTextRange.FromBounds(editor.SelectionStart, editor.SelectionEnd);

    private static bool HasCommandModifier(KeyModifiers modifiers)
        => (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;

    private static bool TryBlockUnsafeProtectedRangeEdit(
        TextBox editor,
        EditorSessionViewModel session,
        DocumentTextRange editRange)
    {
        if (!MarkdownEditorProtectedRangeScanner.IsUnsafeEdit(editor.Text, editRange))
        {
            return false;
        }

        session.SetStatusMessage(session.EditorProtectedImageDataMessage);
        editor.Focus();
        return true;
    }

    private void OnSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        SetSplitterDraggingState(sender, isDragging: false);

        if (DataContext is not EditorSessionViewModel session)
        {
            return;
        }

        var grid = this.FindControl<Grid>("EditGrid");
        if (grid is null || grid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        var leftWidth = grid.ColumnDefinitions[0].ActualWidth;
        var rightWidth = grid.ColumnDefinitions[2].ActualWidth;
        var totalWidth = leftWidth + rightWidth;
        if (totalWidth <= 0)
        {
            return;
        }

        session.SplitRatio = leftWidth / totalWidth;
    }

    private void OnSplitterPointerPressed(object? sender, PointerPressedEventArgs e)
        => SetSplitterDraggingState(sender, isDragging: true);

    private void OnSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
        => SetSplitterDraggingState(sender, isDragging: false);

    private void OnSplitterPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        => SetSplitterDraggingState(sender, isDragging: false);

    private void ApplySplitRatio()
    {
        if (DataContext is not EditorSessionViewModel session)
        {
            return;
        }

        var grid = this.FindControl<Grid>("EditGrid");
        if (grid is null || grid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        var ratio = Math.Clamp(session.SplitRatio, 0.2, 0.8);
        grid.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
        grid.ColumnDefinitions[2].Width = new GridLength(1 - ratio, GridUnitType.Star);
    }

    private void FocusEditorAsync()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var editor = this.FindControl<TextBox>("EditorTextBox");
            editor?.Focus();
        }, DispatcherPriority.Background);
    }

    private static void SetSplitterDraggingState(object? sender, bool isDragging)
    {
        if (sender is Control control)
        {
            control.Classes.Set("dragging", isDragging);
        }
    }
}
