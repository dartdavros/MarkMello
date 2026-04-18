using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownDisplayLayoutModelTests
{
    [Fact]
    public void CreateWrapsInlineCodeWithDisplayOnlyPaddingMarkers()
    {
        var styled = MarkdownStyledText.FromInlines(
        [
            new MarkdownTextInline("See "),
            new MarkdownCodeInline("docs"),
            new MarkdownTextInline(" now")
        ]);

        var model = MarkdownDisplayLayoutModel.Create(styled);

        Assert.Equal(styled.Text.Length + 2, model.DisplayLength);
        Assert.Single(model.CodeBoxes);

        var codeBox = model.CodeBoxes[0];
        Assert.Equal(new DocumentTextRange(4, 8), codeBox.CanonicalRange);
        Assert.Equal(4, model.GetDisplayStartForCanonicalCaret(4));
        Assert.Equal(10, model.GetDisplayEndForCanonicalCaret(8));

        Assert.Equal(4, model.GetCanonicalCaretForDisplayCaret(4));
        Assert.Equal(4, model.GetCanonicalCaretForDisplayCaret(5));
        Assert.Equal(8, model.GetCanonicalCaretForDisplayCaret(9));
        Assert.Equal(8, model.GetCanonicalCaretForDisplayCaret(10));
    }
}
