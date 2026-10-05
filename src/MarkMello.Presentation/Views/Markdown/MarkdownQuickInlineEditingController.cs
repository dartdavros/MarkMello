using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.Editing;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Управляет жизненным циклом инлайн-редактора блока в режиме просмотра Markdown-документа.
/// </summary>
internal sealed class MarkdownQuickInlineEditingController
{
    private readonly MarkdownDocumentView _owner;
    private MarkdownQuickEditorControl? _activeQuickEditor;
    private Control? _quickEditorTargetControl;
    private Panel? _quickEditorParentPanel;
    private int _quickEditorCharStart;
    private int _quickEditorCharEnd;
    private string _quickEditorOriginalBlockText = string.Empty;
    private bool _isCommittingQuickEditor;

    public MarkdownQuickInlineEditingController(MarkdownDocumentView owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public MarkdownQuickEditorControl? ActiveEditor => _activeQuickEditor;

    public bool HasActiveEditor => _activeQuickEditor is not null;

    public bool IsPointerOverActiveEditor(object? source, Point viewPosition)
    {
        if (_activeQuickEditor is null)
        {
            return false;
        }

        if (source is Visual sourceVisual && (ReferenceEquals(sourceVisual, _activeQuickEditor) || IsVisualDescendantOf(sourceVisual, _activeQuickEditor)))
        {
            return true;
        }

        if (_activeQuickEditor.Parent is Visual parentVisual)
        {
            var editorPoint = _owner.TranslatePoint(viewPosition, parentVisual);
            if (editorPoint is not null && _activeQuickEditor.Bounds.Contains(editorPoint.Value))
            {
                return true;
            }
        }

        return false;
    }

    public void BeginQuickEdit(
        MarkdownDocumentSelectionFragmentBase fragment,
        Point localPosition,
        IReadOnlyList<MarkdownSourceLineVisualAnchor> sourceLineAnchors,
        Visual rootVisual)
    {
        CommitQuickEditor();

        if (string.IsNullOrEmpty(_owner.SourceText))
        {
            return;
        }

        Visual? current = fragment;
        MarkdownSourceLineVisualAnchor? matchedAnchor = null;

        while (current is not null && !ReferenceEquals(current, rootVisual))
        {
            for (var i = sourceLineAnchors.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(sourceLineAnchors[i].Control, current))
                {
                    matchedAnchor = sourceLineAnchors[i];
                    break;
                }
            }

            if (matchedAnchor is not null)
            {
                break;
            }

            current = current.GetVisualParent();
        }

        if (matchedAnchor is null)
        {
            return;
        }

        var targetControl = matchedAnchor.Value.Control;
        var span = matchedAnchor.Value.SourceSpan;
        var parentPanel = targetControl.Parent as Panel;

        if (parentPanel is null)
        {
            return;
        }

        if (!MarkdownSourceBlockLocator.TryExtractLines(_owner.SourceText, span.StartLine, span.EndLine, out var blockText, out var charStart, out var charEnd))
        {
            return;
        }

        var editor = new MarkdownQuickEditorControl
        {
            Text = blockText,
            Margin = targetControl.Margin,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        editor.MinHeight = Math.Max(24, targetControl.Bounds.Height);

        if (targetControl is MarkdownSelectionTextFragment textFrag)
        {
            editor.FontSize = textFrag.BaseFontSize;
            editor.FontFamily = textFrag.BaseFontFamily;
            editor.FontWeight = textFrag.BaseFontWeight;
            editor.FontStyle = textFrag.BaseFontStyle;
            if (!double.IsNaN(textFrag.BaseLineHeight))
            {
                editor.LineHeight = textFrag.BaseLineHeight;
            }
        }
        else if (targetControl is TextBlock tb)
        {
            editor.FontSize = tb.FontSize;
            editor.FontFamily = tb.FontFamily;
            editor.FontWeight = tb.FontWeight;
            editor.FontStyle = tb.FontStyle;
            if (!double.IsNaN(tb.LineHeight))
            {
                editor.LineHeight = tb.LineHeight;
            }
        }
        else if (fragment is MarkdownSelectionTextFragment fallbackFrag)
        {
            editor.FontSize = fallbackFrag.BaseFontSize;
            editor.FontFamily = fallbackFrag.BaseFontFamily;
            editor.FontWeight = fallbackFrag.BaseFontWeight;
            editor.FontStyle = fallbackFrag.BaseFontStyle;
            if (!double.IsNaN(fallbackFrag.BaseLineHeight))
            {
                editor.LineHeight = fallbackFrag.BaseLineHeight;
            }
        }

        var localFragOffset = Math.Clamp(fragment.GetDocumentOffset(localPosition) - fragment.DocumentRange.Start, 0, blockText.Length);
        var caretOffset = localFragOffset;

        // If block has a markdown prefix like "# " or "- " or "> " not in the rendered fragment text,
        // offset the caret position into the raw markdown text accordingly
        if (targetControl is not null && !string.IsNullOrEmpty(blockText))
        {
            var prefixLen = 0;
            while (prefixLen < blockText.Length && (blockText[prefixLen] == '#' || blockText[prefixLen] == '>' || blockText[prefixLen] == '-' || blockText[prefixLen] == '*' || blockText[prefixLen] == ' ' || char.IsDigit(blockText[prefixLen]) || blockText[prefixLen] == '.'))
            {
                if (blockText[prefixLen] == ' ')
                {
                    prefixLen++;
                    break;
                }
                prefixLen++;
            }

            if (prefixLen > 0 && prefixLen + localFragOffset <= blockText.Length)
            {
                caretOffset = prefixLen + localFragOffset;
            }
        }

        _quickEditorTargetControl = targetControl;
        _quickEditorParentPanel = parentPanel;
        _quickEditorCharStart = charStart;
        _quickEditorCharEnd = charEnd;
        _quickEditorOriginalBlockText = blockText;
        _activeQuickEditor = editor;

        targetControl.IsVisible = false;

        var index = parentPanel.Children.IndexOf(targetControl);
        if (index >= 0)
        {
            parentPanel.Children.Insert(index + 1, editor);
        }
        else
        {
            parentPanel.Children.Add(editor);
        }

        editor.CommitRequested += (_, _) => CommitQuickEditor();
        editor.CancelRequested += (_, _) => CancelQuickEditor();

        Dispatcher.UIThread.Post(() =>
        {
            editor.Focus();
            editor.SetInitialCaret(caretOffset);
        }, DispatcherPriority.Input);
    }

    public void CommitQuickEditor()
    {
        if (_isCommittingQuickEditor || _activeQuickEditor is null || _quickEditorParentPanel is null || _quickEditorTargetControl is null)
        {
            return;
        }

        _isCommittingQuickEditor = true;
        try
        {
            var editor = _activeQuickEditor;
            var targetControl = _quickEditorTargetControl;
            var parentPanel = _quickEditorParentPanel;
            var newBlockText = editor.Text ?? string.Empty;
            var charStart = _quickEditorCharStart;
            var charEnd = _quickEditorCharEnd;
            var originalText = _quickEditorOriginalBlockText;
            var source = _owner.SourceText;

            _activeQuickEditor = null;
            _quickEditorTargetControl = null;
            _quickEditorParentPanel = null;

            parentPanel.Children.Remove(editor);
            targetControl.IsVisible = true;

            if (!string.IsNullOrEmpty(source) && !string.Equals(newBlockText, originalText, StringComparison.Ordinal))
            {
                var newFullText = MarkdownSourceBlockLocator.ReplaceRange(source, charStart, charEnd, newBlockText);
                _owner.RaiseDocumentContentEdited(newFullText);
            }
        }
        finally
        {
            _isCommittingQuickEditor = false;
        }
    }

    public void CancelQuickEditor()
    {
        if (_activeQuickEditor is null || _quickEditorParentPanel is null || _quickEditorTargetControl is null)
        {
            return;
        }

        var editor = _activeQuickEditor;
        var targetControl = _quickEditorTargetControl;
        var parentPanel = _quickEditorParentPanel;

        _activeQuickEditor = null;
        _quickEditorTargetControl = null;
        _quickEditorParentPanel = null;

        parentPanel.Children.Remove(editor);
        targetControl.IsVisible = true;
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
}
