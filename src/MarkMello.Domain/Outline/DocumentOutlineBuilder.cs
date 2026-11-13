namespace MarkMello.Domain;

/// <summary>
/// Строит оглавление из уже разобранного документа (ADR-0008 Rule 10): только
/// заголовки верхнего уровня документа, без повторного парсинга исходника.
/// Пропущенный уровень не создаёт пустых узлов (Rule 5).
/// </summary>
public static class DocumentOutlineBuilder
{
    public static DocumentOutline Build(RenderedMarkdownDocument document, int maxLevel)
    {
        var roots = new List<Node>();
        var stack = new Stack<Node>();

        foreach (var block in document.Blocks)
        {
            if (block is not MarkdownHeadingBlock { SourceSpan: { } span } heading || heading.Level > maxLevel)
            {
                continue;
            }

            var node = new Node(heading.Level, MarkdownInlinePlainText.From(heading.Inlines), span.StartLine);
            while (stack.Count > 0 && stack.Peek().Level >= node.Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().Children.Add(node);
            }

            stack.Push(node);
        }

        return roots.Count == 0 ? DocumentOutline.Empty : new DocumentOutline(roots.Select(Freeze).ToArray());
    }

    private static OutlineItem Freeze(Node node)
        => new(node.Level, node.Text, node.SourceLine, node.Children.Select(Freeze).ToArray());

    private sealed record Node(int Level, string Text, int SourceLine)
    {
        public List<Node> Children { get; } = [];
    }
}
