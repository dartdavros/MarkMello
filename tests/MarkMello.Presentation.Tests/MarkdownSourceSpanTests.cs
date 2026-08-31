using MarkMello.Domain;
using MarkMello.Infrastructure.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Source spans feed edit-mode scroll synchronization, so a span that stops
/// short of its block sends the preview to the wrong place.
/// </summary>
public sealed class MarkdownSourceSpanTests
{
    [Fact]
    public void FencedCodeBlockSpanCoversItsFenceLines()
    {
        const string markdown = """
            Intro paragraph.

            ```csharp
            var x = 1;
            var y = 2;
            ```

            Outro paragraph.
            """;

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);
        var code = Assert.IsType<MarkdownCodeBlock>(document.Blocks[1]);

        Assert.NotNull(code.SourceSpan);

        // Lines 2..5: opening fence, two code lines, closing fence.
        Assert.Equal(2, code.SourceSpan!.Value.StartLine);
        Assert.Equal(5, code.SourceSpan!.Value.EndLine);
    }

    [Fact]
    public void UnclosedFencedCodeBlockSpanCountsOnlyTheOpeningFence()
    {
        const string markdown = """
            Intro paragraph.

            ```csharp
            var x = 1;
            """;

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);
        var code = Assert.IsType<MarkdownCodeBlock>(document.Blocks[1]);

        Assert.NotNull(code.SourceSpan);
        Assert.Equal(2, code.SourceSpan!.Value.StartLine);
        Assert.Equal(3, code.SourceSpan!.Value.EndLine);
    }

    [Fact]
    public void ParagraphSpanCoversEveryLineItWasWrittenOn()
    {
        const string markdown = """
            First line of the paragraph
            second line of the paragraph
            third line of the paragraph

            Next paragraph.
            """;

        var document = new MarkdigMarkdownDocumentRenderer().Render(markdown);
        var paragraph = Assert.IsType<MarkdownParagraphBlock>(document.Blocks[0]);

        Assert.NotNull(paragraph.SourceSpan);
        Assert.Equal(0, paragraph.SourceSpan!.Value.StartLine);
        Assert.Equal(2, paragraph.SourceSpan!.Value.EndLine);
    }
}
