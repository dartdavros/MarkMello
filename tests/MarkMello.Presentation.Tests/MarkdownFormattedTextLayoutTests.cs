using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using MarkMello.Presentation.Views.Markdown;
using AvaloniaApplication = Avalonia.Application;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownFormattedTextLayoutTests
{
    [Fact]
    public void LayoutCreatesOneVisualLinePerExplicitLineBreak()
    {
        EnsureAvaloniaStarted();

        using var layout = CreateLayout("first\nsecond\nthird");

        Assert.Equal(3, layout.GetLineMetrics().Count);
    }

    [Fact]
    public void CaretHitTestMapsVisualLinesToCanonicalLineStarts()
    {
        EnsureAvaloniaStarted();

        const string text = "first\nsecond\nthird";
        using var layout = CreateLayout(text);
        var lines = layout.GetLineMetrics();

        Assert.Equal(text.IndexOf("second", StringComparison.Ordinal), layout.GetCanonicalCaretOffset(GetLineStartPoint(lines[1])));
        Assert.Equal(text.IndexOf("third", StringComparison.Ordinal), layout.GetCanonicalCaretOffset(GetLineStartPoint(lines[2])));
    }

    private static Point GetLineStartPoint(MarkdownFormattedTextLineMetrics metrics)
        => new(0, metrics.Bounds.Y + metrics.Bounds.Height / 2);

    private static void EnsureAvaloniaStarted()
    {
        if (AvaloniaApplication.Current is not null)
        {
            return;
        }

        AppBuilder.Configure<AvaloniaApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
    }

    private static MarkdownFormattedTextLayout CreateLayout(string text)
        => new(
            new MarkdownStyledText(
                text,
                Array.Empty<MarkdownTextStyleSpan>(),
                Array.Empty<MarkdownLinkSpan>(),
                Array.Empty<MarkdownInlineImageSpan>()),
            inlineImages: null,
            baseFontFamily: FontFamily.Default,
            inlineCodeFontFamily: FontFamily.Default,
            baseFontSize: 14,
            baseFontWeight: FontWeight.Normal,
            baseFontStyle: FontStyle.Normal,
            lineHeight: 21,
            letterSpacing: 0,
            textWrapping: TextWrapping.NoWrap,
            maxWidth: 100_000,
            foreground: Brushes.Black,
            linkDecorations: null);
}
