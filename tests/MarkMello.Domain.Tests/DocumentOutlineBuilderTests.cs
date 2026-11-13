using MarkMello.Domain;

namespace MarkMello.Domain.Tests;

public sealed class DocumentOutlineBuilderTests
{
    private static MarkdownHeadingBlock H(int level, string text, int line)
        => new(level, [new MarkdownTextInline(text)]) { SourceSpan = new MarkdownSourceSpan(line) };

    private static RenderedMarkdownDocument Doc(params MarkdownBlock[] blocks) => new(blocks);

    [Fact]
    public void EmptyDocumentGivesEmptyOutline()
        => Assert.True(DocumentOutlineBuilder.Build(RenderedMarkdownDocument.Empty, 6).IsEmpty);

    [Fact]
    public void NestsHeadingsByLevel()
    {
        var outline = DocumentOutlineBuilder.Build(
            Doc(H(1, "Guide", 0), H(2, "Install", 2), H(3, "Windows", 4), H(2, "Run", 6)), 6);

        var root = Assert.Single(outline.Items);
        Assert.Equal("Guide", root.Text);
        Assert.Equal(["Install", "Run"], root.Children.Select(c => c.Text));
        Assert.Equal("Windows", Assert.Single(root.Children[0].Children).Text);
        Assert.Equal(4, root.Children[0].Children[0].SourceLine);
    }

    [Fact]
    public void SkippedLevelAttachesToNearestShallowerHeading()
    {
        var outline = DocumentOutlineBuilder.Build(Doc(H(1, "A", 0), H(3, "C", 1)), 6);

        Assert.Equal("C", Assert.Single(Assert.Single(outline.Items).Children).Text);
    }

    [Fact]
    public void HeadingsDeeperThanMaxLevelAreDropped()
    {
        var outline = DocumentOutlineBuilder.Build(Doc(H(1, "A", 0), H(2, "B", 1), H(3, "C", 2)), 2);

        Assert.Empty(Assert.Single(Assert.Single(outline.Items).Children).Children);
    }

    [Fact]
    public void FirstHeadingDeeperThanRootIsStillRoot()
    {
        var outline = DocumentOutlineBuilder.Build(Doc(H(2, "B", 0), H(1, "A", 3)), 6);

        Assert.Equal(["B", "A"], outline.Items.Select(i => i.Text));
    }

    [Fact]
    public void HeadingWithoutSourceSpanIsSkipped()
    {
        var noSpan = new MarkdownHeadingBlock(1, [new MarkdownTextInline("X")]);

        Assert.True(DocumentOutlineBuilder.Build(Doc(noSpan), 6).IsEmpty);
    }

    [Fact]
    public void InlineMarkupBecomesPlainText()
    {
        var heading = new MarkdownHeadingBlock(1,
        [
            new MarkdownStrongInline([new MarkdownTextInline("Bold")]),
            new MarkdownTextInline("  and "),
            new MarkdownCodeInline("code"),
            new MarkdownLinkInline([new MarkdownTextInline(" link")], "#x", null)
        ]) { SourceSpan = new MarkdownSourceSpan(0) };

        Assert.Equal("Bold and code link", Assert.Single(DocumentOutlineBuilder.Build(Doc(heading), 6).Items).Text);
    }

    [Fact]
    public void ParagraphsAreIgnored()
    {
        var outline = DocumentOutlineBuilder.Build(
            Doc(new MarkdownParagraphBlock([new MarkdownTextInline("# not a heading")])), 6);

        Assert.True(outline.IsEmpty);
    }
}
