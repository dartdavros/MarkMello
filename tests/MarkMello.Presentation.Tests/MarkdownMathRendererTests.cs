using Avalonia.Media;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownMathRendererTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownMathRendererTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task InlineFormulaMeasuresWithPositiveSize()
        => _fixture.Session.Dispatch(() =>
        {
            var layout = MarkdownMathRenderer.Measure("E = mc^2", 16, Brushes.Black, isDisplay: false);

            Assert.NotNull(layout);
            Assert.True(layout!.IsValid, layout.ErrorMessage);
            Assert.True(layout.Size.Width > 0);
            Assert.True(layout.Size.Height > 0);
        }, CancellationToken.None);

    [Fact]
    public Task DisplayFormulaWithFractionAndSubscriptMeasures()
        => _fixture.Session.Dispatch(() =>
        {
            var layout = MarkdownMathRenderer.Measure(@"R_{SRH} = \frac{n}{\alpha}", 16, Brushes.Black, isDisplay: true);

            Assert.NotNull(layout);
            Assert.True(layout!.IsValid, layout.ErrorMessage);
            Assert.True(layout.Size.Width > 0);
        }, CancellationToken.None);

    [Fact]
    public Task InvalidLatexReportsErrorInsteadOfThrowing()
        => _fixture.Session.Dispatch(() =>
        {
            var layout = MarkdownMathRenderer.Measure(@"\undefinedcommand{", 16, Brushes.Black, isDisplay: false);

            Assert.NotNull(layout);
            Assert.False(layout!.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(layout.ErrorMessage));
        }, CancellationToken.None);
}
