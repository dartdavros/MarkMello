using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

public sealed partial class MarkdownDocumentView
{
    private StackPanel BuildMathBlock(MarkdownMathBlock block)
    {
        var baseFontSize = ReadingPreferences.FontSize;
        var foreground = LookupBrush("MmTextBrush") ?? Brushes.Black;

        var layout = MarkdownMathRenderer.Measure(
            block.Source,
            baseFontSize,
            foreground,
            isDisplay: true);

        var host = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(0, 14, 0, 18),
            HorizontalAlignment = HorizontalAlignment.Center,
            UseLayoutRounding = true
        };

        if (layout is { IsValid: true } valid)
        {
            host.Children.Add(new MarkdownMathBlockVisual(
                block.Source,
                baseFontSize,
                foreground,
                valid.Size));
        }
        else
        {
            // Owner decision: on invalid LaTeX show the source instead of an
            // error ornament, so the author never loses the formula text.
            host.Children.Add(new SelectableTextBlock
            {
                Text = block.Source,
                FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
                FontSize = Math.Max(12, baseFontSize - 2),
                TextWrapping = TextWrapping.Wrap,
                UseLayoutRounding = true
            });
        }

        return host;
    }
}

/// <summary>
/// Block-level display-formula visual. Measures once at construction; the
/// document view rebuilds on typography/theme changes, which re-measures.
/// </summary>
internal sealed class MarkdownMathBlockVisual : Control
{
    private readonly string _latex;
    private readonly double _fontSize;
    private readonly IBrush _foreground;
    private readonly Size _size;

    public MarkdownMathBlockVisual(string latex, double fontSize, IBrush foreground, Size size)
    {
        _latex = latex;
        _fontSize = fontSize;
        _foreground = foreground;
        _size = size;
        HorizontalAlignment = HorizontalAlignment.Center;
        UseLayoutRounding = true;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(Math.Min(_size.Width, double.IsFinite(availableSize.Width) ? availableSize.Width : _size.Width), _size.Height);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = double.IsFinite(Bounds.Width) ? Bounds.Width : _size.Width;
        var origin = new Point(Math.Max(0, (width - _size.Width) / 2), 0);
        MarkdownMathRenderer.Draw(
            context,
            _latex,
            _size,
            origin,
            _fontSize,
            _foreground,
            isDisplay: true);
    }
}
