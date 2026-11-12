using System.Text;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Rewrites LaTeX constructs that the bundled CSharpMath engine does not
/// understand, or that it typesets too tightly compared with LaTeX, into
/// equivalent input that renders correctly:
///
/// <list type="bullet">
/// <item><c>\dfrac</c> / <c>\tfrac</c> — the engine has no such commands and
/// would fall back to raw source. They map to a <c>\displaystyle</c>-scoped
/// <c>\frac</c> / <c>\textstyle</c>-scoped <c>\frac</c>.</item>
/// <item>Binary <c>+</c> / <c>−</c> between two adjacent fragments — the engine
/// gives a binary operator only a thin medium space, so with tall neighbours
/// (two stacked fractions) the sign nearly touches them. A thin space
/// (<c>\,</c>) is inserted on each side when the neighbour ends or starts
/// with a fraction-like group, which keeps the standard TeX look without
/// changing simple cases such as <c>a + b</c>.</item>
/// </list>
///
/// The normalizer is purely textual and deterministic, so its result can be
/// part of the measurement cache key.
/// </summary>
internal static class MarkdownMathTexNormalizer
{
    public static string Normalize(string latex)    {
        if (string.IsNullOrEmpty(latex))
        {
            return latex;
        }

        var expanded = ExpandSizedFractions(latex);
        return ExpandBinaryOperatorSpacing(expanded);
    }

    /// <summary>
    /// Replaces <c>\dfrac{a}{b}</c> with <c>{\displaystyle\frac{a}{b}}</c> and
    /// <c>\tfrac{a}{b}</c> with <c>{\textstyle\frac{a}{b}}</c>, keeping both
    /// arguments intact (balanced-brace aware, so nested fractions survive).
    /// </summary>
    private static string ExpandSizedFractions(string latex)
    {
        if (latex.IndexOf(@"\dfrac", StringComparison.Ordinal) < 0
            && latex.IndexOf(@"\tfrac", StringComparison.Ordinal) < 0)
        {
            return latex;
        }

        var builder = new StringBuilder(latex.Length + 32);
        var index = 0;
        while (index < latex.Length)
        {
            if (TryReadSizedFraction(latex, index, out var consumed, out var numerator, out var denominator, out var style))
            {
                builder.Append('{').Append(style).Append(@"\frac{")
                    .Append(ExpandSizedFractions(numerator))
                    .Append("}{")
                    .Append(ExpandSizedFractions(denominator))
                    .Append("}}");
                index += consumed;
                continue;
            }

            builder.Append(latex[index]);
            index++;
        }

        return builder.ToString();
    }

    private static bool TryReadSizedFraction(
        string latex,
        int index,
        out int consumed,
        out string numerator,
        out string denominator,
        out string style)
    {
        consumed = 0;
        numerator = string.Empty;
        denominator = string.Empty;
        style = @"\displaystyle";

        if (!latex.AsSpan(index).StartsWith(@"\dfrac", StringComparison.Ordinal)
            && !latex.AsSpan(index).StartsWith(@"\tfrac", StringComparison.Ordinal))
        {
            return false;
        }

        style = latex.AsSpan(index).StartsWith(@"\tfrac", StringComparison.Ordinal)
            ? @"\textstyle"
            : @"\displaystyle";

        var cursor = index + 6; // skip "\dfrac" / "\tfrac"
        if (!TryReadGroup(latex, ref cursor, out numerator)
            || !TryReadGroup(latex, ref cursor, out denominator))
        {
            return false;
        }

        consumed = cursor - index;
        return true;
    }

