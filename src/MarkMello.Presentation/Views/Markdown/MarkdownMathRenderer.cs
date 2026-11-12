using Avalonia;
using Avalonia.Media;
using CSharpMath.Avalonia;
using CSharpMath.Rendering.FrontEnd;
using MathPainter = CSharpMath.Avalonia.MathPainter;
using CSharpMathTextAlignment = CSharpMath.Rendering.FrontEnd.TextAlignment;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Renders LaTeX math with the bundled in-process CSharpMath engine
/// (<see cref="MathPainter"/> + <see cref="AvaloniaCanvas"/>). No WebView,
/// JavaScript runtime, network call or external process is involved, which
/// keeps formula support inside the native viewer path.
///
/// The class is stateless and thread-safe for measurement: each call creates
/// its own painter because painter instances hold mutable layout state and
/// edit-mode preview renders may arrive off the UI thread.
///
/// A formula that fails to parse produces a non-null <see cref="Layout.ErrorMessage"/>;
/// the caller decides the fallback (source code is shown instead of silence).
/// </summary>
internal static class MarkdownMathRenderer
{
    // Lazy text layout re-runs Measure/Draw on every rebuild (typing in the
    // editor preview, resize, theme change). Parsing LaTeX is the expensive
    // part, so measured layouts are memoized by (latex, size, colour, style).
    // Dictionary + lock because edit-mode preview may measure off the UI thread.
    private static readonly Dictionary<MarkdownMathCacheKey, MarkdownMathLayout> Cache = new();
    private static readonly object CacheLock = new();
    private const int MaxCacheEntries = 512;

    /// <summary>
    /// Measures the formula for a given font size and text colour. Returns
    /// <c>null</c> when the formula does not parse.
    /// </summary>
    public static MarkdownMathLayout? Measure(
        string latex,
        double fontSize,
        IBrush foreground,
        bool isDisplay)
    {
        if (string.IsNullOrWhiteSpace(latex))
        {
            return null;
        }

        var key = new MarkdownMathCacheKey(latex, Math.Round(fontSize, 2), ResolveColor(foreground), isDisplay);
        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var normalized = MarkdownMathTexNormalizer.Normalize(latex);
        var layout = MeasureCore(normalized, fontSize, foreground, isDisplay);
        lock (CacheLock)
        {
            if (Cache.Count >= MaxCacheEntries)
            {
                Cache.Clear();
            }

            Cache[key] = layout;
        }

        return layout;
    }

    private static MarkdownMathLayout MeasureCore(
        string latex,
        double fontSize,
        IBrush foreground,
        bool isDisplay)
    {
        try
        {
            var painter = CreatePainter(latex, fontSize, foreground, isDisplay);

            // Measure is what materializes Display (UpdateDisplay runs inside
            // it), so it must be called before any Display check.
            var rect = painter.Measure(float.NaN);

            // The engine builds an inline error display for bad LaTeX and sets
            // ErrorMessage; treat that as "invalid", so the caller can fall
            // back to showing the source instead of a red text blob.
            if (painter.ErrorMessage is { Length: > 0 } errorMessage)
            {
                return Invalid(errorMessage);
            }

            if (painter.Display is null)
            {
                return Invalid("Invalid LaTeX formula");
            }

            // rect.Height is the display's own ascent+descent, but for stacked
            // formulas the engine reports that extent relative to its math axis
            // rather than to the ink top (rect.Y + rect.Height > 0). The safe box
            // is therefore ascent + descent, where descent covers everything the
            // display claims below the baseline.
            var display = painter.Display;
            var ascent = Math.Max(1, -rect.Y);
            var claimedDescent = Math.Max(0, rect.Height - ascent);
            var engineDescent = display?.Descent ?? 0;
            var descent = Math.Max(claimedDescent, Math.Max(0, engineDescent));

            var width = Math.Max(1, rect.Width);
            var height = Math.Max(1, ascent + descent);
            var baseline = Math.Clamp(ascent, 0, height);

            return new MarkdownMathLayout(new Size(width, height), baseline, null, rect.Y, rect.Height);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // The engine surfaces malformed LaTeX through exceptions in some
            // paths; treat those as a formula-level error, never a document
            // failure.
            return new MarkdownMathLayout(new Size(0, 0), 0, ex.Message, 0, 0);
        }
    }

    private static MarkdownMathLayout Invalid(string message)
        => new(new Size(0, 0), 0, message, 0, 0);

    /// <summary>
    /// Draws an already measured formula at <paramref name="origin"/>.
    /// The origin is the top-left of the measured box.
    /// </summary>
    public static void Draw(
        DrawingContext context,
        string latex,
        Size size,
        Point origin,
        double fontSize,
        IBrush foreground,
        bool isDisplay)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var painter = CreatePainter(MarkdownMathTexNormalizer.Normalize(latex), fontSize, foreground, isDisplay);

        // The painter centers the display inside the given canvas and flips Y
        // itself. Translate exactly to the box origin and let the painter's own
        // flip handle the rest; callers pass an origin that already accounts for
        // any canvas-height compensation they need (see MarkdownMathBlockVisual).
        using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            var canvas = new AvaloniaCanvas(context, size);
            painter.Draw(canvas, CSharpMathTextAlignment.Center);
        }
    }

    /// <summary>
    /// Display formulas get a larger top-level size than the body text
    /// (owner decision: +10%) so fractions nested one level deep stay legible.
    /// The engine already shrinks nested levels (display -> text -> script);
    /// this scaling lifts the whole stack instead of fighting that rule.
    /// </summary>
    private const double DisplayFontScale = 1.10;

    private static MathPainter CreatePainter(string latex, double fontSize, IBrush foreground, bool isDisplay)
        => new()
        {
            LaTeX = latex,
            FontSize = (float)Math.Max(1, fontSize * (isDisplay ? DisplayFontScale : 1.0)),
            LineStyle = isDisplay ? CSharpMath.Atom.LineStyle.Display : CSharpMath.Atom.LineStyle.Text,
            TextColor = ResolveColor(foreground),
            ErrorColor = ResolveColor(foreground),
        };
    private static Color ResolveColor(IBrush brush)
        => brush is ISolidColorBrush solid ? solid.Color : Colors.Black;
}

internal sealed record MarkdownMathLayout(
    Size Size,
    double Baseline,
    string? ErrorMessage,
    double Top,
    double RawHeight)
{
    public bool IsValid => ErrorMessage is null && Size.Width > 0 && Size.Height > 0;
}

internal readonly record struct MarkdownMathCacheKey(
    string Latex,
    double FontSize,
    Color Color,
    bool IsDisplay);
