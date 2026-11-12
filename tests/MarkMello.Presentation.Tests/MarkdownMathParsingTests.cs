using MarkMello.Domain;
using MarkMello.Infrastructure.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownMathParsingTests
{
    [Fact]
    public void InlineMathBecomesMathInline()
    {
        var document = new MarkdigMarkdownDocumentRenderer().Render("Energy is $E = mc^2$ here.");

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        var math = Assert.Single(paragraph.Inlines.OfType<MarkdownMathInline>());
        Assert.Equal("E = mc^2", math.Content);
        Assert.False(math.IsDisplay);
    }

    [Fact]
    public void DoubleDollarBlockBecomesMathBlock()
    {
        const string markdown = """
            $$
            R = \alpha P
            $$
            """;

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);

        var math = Assert.IsType<MarkdownMathBlock>(Assert.Single(document.Blocks));
        Assert.Equal("R = \\alpha P", math.Source);
        Assert.NotNull(math.SourceSpan);
    }

    [Fact]
    public void DisplayMathBlockCarriesSourceSpan()
    {
        const string markdown = """
            # Title

            $$
            a + b
            $$
            """;

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);

        Assert.Collection(
            document.Blocks,
            block => Assert.IsType<MarkdownHeadingBlock>(block),
            block =>
            {
                var math = Assert.IsType<MarkdownMathBlock>(block);
                Assert.True(math.SourceSpan!.Value.StartLine >= 2);
            });
    }

    [Fact]
    public void MathIsPreservedInPlainTextMap()
    {
        var document = new MarkdigMarkdownDocumentRenderer().Render("Value $x_1$ and formula.");

        var text = MarkdownDocumentTextMap.Create(document).Text;

        Assert.Contains("x_1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DanglingDollarStaysPlainText()
    {
        var document = new MarkdigMarkdownDocumentRenderer().Render("It costs 5 dollars.");

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        Assert.DoesNotContain(paragraph.Inlines, inline => inline is MarkdownMathInline);
    }
}
