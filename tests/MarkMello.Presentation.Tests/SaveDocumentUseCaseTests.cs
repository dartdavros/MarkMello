using MarkMello.Application.UseCases;

namespace MarkMello.Presentation.Tests;

public sealed class SaveDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncSavesToNormalizedMarkdownPath()
    {
        var saver = new RecordingDocumentSaver();
        var useCase = new SaveDocumentUseCase(saver);
        var pathWithoutExtension = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "draft");

        var result = await useCase.ExecuteAsync(pathWithoutExtension, "# hello");

        var success = Assert.IsType<SaveDocumentResult.Success>(result);
        Assert.EndsWith("draft.md", success.Source.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("draft.md", success.Source.FileName);
        Assert.Equal("# hello", success.Source.Content);

        var save = Assert.Single(saver.Saves);
        Assert.Equal(success.Source.Path, save.Path);
        Assert.Equal("# hello", save.Content);
    }

    [Fact]
    public async Task ExecuteAsyncReturnsAccessDeniedWhenSaverRejectsWrite()
    {
        var saver = new RecordingDocumentSaver
        {
            NextException = new UnauthorizedAccessException("nope")
        };
        var useCase = new SaveDocumentUseCase(saver);
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "blocked.md");

        var result = await useCase.ExecuteAsync(path, "content");

        var denied = Assert.IsType<SaveDocumentResult.AccessDenied>(result);
        Assert.Equal(Path.GetFullPath(path), denied.Path);
    }

    [Fact]
    public async Task ExecuteAsyncReturnsWriteErrorWhenIoFails()
    {
        var saver = new RecordingDocumentSaver
        {
            NextException = new IOException("disk full")
        };
        var useCase = new SaveDocumentUseCase(saver);
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "broken.md");

        var result = await useCase.ExecuteAsync(path, "content");

        var writeError = Assert.IsType<SaveDocumentResult.WriteError>(result);
        Assert.Equal(Path.GetFullPath(path), writeError.Path);
        Assert.Contains("disk full", writeError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsyncReturnsInvalidPathForUnsupportedExtension()
    {
        var saver = new RecordingDocumentSaver();
        var useCase = new SaveDocumentUseCase(saver);
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "notes.pdf");

        var result = await useCase.ExecuteAsync(path, "content");

        var invalid = Assert.IsType<SaveDocumentResult.InvalidPath>(result);
        Assert.Equal(path, invalid.Path);
        Assert.Empty(saver.Saves);
    }
}
