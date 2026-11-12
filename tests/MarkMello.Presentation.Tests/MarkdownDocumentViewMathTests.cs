using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownDocumentViewMathTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownDocumentViewMathTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task DisplayMathBlockCreatesMathVisual()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                new MarkdownMathBlock(@"R = \frac{a}{b}"),
            ]));

            var math = view.GetVisualDescendants().OfType<MarkdownMathBlockVisual>().ToList();
            Assert.Single(math);
            Assert.True(math[0].Bounds.Width > 0);
            Assert.True(math[0].Bounds.Height > 0);
        }, CancellationToken.None);

    [Fact]
    public Task InvalidDisplayMathFallsBackToSourceInsteadOfBlank()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                new MarkdownMathBlock(@"\undefinedcommand{x}"),
            ]));

            Assert.Empty(view.GetVisualDescendants().OfType<MarkdownMathBlockVisual>());
            var fallback = view.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
            Assert.Contains("undefinedcommand", fallback.Text ?? string.Empty, StringComparison.Ordinal);
        }, CancellationToken.None);

    [Fact]
    public Task InlineMathKeepsParagraphTextAroundFormula()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                new MarkdownParagraphBlock(
                [
                    new MarkdownTextInline("before "),
                    new MarkdownMathInline("x^2", IsDisplay: false),
                    new MarkdownTextInline(" after"),
                ]),
            ]));

            var paragraph = view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().Single();
            Assert.Contains("before", paragraph.StyledText.Text, StringComparison.Ordinal);
            Assert.Contains("after", paragraph.StyledText.Text, StringComparison.Ordinal);
            Assert.Single(paragraph.StyledText.Math);
            Assert.True(paragraph.Bounds.Width > 0);
        }, CancellationToken.None);

    private static MarkdownDocumentView CreateView(RenderedMarkdownDocument document)
    {
        var view = new MarkdownDocumentView
        {
            Document = document,
            ReadingPreferences = ReadingPreferences.Default,
        };
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        window.UpdateLayout();
        window.Measure(new Size(800, 600));
        window.Arrange(new Rect(0, 0, 800, 600));
        window.UpdateLayout();
        return view;
    }
}
