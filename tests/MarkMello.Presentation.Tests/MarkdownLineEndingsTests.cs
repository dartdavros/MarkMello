using Avalonia;
using Avalonia.Controls;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Editing;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownLineEndingsTests
{
    [Fact]
    public void DetectReturnsUnixForAnLfDocument()
        => Assert.Equal("\n", MarkdownLineEndings.Detect("alpha\nbeta\ngamma\n"));

    [Fact]
    public void DetectReturnsWindowsForACrlfDocument()
        => Assert.Equal("\r\n", MarkdownLineEndings.Detect("alpha\r\nbeta\r\ngamma\r\n"));

    [Fact]
    public void DetectFollowsTheMajorityInAMixedDocument()
    {
        Assert.Equal("\n", MarkdownLineEndings.Detect("a\nb\nc\r\n"));
        Assert.Equal("\r\n", MarkdownLineEndings.Detect("a\r\nb\r\nc\n"));
    }

    [Fact]
    public void DetectFallsBackToThePlatformNewLineWithoutLineBreaks()
    {
        Assert.Equal(Environment.NewLine, MarkdownLineEndings.Detect(string.Empty));
        Assert.Equal(Environment.NewLine, MarkdownLineEndings.Detect(null));
        Assert.Equal(Environment.NewLine, MarkdownLineEndings.Detect("single line"));
    }

    [Fact]
    public void SessionReportsTheLineEndingOfTheLoadedDocument()
    {
        var session = new EditorSessionViewModel(
            "one.md",
            "alpha\nbeta\n",
            ReadingPreferences.Default,
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            imageSourceResolver: null);

        Assert.Equal("\n", session.DocumentNewLine);

        session.ApplyLoadedDocument(new MarkdownSource(
            Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "two.md"),
            "two.md",
            "alpha\r\nbeta\r\n"));

        Assert.Equal("\r\n", session.DocumentNewLine);
    }
}

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class EditorNewLineBindingTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public EditorNewLineBindingTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task EditorInsertsTheLineEndingTheDocumentAlreadyUses()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var session = new EditorSessionViewModel(
                "one.md",
                "alpha\nbeta\n",
                ReadingPreferences.Default,
                new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
                imageSourceResolver: null);

            var view = new EditWorkspaceView { DataContext = session };
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            window.Measure(new Size(800, 600));
            window.Arrange(new Rect(0, 0, 800, 600));

            var editor = view.GetControl<TextBox>("EditorTextBox");

            // Without this the editor would insert CRLF into an LF document on
            // Windows, leaving it permanently different from what was loaded.
            Assert.Equal("\n", editor.NewLine);
        }, CancellationToken.None);
    }
}
