using MarkMello.Application.UseCases;
using MarkMello.Domain;
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
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer()),
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

    private static EditorSessionViewModel CreateSession(string path, string content)
        => new(
            new MarkdownSource(path, Path.GetFileName(path), content),
            ReadingPreferences.Default,
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer()),
            imageSourceResolver: null);

    private static string ExtractPlainText(RenderedMarkdownDocument document)
    {
        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        var text = Assert.IsType<MarkdownTextInline>(Assert.Single(paragraph.Inlines));
        return text.Text;
    }
}
