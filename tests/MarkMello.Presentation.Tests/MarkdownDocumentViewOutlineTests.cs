using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Controls;
using MarkMello.Domain;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownDocumentViewOutlineTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownDocumentViewOutlineTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task ScrollsToHeadingBySourceLineAndReportsIt()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var blocks = new List<MarkdownBlock>();
            for (var i = 0; i < 40; i++)
            {
                blocks.Add(new MarkdownHeadingBlock(2, [new MarkdownTextInline($"Section {i}")])
                {
                    SourceSpan = new MarkdownSourceSpan(i * 10)
                });
                blocks.Add(new MarkdownParagraphBlock([new MarkdownTextInline("text text text")])
                {
                    SourceSpan = new MarkdownSourceSpan((i * 10) + 2)
                });
            }

            var view = new MarkdownDocumentView
            {
                Document = new RenderedMarkdownDocument(blocks),
                ReadingPreferences = ReadingPreferences.Default
            };
            var scroll = new ScrollViewer { Content = view, Template = ScrollTemplate() };
            var window = new Window { Width = 600, Height = 300, Content = scroll };
            window.Show();
            Layout(window);

            Assert.True(scroll.ScrollBarMaximum.Y > 0);
            Assert.Equal(0, scroll.Offset.Y);
            Assert.Equal(0, view.GetCurrentHeadingSourceLine());

            Assert.True(view.TryScrollToHeadingSourceLine(200));
            Layout(window);

            Assert.True(scroll.Offset.Y > 0);
            Assert.Equal(200, view.GetCurrentHeadingSourceLine());
            Assert.False(view.TryScrollToHeadingSourceLine(7));
        }, CancellationToken.None);
    }

    private static void Layout(Window window)
    {
        window.UpdateLayout();
        window.Measure(new Avalonia.Size(window.Width, window.Height));
        window.Arrange(new Avalonia.Rect(0, 0, window.Width, window.Height));
        window.UpdateLayout();
    }

    // Headless app has no theme, so ScrollViewer needs a minimal template to scroll.
    private static FuncControlTemplate ScrollTemplate()
        => new((_, scope) => new ScrollContentPresenter { Name = "PART_ContentPresenter" }
            .RegisterInNameScope(scope));
}
