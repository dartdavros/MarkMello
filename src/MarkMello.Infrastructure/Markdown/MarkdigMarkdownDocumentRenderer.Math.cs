using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkMello.Domain;

namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Promotes a display formula written on its own line to a block formula.
///
/// Markdig reports both <c>$...$</c> and <c>$$...$$</c> as <see cref="MathInline"/>,
/// so a display formula never reaches the <c>MathBlock</c> path. Left as an
/// inline run it occupies a single text line while the formula itself can be
/// more than three times taller (nested fractions reach ~100px), so it paints
/// over the paragraph below.
///
/// The paragraph is split around such formulas: the text before and after keeps
/// its own paragraph and each <c>$$</c> that stands alone on its line becomes a
/// <see cref="MarkdownMathBlock"/>. Double dollars embedded mid-sentence stay
/// inline — that is a deliberate authoring choice.
/// </summary>
public sealed partial class MarkdigMarkdownDocumentRenderer
{
    private static bool TrySplitDisplayMathParagraph(
        ParagraphBlock paragraph,
        string source,
        List<MarkdownBlock> target)
    {
        if (paragraph.Inline is not { } inline)
        {
            return false;
        }

        var hasDisplayMath = false;
        foreach (var node in inline)
        {
            if (node is MathInline { DelimiterCount: >= 2 })
            {
                hasDisplayMath = true;
                break;
            }
        }

        if (!hasDisplayMath)
        {
            return false;
        }

        // Walk the inline stream once, collecting runs of text and the formulas
        // that stand alone on a line.
        var parts = new List<(bool IsMath, List<MarkdownInline> Inlines, string Latex)>();
        var current = new List<MarkdownInline>();

        // Tracks whether the previous source node ended a line. The converted
        // form turns a soft break into a plain space, so the question must be
        // answered from the Markdig nodes, not from the converted ones.
        var atLineStart = true;

        void CloseTextPart()
        {
            TrimEdgeLineBreaks(current);
            if (current.Count > 0)
            {
                parts.Add((false, current.ToList(), string.Empty));
            }

            current.Clear();
        }

        foreach (var node in inline)
        {
            if (node is MathInline { DelimiterCount: >= 2 } displayMath)
            {
                if (atLineStart)
                {
                    CloseTextPart();
                    var latex = displayMath.Content.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(latex))
                    {
                        parts.Add((true, new List<MarkdownInline>(), latex));
                    }

                    atLineStart = false;
                    continue;
                }

                // Mid-sentence $$: keep it inline, the author meant it that way.
                AddConvertedInline(node, current);
                atLineStart = false;
                continue;
            }

            AddConvertedInline(node, current);

            atLineStart = node switch
            {
                LineBreakInline => true,
                LiteralInline literal => literal.Content.ToString().Trim().Length == 0,
                _ => false,
            };
        }

        CloseTextPart();

        var mathCount = 0;
        foreach (var part in parts)
        {
            if (part.IsMath)
            {
                mathCount++;
            }
        }

        if (mathCount == 0)
        {
            return false;
        }

        foreach (var part in parts)
        {
            target.Add(part.IsMath
                ? WithSourceSpan(new MarkdownMathBlock(part.Latex), paragraph, source)
                : WithSourceSpan(new MarkdownParagraphBlock(part.Inlines), paragraph, source));
        }

        return true;
    }

    /// <summary>
    /// True when the formula starts a fresh line: nothing but line breaks and
    /// whitespace precede it in the current run.
    /// </summary>
    private static bool IsOnOwnLine(List<MarkdownInline> current)
    {
        for (var i = current.Count - 1; i >= 0; i--)
        {
            var node = current[i];
            if (node is MarkdownLineBreakInline)
            {
                return true;
            }

            if (node is MarkdownTextInline text && string.IsNullOrWhiteSpace(text.Text))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static void TrimEdgeLineBreaks(List<MarkdownInline> nodes)
    {
        while (nodes.Count > 0 && IsBlank(nodes[^1]))
        {
            nodes.RemoveAt(nodes.Count - 1);
        }

        while (nodes.Count > 0 && IsBlank(nodes[0]))
        {
            nodes.RemoveAt(0);
        }

        static bool IsBlank(MarkdownInline node)
            => node is MarkdownLineBreakInline
                || (node is MarkdownTextInline text && string.IsNullOrWhiteSpace(text.Text));
    }
}
