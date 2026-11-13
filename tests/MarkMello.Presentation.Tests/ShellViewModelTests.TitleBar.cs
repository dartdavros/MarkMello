namespace MarkMello.Presentation.Tests;

public sealed partial class ShellViewModelTests
{
    [Fact]
    public async Task SavedIndicatorTracksEditingFailedSaveAndSuccessfulSave()
    {
        var harness = CreateHarness();
        var shell = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "saved.md");
        harness.Loader.Sources[path] = CreateSource(path, "original");
        await shell.OpenPathAsync(path);
        Assert.True(shell.IsCurrentDocumentSaved);

        var changed = new List<string?>();
        shell.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        await shell.ToggleEditModeCommand.ExecuteAsync(null);
        shell.EditorSession!.SourceText = "edited";
        Assert.False(shell.IsCurrentDocumentSaved);
        Assert.Contains(nameof(shell.IsCurrentDocumentSaved), changed);

        harness.DocumentSaver.NextException = new IOException("Save failed");
        await shell.SaveCommand.ExecuteAsync(null);
        Assert.False(shell.IsCurrentDocumentSaved);

        changed.Clear();
        harness.DocumentSaver.NextException = null;
        await shell.SaveCommand.ExecuteAsync(null);
        Assert.True(shell.IsCurrentDocumentSaved);
        Assert.Contains(nameof(shell.IsCurrentDocumentSaved), changed);
    }

    [Fact]
    public async Task EmptyNewDocumentRemainsUnsavedUntilItHasBeenSavedToAFile()
    {
        var harness = CreateHarness();
        var shell = harness.ViewModel;
        await shell.CreateNewDocumentCommand.ExecuteAsync(null);

        Assert.False(shell.IsDirty);
        Assert.False(shell.IsCurrentDocumentSaved);

        harness.FilePicker.SavePath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "draft.md");
        await shell.SaveCommand.ExecuteAsync(null);

        Assert.True(shell.IsCurrentDocumentSaved);
        Assert.Equal("draft.md", shell.FileName);
    }
}
