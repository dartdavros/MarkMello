using MarkMello.Domain;
using MarkMello.Presentation.Clipboard;

namespace MarkMello.Presentation.Tests.Clipboard;

public sealed class TelegramMarkdownFormatterTests
{
    [Fact]
    public void FormatReturnsEmptyForEmptyDocument()
    {
        var result = TelegramMarkdownFormatter.Format(RenderedMarkdownDocument.Empty);

        Assert.Equal(string.Empty, result);
    }
}
