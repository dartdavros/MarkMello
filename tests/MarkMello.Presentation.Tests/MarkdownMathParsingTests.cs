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
    public void SingleLineDisplayMathBecomesMathBlock()
    {
        // Markdig reports $$...$$ as MathInline, so a standalone one-line
        // formula must be promoted to a block or it occupies one text line
        // while painting a much taller fraction over the next paragraph.
        const string markdown = """
            Текст до.

            $$R_s = \frac{a}{b}$$

            Текст после.
            """;

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);

        Assert.Collection(
            document.Blocks,
            block => Assert.IsType<MarkdownParagraphBlock>(block),
            block =>
            {
                var math = Assert.IsType<MarkdownMathBlock>(block);
                Assert.Equal(@"R_s = \frac{a}{b}", math.Source);
            },
            block => Assert.IsType<MarkdownParagraphBlock>(block));
    }

    [Fact]
    public void DisplayMathInsideListItemBecomesMathBlock()
    {
        const string markdown = """
            - **Формула**:

              $$R_s = \frac{a}{b}$$

              Пояснение.
            """;

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);

        var list = Assert.IsType<MarkdownListBlock>(Assert.Single(document.Blocks));
        var blocks = Assert.Single(list.Items).Blocks;
        Assert.Collection(
            blocks,
            block => Assert.IsType<MarkdownParagraphBlock>(block),
            block => Assert.Equal(@"R_s = \frac{a}{b}", Assert.IsType<MarkdownMathBlock>(block).Source),
            block => Assert.IsType<MarkdownParagraphBlock>(block));
    }

    [Fact]
    public void InlineMathInsideSentenceStaysInline()
    {
        const string markdown = "Значение $R_{surf}$ здесь.";

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        var math = Assert.Single(paragraph.Inlines.OfType<MarkdownMathInline>());
        Assert.False(math.IsDisplay);
    }

    [Fact]
    public void DisplayMathWithSurroundingTextStaysInline()
    {
        // Double dollars mid-sentence are the author's inline choice; only a
        // paragraph consisting solely of the formula is promoted.
        const string markdown = "До $$a + b$$ после.";

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        Assert.Single(paragraph.Inlines.OfType<MarkdownMathInline>());
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
