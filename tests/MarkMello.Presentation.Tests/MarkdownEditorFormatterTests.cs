using MarkMello.Presentation.Editing;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownEditorFormatterTests
{
    [Fact]
    public void ApplyWrapsSelectedTextForBold()
    {
        var result = MarkdownEditorFormatter.Apply(
            "hello world",
            MarkdownEditorFormatKind.Bold,
            selectionStart: 6,
            selectionEnd: 11);

        Assert.Equal("hello **world**", result.Text);
        Assert.Equal(15, result.SelectionStart);
        Assert.Equal(result.SelectionStart, result.SelectionEnd);
    }

    [Fact]
    public void ApplyInsertsLinkPlaceholderWhenSelectionIsEmpty()
    {
        var result = MarkdownEditorFormatter.Apply(
            "hello",
            MarkdownEditorFormatKind.Link,
            selectionStart: 5,
            selectionEnd: 5);

        Assert.Equal("hello[link](url)", result.Text);
        Assert.Equal(16, result.SelectionStart);
        Assert.Equal(result.SelectionStart, result.SelectionEnd);
    }

    [Fact]
    public void ApplyNormalizesReversedSelectionForListInsertion()
    {
        var result = MarkdownEditorFormatter.Apply(
            "alpha beta",
            MarkdownEditorFormatKind.List,
            selectionStart: 10,
            selectionEnd: 6);

        Assert.Equal("alpha \n- beta", result.Text);
        Assert.Equal(13, result.SelectionStart);
        Assert.Equal(result.SelectionStart, result.SelectionEnd);
    }
}
