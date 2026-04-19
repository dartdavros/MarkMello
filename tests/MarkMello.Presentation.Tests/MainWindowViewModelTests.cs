using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task ToggleEditModeCommandLazilyCreatesEditorSession()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);

        Assert.False(harness.ViewModel.IsEditMode);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Same(harness.ViewModel, harness.ViewModel.ActiveDocumentContent);
        Assert.Contains(StartupStage.ReadableDocument, harness.StartupMetrics.Marks);

        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsEditMode);
        Assert.NotNull(harness.ViewModel.EditorSession);
        Assert.Same(harness.ViewModel.EditorSession, harness.ViewModel.ActiveDocumentContent);
        Assert.Equal("Reading", harness.ViewModel.EditToggleLabel);
        Assert.Equal(1, harness.StartupMetrics.Marks.Count(stage => stage == StartupStage.EditorActivation));
    }

    [Fact]
    public async Task ToggleEditModeCommandWhenDirtyShowsPromptAndDiscardLeavesEditMode()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "changed";

        Assert.True(harness.ViewModel.IsDirty);
        Assert.Equal("one.md •", harness.ViewModel.TitleFileDisplayName);

        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.True(harness.ViewModel.IsEditMode);
        Assert.Contains("reading mode", harness.ViewModel.DirtyPromptMessage, StringComparison.OrdinalIgnoreCase);

        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.False(harness.ViewModel.IsEditMode);
        Assert.False(harness.ViewModel.IsDirty);
        Assert.Equal("alpha beta", harness.ViewModel.Document!.Content);
    }

    [Fact]
    public async Task OpenDroppedFileAsyncWhenEditorIsDirtyDefersNavigationUntilDiscard()
    {
        var harness = CreateHarness();
        var firstPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var secondPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "two.md");
        harness.Loader.Sources[firstPath] = CreateSource(firstPath, "first");
        harness.Loader.Sources[secondPath] = CreateSource(secondPath, "second");

        await harness.ViewModel.OpenPathAsync(firstPath);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first changed";

        await harness.ViewModel.OpenDroppedFileAsync(secondPath);

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Equal("one.md", harness.ViewModel.FileName);
        Assert.Equal("first", harness.ViewModel.Document!.Content);

        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.False(harness.ViewModel.IsEditMode);
        Assert.Equal("two.md", harness.ViewModel.FileName);
        Assert.Equal("second", harness.ViewModel.Document!.Content);
    }

    [Fact]
    public async Task ReloadCommandWhenEditorIsDirtyPromptsAndReloadsIntoEditorSession()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first changed";
        harness.Loader.Sources[path] = CreateSource(path, "reloaded");

        await harness.ViewModel.ReloadCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Equal("first", harness.ViewModel.Document!.Content);
        Assert.Equal("first changed", harness.ViewModel.EditorSession.SourceText);

        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.True(harness.ViewModel.IsEditMode);
        Assert.False(harness.ViewModel.IsDirty);
        Assert.Equal("reloaded", harness.ViewModel.Document!.Content);
        Assert.Equal("reloaded", harness.ViewModel.EditorSession.SourceText);
    }

    [Fact]
    public async Task SaveCommandPersistsEditorBufferAndClearsDirtyState()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first updated";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(path, save.Path);
        Assert.Equal("first updated", save.Content);
        Assert.False(harness.ViewModel.IsDirty);
        Assert.Equal("first updated", harness.ViewModel.Document!.Content);
        Assert.Equal("one.md", harness.ViewModel.TitleFileDisplayName);
    }

    [Fact]
    public async Task SaveCommandWhenSavingFailsKeepsDirtyStateAndShowsStatusMessage()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");
        harness.DocumentSaver.NextException = new UnauthorizedAccessException("blocked");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first updated";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsEditMode);
        Assert.True(harness.ViewModel.IsDirty);
        Assert.Equal("first", harness.ViewModel.Document!.Content);
        Assert.Equal($"Access denied: {path}", harness.ViewModel.EditorSession.StatusMessage);
    }

    [Fact]
    public async Task TryQueueCloseRequestWhenDirtyPromptsAndContinuesCloseAfterDiscard()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var closeRequested = false;
        harness.Loader.Sources[path] = CreateSource(path, "first");
        harness.ViewModel.CloseRequested += (_, _) => closeRequested = true;

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first updated";

        var queued = harness.ViewModel.TryQueueCloseRequest();

        Assert.True(queued);
        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.False(closeRequested);

        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.True(closeRequested);
    }

    [Fact]
    public async Task SaveAsCommandUsesPickerPathAndUpdatesDocumentIdentity()
    {
        var harness = CreateHarness();
        var originalPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var savedAsPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "renamed.md");
        harness.Loader.Sources[originalPath] = CreateSource(originalPath, "first");
        harness.FilePicker.SavePath = savedAsPath;

        await harness.ViewModel.OpenPathAsync(originalPath);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first updated";

        await harness.ViewModel.SaveAsCommand.ExecuteAsync(null);

        Assert.Equal(["one.md"], harness.FilePicker.SuggestedSaveFileNames);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(savedAsPath, save.Path);
        Assert.Equal("first updated", harness.ViewModel.Document!.Content);
        Assert.Equal(savedAsPath, harness.ViewModel.Document.Path);
        Assert.Equal("renamed.md", harness.ViewModel.FileName);
        Assert.False(harness.ViewModel.IsDirty);
    }

    private static MarkdownSource CreateSource(string path, string content)
        => new(path, Path.GetFileName(path), content);

    private static TestHarness CreateHarness()
    {
        var loader = new StubDocumentLoader();
        var saver = new RecordingDocumentSaver();
        var picker = new StubFilePicker();
        var settings = new InMemorySettingsStore();
        var themeService = new RecordingThemeService();
        var startupMetrics = new RecordingStartupMetrics();
        var viewModel = new MainWindowViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(saver),
            picker,
            new StubCommandLineActivation(),
            settings,
            themeService,
            startupMetrics,
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer()));

        return new TestHarness(loader, saver, picker, startupMetrics, viewModel);
    }

    private sealed record TestHarness(
        StubDocumentLoader Loader,
        RecordingDocumentSaver DocumentSaver,
        StubFilePicker FilePicker,
        RecordingStartupMetrics StartupMetrics,
        MainWindowViewModel ViewModel);
}