    /// <summary>
    /// Reads one <c>{...}</c> argument at <paramref name="cursor"/>, skipping
    /// leading spaces and honouring nested braces. Returns the inner text.
    /// </summary>
    private static bool TryReadGroup(string latex, ref int cursor, out string content)
    {
        content = string.Empty;
        while (cursor < latex.Length && char.IsWhiteSpace(latex[cursor]))
        {
            cursor++;
        }

        if (cursor >= latex.Length || latex[cursor] != '{')
        {
            return false;
        }

        var depth = 0;
        var start = cursor + 1;
        for (var i = cursor; i < latex.Length; i++)
        {
            switch (latex[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        content = latex[start..i];
                        cursor = i + 1;
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    /// <summary>
    /// Inserts a thin space around binary <c>+</c> / <c>−</c> whose neighbour
    /// is a fraction-like group, so the sign does not visually merge with the
    /// tall fraction stacks on either side.
    /// </summary>
    private static string ExpandBinaryOperatorSpacing(string latex)
    {
        var builder = new StringBuilder(latex.Length + 16);
        for (var i = 0; i < latex.Length; i++)
        {
            var ch = latex[i];
            var isBinarySign = ch is '+' or '-' or '\u2212';
            if (isBinarySign && IsBinaryContext(latex, i) && NeedsFractionSpacing(latex, i))
            {
                builder.Append(@"\,\,");
                builder.Append(ch);
                builder.Append(@"\,\,");
                i = SkipFollowingSpaces(latex, i) - 1;
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A sign is binary when it has a token before it (not an opening brace or
    /// another operator) and a token after it.
    /// </summary>
    private static bool IsBinaryContext(string latex, int index)
    {
        var previous = PreviousNonSpace(latex, index);
        var next = SkipFollowingSpaces(latex, index);
        if (next >= latex.Length)
        {
            return false;
        }

        var previousChar = previous >= 0 ? latex[previous] : '\0';
        return previousChar is not ('{' or '[' or '(' or ',' or '\0')
            && previousChar is not ('+' or '-' or '*' or '/' or '=' or '<' or '>')
            && previousChar != '\u2212';
    }

    private static int PreviousNonSpace(string latex, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(latex[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int SkipFollowingSpaces(string latex, int index)
    {
        var i = index + 1;
        while (i < latex.Length && char.IsWhiteSpace(latex[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// True when either neighbour of the sign is a group that starts with a
    /// fraction (plain <c>\frac</c> or the <c>\displaystyle\frac</c> form the
    /// normalizer produces), i.e. a tall stack that the sign can touch.
    /// </summary>
    private static bool NeedsFractionSpacing(string latex, int index)
    {
        return NeighbourIsFractionGroup(latex, index, lookRight: false)
            || NeighbourIsFractionGroup(latex, index, lookRight: true);
    }

    private static bool NeighbourIsFractionGroup(string latex, int index, bool lookRight)
    {
        var cursor = lookRight ? SkipFollowingSpaces(latex, index) : PreviousNonSpace(latex, index);
        if (cursor < 0 || cursor >= latex.Length)
        {
            return false;
        }

        if (lookRight)
        {
            return StartsWithFraction(latex, cursor);
        }

        // Look left: the sign may follow "...}\frac{a}{b}", so walk back to the
        // opening brace of the group that ends right before the sign.
        if (latex[cursor] != '}')
        {
            return false;
        }

        var depth = 0;
        for (var i = cursor; i >= 0; i--)
        {
            if (latex[i] == '}')
            {
                depth++;
            }
            else if (latex[i] == '{')
            {
                depth--;
                if (depth == 0)
                {
                    // The group may be a bare argument of \frac; also check the
                    // fraction that owns it: "\frac{a}{b}" ends with this brace.
                    return StartsWithFraction(latex, SkipSpacesForward(latex, i + 1));
                }
            }
        }

        return false;
    }

    private static int SkipSpacesForward(string latex, int index)
    {
        var i = index;
        while (i < latex.Length && char.IsWhiteSpace(latex[i]))
        {
            i++;
        }

        return i;
    }

    private static bool StartsWithFraction(string latex, int index)
    {
        var span = latex.AsSpan(index);
        return span.StartsWith(@"\frac", StringComparison.Ordinal)
            || span.StartsWith(@"\displaystyle\frac", StringComparison.Ordinal)
            || span.StartsWith(@"\textstyle\frac", StringComparison.Ordinal)
            || span.StartsWith(@"\dfrac", StringComparison.Ordinal)
            || span.StartsWith(@"\tfrac", StringComparison.Ordinal)
            || span.StartsWith(@"\binom", StringComparison.Ordinal);
    }
}
