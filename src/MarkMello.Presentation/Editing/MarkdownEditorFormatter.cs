namespace MarkMello.Presentation.Editing;

public enum MarkdownEditorFormatKind
{
    Bold,
    Italic,
    Code,
    Link,
    List,
    Quote
}

public readonly record struct MarkdownEditorEditResult(
    string Text,
    int SelectionStart,
    int SelectionEnd);

/// <summary>
/// Pure text transforms for the lightweight M5 markdown editor toolbar.
/// Uses the same insertion rules as the design prototype.
/// </summary>
public static class MarkdownEditorFormatter
{
    public static MarkdownEditorEditResult Apply(
        string? source,
        MarkdownEditorFormatKind kind,
        int selectionStart,
        int selectionEnd)
    {
        var currentText = source ?? string.Empty;
        var textLength = currentText.Length;

        var start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, textLength);
        var end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, textLength);

        var before = currentText[..start];
        var selected = currentText[start..end];
        var after = currentText[end..];

        if (string.IsNullOrEmpty(selected))
        {
            selected = kind switch
            {
                MarkdownEditorFormatKind.Bold => "bold text",
                MarkdownEditorFormatKind.Italic => "italic text",
                MarkdownEditorFormatKind.Code => "code",
                MarkdownEditorFormatKind.Link => "link",
                MarkdownEditorFormatKind.List => "item",
                MarkdownEditorFormatKind.Quote => "quoted text",
                _ => string.Empty
            };
        }

        var wrapped = kind switch
        {
            MarkdownEditorFormatKind.Bold => $"**{selected}**",
            MarkdownEditorFormatKind.Italic => $"*{selected}*",
            MarkdownEditorFormatKind.Code => $"`{selected}`",
            MarkdownEditorFormatKind.Link => $"[{selected}](url)",
            MarkdownEditorFormatKind.List => $"\n- {selected}",
            MarkdownEditorFormatKind.Quote => $"\n> {selected}",
            _ => selected
        };

        var nextText = before + wrapped + after;
        var caretIndex = start + wrapped.Length;

        return new MarkdownEditorEditResult(nextText, caretIndex, caretIndex);
    }
}
