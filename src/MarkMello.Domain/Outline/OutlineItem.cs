namespace MarkMello.Domain;

/// <summary>
/// Пункт оглавления. Идентифицируется строкой исходника: она одинаково понятна
/// viewer'у (прокрутка к заголовку) и редактору (курсор на строку).
/// </summary>
public sealed record OutlineItem(int Level, string Text, int SourceLine, IReadOnlyList<OutlineItem> Children);

public sealed record DocumentOutline(IReadOnlyList<OutlineItem> Items)
{
    public static DocumentOutline Empty { get; } = new(Array.Empty<OutlineItem>());

    public bool IsEmpty => Items.Count == 0;
}
