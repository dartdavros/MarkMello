using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Editing;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

public sealed class EditorSessionViewModelTests
{
    [Fact]
    public void SourceTextChangeMarksSessionDirtyAndUpdatesPreview()
    {
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var session = CreateSession(path, "alpha beta");

        Assert.False(session.IsDirty);
        Assert.Equal(2, session.WordCount);
        Assert.Equal("alpha beta", ExtractPlainText(session.RenderedPreview));
        Assert.Equal(Path.GetDirectoryName(path), session.RenderedPreview.BaseDirectory);

        session.SourceText = "alpha beta gamma";

        Assert.True(session.IsDirty);
        Assert.Equal(3, session.WordCount);
        Assert.Equal("alpha beta gamma", ExtractPlainText(session.RenderedPreview));
        Assert.Equal(Path.GetDirectoryName(path), session.RenderedPreview.BaseDirectory);
    }

    [Fact]
    public void DraftSessionStartsWithoutPathAndKeepsInitialContentClean()
    {
        var session = new EditorSessionViewModel(
            "Untitled.md",
            "alpha beta",
            ReadingPreferences.Default,
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            imageSourceResolver: null);

        Assert.Null(session.CurrentPath);
        Assert.Equal("Untitled.md", session.FileName);
        Assert.Equal("alpha beta", session.SourceText);
        Assert.Equal("alpha beta", session.LastPersistedSource);
        Assert.False(session.IsDirty);
        Assert.Null(session.RenderedPreview.BaseDirectory);
    }

    [Fact]
    public void ApplySavedDocumentResetsDirtyStateAndUpdatesIdentity()
    {
        var originalPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var savedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "two.md");
        var session = CreateSession(originalPath, "alpha");
        session.SourceText = "alpha updated";

        session.ApplySavedDocument(new MarkdownSource(savedPath, "two.md", "beta gamma"));

        Assert.False(session.IsDirty);
        Assert.Equal(savedPath, session.CurrentPath);
        Assert.Equal("two.md", session.FileName);
        Assert.Equal("beta gamma", session.SourceText);
        Assert.Equal("beta gamma", session.LastPersistedSource);
        Assert.Equal(Path.GetDirectoryName(savedPath), session.RenderedPreview.BaseDirectory);
    }

    [Fact]
    public void DiscardChangesRevertsSourceAndClearsStatusMessage()
    {
        var session = CreateSession(Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md"), "alpha");
        session.SourceText = "beta";
        session.SetStatusMessage("Couldn't save the document.");

        session.DiscardChanges();

        Assert.False(session.IsDirty);
        Assert.Equal("alpha", session.SourceText);
        Assert.False(session.HasStatusMessage);
        Assert.Equal(string.Empty, session.StatusMessage);
    }

    [Fact]
    public void KeystrokesCoalesceIntoASinglePreviewRender()
    {
        var renderer = new TestMarkdownRenderer();
        var scheduler = new ManualEditorPreviewScheduler();
        var session = CreateSession("alpha", renderer, scheduler);

        // One render for the initial document; typing must not add one per character.
        Assert.Equal(1, renderer.RenderCount);

        session.SourceText = "alph";
        session.SourceText = "alp";
        session.SourceText = "alpha beta";

        Assert.Equal(3, scheduler.ScheduleCount);
        Assert.Equal(1, renderer.RenderCount);
        Assert.Equal("alpha", ExtractPlainText(session.RenderedPreview));

        scheduler.Flush();

        Assert.Equal(2, renderer.RenderCount);
        Assert.Equal("alpha beta", ExtractPlainText(session.RenderedPreview));
    }

    [Fact]
    public void SourceTextChangeUpdatesDirtyStateAndMetricsBeforePreviewCatchesUp()
    {
        var renderer = new TestMarkdownRenderer();
        var scheduler = new ManualEditorPreviewScheduler();
        var session = CreateSession("alpha", renderer, scheduler);

        session.SourceText = "alpha beta gamma";

        // Dirty state and counters are cheap and must stay synchronous with typing.
        Assert.True(session.IsDirty);
        Assert.Equal(3, session.WordCount);
        Assert.Equal(1, renderer.RenderCount);
    }

    [Fact]
    public void ApplyLoadedDocumentRendersImmediatelyAndDropsPendingPreview()
    {
        var renderer = new TestMarkdownRenderer();
        var scheduler = new ManualEditorPreviewScheduler();
        var session = CreateSession("alpha", renderer, scheduler);
        session.SourceText = "stale draft";

        var loadedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "two.md");
        session.ApplyLoadedDocument(new MarkdownSource(loadedPath, "two.md", "beta gamma"));

        Assert.False(scheduler.HasPendingRender);
        Assert.Equal("beta gamma", ExtractPlainText(session.RenderedPreview));
        Assert.Equal(2, renderer.RenderCount);
    }

    [Fact]
    public void DiscardChangesRendersImmediatelyAndDropsPendingPreview()
    {
        var renderer = new TestMarkdownRenderer();
        var scheduler = new ManualEditorPreviewScheduler();
        var session = CreateSession("alpha", renderer, scheduler);
        session.SourceText = "stale draft";

        session.DiscardChanges();

        Assert.False(scheduler.HasPendingRender);
        Assert.Equal("alpha", ExtractPlainText(session.RenderedPreview));
    }

    private static EditorSessionViewModel CreateSession(
        string content,
        TestMarkdownRenderer renderer,
        IEditorPreviewScheduler scheduler)
        => new(
            "one.md",
            content,
            ReadingPreferences.Default,
            new RenderMarkdownDocumentUseCase(renderer, new FakeDiagramRenderService()),
            imageSourceResolver: null,
            localization: null,
            previewScheduler: scheduler);

    private static EditorSessionViewModel CreateSession(string path, string content)
        => new(
            new MarkdownSource(path, Path.GetFileName(path), content),
            ReadingPreferences.Default,
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            imageSourceResolver: null);

    private static string ExtractPlainText(RenderedMarkdownDocument document)
    {
        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        var text = Assert.IsType<MarkdownTextInline>(Assert.Single(paragraph.Inlines));
        return text.Text;
    }
}
