namespace MarkMello.Domain;

/// <summary>
/// Глобальный диапазон текста документа в модели document-wide selection.
/// End является exclusive.
/// </summary>
public readonly record struct DocumentTextRange
{
    public static DocumentTextRange Empty { get; } = new(0, 0);

    public DocumentTextRange(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start, nameof(end));

        Start = start;
        End = end;
    }

    public int Start { get; }

    public int End { get; }

    public int Length => End - Start;

    public bool IsEmpty => Length == 0;

    public bool Contains(int offset) => offset >= Start && offset < End;

    public bool Intersects(DocumentTextRange other) => Start < other.End && End > other.Start;

    public DocumentTextRange Intersection(DocumentTextRange other)
    {
        var start = Math.Max(Start, other.Start);
        var end = Math.Min(End, other.End);
        return end <= start ? Empty : new DocumentTextRange(start, end);
    }

    public static DocumentTextRange FromBounds(int first, int second)
    {
        return first <= second
            ? new DocumentTextRange(first, second)
            : new DocumentTextRange(second, first);
    }

    public int ClampOffset(int offset)
    {
        if (offset < Start)
        {
            return Start;
        }

        if (offset > End)
        {
            return End;
        }

        return offset;
    }
}
