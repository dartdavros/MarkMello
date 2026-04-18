namespace MarkMello.Domain;

/// <summary>
/// Логический фрагмент канонического text flow документа.
/// Используется для связи глобального plain-text представления документа
/// с отдельными semantic/render fragments.
/// </summary>
public sealed record MarkdownDocumentTextFragment
{
    public MarkdownDocumentTextFragment(
        string key,
        MarkdownDocumentTextFragmentKind kind,
        DocumentTextRange range,
        string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(text);

        if (range.Length != text.Length)
        {
            throw new ArgumentException("Fragment range length must match fragment text length.", nameof(range));
        }

        Key = key;
        Kind = kind;
        Range = range;
        Text = text;
    }

    public string Key { get; }

    public MarkdownDocumentTextFragmentKind Kind { get; }

    public DocumentTextRange Range { get; }

    public string Text { get; }
}

/// <summary>
/// Вид логического текстового фрагмента в document-wide selection model.
/// Значения остаются domain-level и не привязаны к конкретному UI toolkit.
/// </summary>
public enum MarkdownDocumentTextFragmentKind
{
    Paragraph = 0,
    Heading = 1,
    CodeBlock = 2,
    TableCell = 3,
    Fallback = 4
}
