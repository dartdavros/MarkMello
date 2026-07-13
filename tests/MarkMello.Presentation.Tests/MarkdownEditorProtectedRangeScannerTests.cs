using MarkMello.Domain;
using MarkMello.Presentation.Editing;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownEditorProtectedRangeScannerTests
{
    [Fact]
    public void FindDataImageDefinitionRangesReturnsWholeDefinitionLineIncludingLineBreak()
    {
        const string source = "before\n[image1]: <data:image/png;base64,AQIDBA==>\nafter";

        var range = Assert.Single(MarkdownEditorProtectedRangeScanner.FindDataImageDefinitionRanges(source));

        Assert.Equal("[image1]: <data:image/png;base64,AQIDBA==>\n", source[range.Start..range.End]);
    }

    [Fact]
    public void IsUnsafeEditBlocksPartialEditInsideDataImageDefinition()
    {
        const string source = "[image1]: <data:image/png;base64,AQIDBA==>\n";
        var editStart = source.IndexOf("AQID", StringComparison.Ordinal);

        var unsafeEdit = MarkdownEditorProtectedRangeScanner.IsUnsafeEdit(
            source,
            new DocumentTextRange(editStart, editStart + 1));

        Assert.True(unsafeEdit);
    }

    [Fact]
    public void IsUnsafeEditAllowsReplacingWholeDataImageDefinitionLine()
    {
        const string source = "[image1]: <data:image/png;base64,AQIDBA==>\n";
        var protectedRange = Assert.Single(MarkdownEditorProtectedRangeScanner.FindDataImageDefinitionRanges(source));

        var unsafeEdit = MarkdownEditorProtectedRangeScanner.IsUnsafeEdit(source, protectedRange);

        Assert.False(unsafeEdit);
    }
}
