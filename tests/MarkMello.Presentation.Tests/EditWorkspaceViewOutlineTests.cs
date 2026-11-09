using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Edit-mode outline host (ADR-0008 Rule 6). Keyboard focus and the resulting
/// scroll position are not asserted: the headless platform has no real focus
/// manager and the editor's text presenter is not reliably laid out.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class EditWorkspaceViewOutlineTests
{
    private const string Text = "# One\nalpha\n\n## Two\nbeta";

    private readonly AvaloniaHeadlessFixture _fixture;

    public EditWorkspaceViewOutlineTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task NavigatePlacesCaretAtStartOfTheLine()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (view, editor) = CreateAttachedView();

            Assert.True(view.TryNavigateToSourceLine(3));

            Assert.Equal(Text.IndexOf("## Two", StringComparison.Ordinal), editor.CaretIndex);
            Assert.Equal(3, view.CurrentSourceLine);
        }, CancellationToken.None);
    }

    [Fact]
    public Task InitialSourceLineIsTheCaretLine()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView();

            Assert.Equal(0, view.CurrentSourceLine);
        }, CancellationToken.None);
    }

    [Fact]
    public Task LineChangeRaisesOnceAndSameLineMoveRaisesNothing()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (view, editor) = CreateAttachedView();
            var raised = 0;
            view.CurrentSourceLineChanged += (_, _) => raised++;

            editor.CaretIndex = Text.IndexOf("beta", StringComparison.Ordinal);
            Assert.Equal(1, raised);
            Assert.Equal(4, view.CurrentSourceLine);

            editor.CaretIndex += 2;
            Assert.Equal(1, raised);
            Assert.Equal(4, view.CurrentSourceLine);
        }, CancellationToken.None);
    }

    [Fact]
    public Task WithoutSubscribersCaretMovesAreNotTrackedButTheLineIsReadFresh()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (view, editor) = CreateAttachedView();
            var cache = typeof(EditWorkspaceView).GetField(
                "_outlineCaretLine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            Assert.Null(cache.GetValue(view));

            editor.CaretIndex = Text.IndexOf("beta", StringComparison.Ordinal);

            Assert.Null(cache.GetValue(view));
            Assert.Equal(4, view.CurrentSourceLine);

            var raised = 0;
            view.CurrentSourceLineChanged += (_, _) => raised++;
            editor.CaretIndex += 1;
            Assert.Equal(0, raised);

            editor.CaretIndex = 0;
            Assert.Equal(1, raised);
            Assert.Equal(0, view.CurrentSourceLine);
        }, CancellationToken.None);
    }

    [Fact]
    public Task LinePastTheEndClampsToTheEndOfTheText()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (view, editor) = CreateAttachedView();

            Assert.True(view.TryNavigateToSourceLine(500));

            Assert.Equal(Text.Length, editor.CaretIndex);
        }, CancellationToken.None);
    }

    private static (EditWorkspaceView View, TextBox Editor) CreateAttachedView()
    {
        var session = new EditorSessionViewModel(
            "Draft.md",
            Text,
            ReadingPreferences.Default,
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            imageSourceResolver: null);
        var view = new EditWorkspaceView { DataContext = session };
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        for (var pass = 0; pass < 6; pass++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }


        var editor = view.GetVisualDescendants().OfType<TextBox>().First(static box => box.Name == "EditorTextBox");
        return (view, editor);
    }
}
