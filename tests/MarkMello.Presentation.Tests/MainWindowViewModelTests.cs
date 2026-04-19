using MarkMello.Application.UseCases;
using MarkMello.Application.Updates;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;
using MarkMello.Presentation.ViewModels;
using System.Globalization;

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
    public void ToggleAppMenuCommandOpensMenuAndClearErrorClosesOverlay()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);

        Assert.True(harness.ViewModel.IsAppMenuOpen);
        Assert.True(harness.ViewModel.IsAppOverlayOpen);
        Assert.True(harness.ViewModel.HasOpenOverlay);

        harness.ViewModel.ClearErrorCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppMenuOpen);
        Assert.False(harness.ViewModel.HasOpenOverlay);
    }

    [Fact]
    public void ToggleSettingsCommandReplacesAppMenuWithReadingSettings()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);
        harness.ViewModel.ToggleSettingsCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppMenuOpen);
        Assert.True(harness.ViewModel.IsSettingsOpen);
        Assert.False(harness.ViewModel.IsAppOverlayOpen);
    }

    [Fact]
    public void OpenAppSettingsCommandSwitchesFromMenuToAppSettings()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);
        harness.ViewModel.OpenAppSettingsCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppMenuOpen);
        Assert.True(harness.ViewModel.IsAppSettingsOpen);
        Assert.True(harness.ViewModel.IsAppOverlayOpen);

        harness.ViewModel.ReturnToAppMenuCommand.Execute(null);

        Assert.True(harness.ViewModel.IsAppMenuOpen);
        Assert.False(harness.ViewModel.IsAppSettingsOpen);
    }

    [Fact]
    public void OpenAboutCommandSwitchesFromSettingsToAboutAndBack()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);
        harness.ViewModel.OpenAppSettingsCommand.Execute(null);
        harness.ViewModel.OpenAboutCommand.Execute(null);

        Assert.True(harness.ViewModel.IsAppAboutOpen);
        Assert.True(harness.ViewModel.IsAppOverlayOpen);
        Assert.False(harness.ViewModel.IsAppSettingsOpen);

        harness.ViewModel.ReturnToAppSettingsCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppAboutOpen);
        Assert.True(harness.ViewModel.IsAppSettingsOpen);
    }

    [Fact]
    public async Task CreateNewDocumentCommandStartsInEditModeWithUnsavedDraft()
    {
        var harness = CreateHarness();

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsViewer);
        Assert.True(harness.ViewModel.IsEditMode);
        Assert.Null(harness.ViewModel.Document);
        Assert.NotNull(harness.ViewModel.EditorSession);
        Assert.Null(harness.ViewModel.EditorSession.CurrentPath);
        Assert.Equal("Untitled.md", harness.ViewModel.FileName);
        Assert.Equal("Untitled.md — MarkMello", harness.ViewModel.WindowTitle);
        Assert.Contains(StartupStage.EditorActivation, harness.StartupMetrics.Marks);
        Assert.DoesNotContain(StartupStage.ReadableDocument, harness.StartupMetrics.Marks);
    }

    [Fact]
    public async Task CloseFileCommandReturnsViewingDocumentToWelcome()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);

        await harness.ViewModel.CloseFileCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.IsViewer);
        Assert.Null(harness.ViewModel.Document);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Equal("MarkMello", harness.ViewModel.WindowTitle);
        Assert.False(harness.ViewModel.CloseFileCommand.CanExecute(null));
    }

    [Fact]
    public async Task CloseFileCommandWhenDirtyDraftPromptsAndDiscardReturnsToWelcome()
    {
        var harness = CreateHarness();

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "# Draft";

        await harness.ViewModel.CloseFileCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Contains("closing the current document", harness.ViewModel.DirtyPromptMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(harness.ViewModel.IsEditMode);

        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Null(harness.ViewModel.Document);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Equal("MarkMello", harness.ViewModel.WindowTitle);
    }

    [Fact]
    public async Task CloseFileCommandWhenDirtyAndSavedPersistsThenReturnsToWelcome()
    {
        var harness = CreateHarness();
        var savedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "close-after-save.md");
        harness.FilePicker.SavePath = savedPath;

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first draft";

        await harness.ViewModel.CloseFileCommand.ExecuteAsync(null);
        await harness.ViewModel.ConfirmDirtySaveCommand.ExecuteAsync(null);

        Assert.Equal(["Untitled.md"], harness.FilePicker.SuggestedSaveFileNames);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(savedPath, save.Path);
        Assert.Equal("first draft", save.Content);
        Assert.True(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Null(harness.ViewModel.Document);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Equal("MarkMello", harness.ViewModel.WindowTitle);
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
    public async Task SaveCommandForNewDocumentUsesSaveAsPickerAndCreatesDocumentIdentity()
    {
        var harness = CreateHarness();
        var savedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "draft.md");
        harness.FilePicker.SavePath = savedPath;

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first draft";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["Untitled.md"], harness.FilePicker.SuggestedSaveFileNames);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(savedPath, save.Path);
        Assert.Equal("first draft", save.Content);
        Assert.Equal(savedPath, harness.ViewModel.Document!.Path);
        Assert.Equal("draft.md", harness.ViewModel.FileName);
        Assert.False(harness.ViewModel.IsDirty);
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

    [Fact]
    public async Task CheckForUpdatesCommandWhenUpdateAvailableShowsDownloadAction()
    {
        var harness = CreateHarness();
        var package = CreateUpdatePackage();
        harness.UpdateService.NextCheckResult = new UpdateCheckResult.UpdateAvailable(package);

        await harness.ViewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Update 1.2.3 available", harness.ViewModel.UpdateStatusTitle);
        Assert.Contains(package.AssetName, harness.ViewModel.UpdateStatusMessage, StringComparison.Ordinal);
        Assert.True(harness.ViewModel.CanDownloadAvailableUpdate);
        Assert.False(harness.ViewModel.CanOpenDownloadedUpdate);
        Assert.Equal("Available", harness.ViewModel.UpdateStateBadge);
    }

    [Fact]
    public async Task DownloadUpdateCommandWhenSuccessfulShowsNativeAction()
    {
        var harness = CreateHarness();
        var package = CreateUpdatePackage();
        var downloadedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", package.AssetName);
        harness.UpdateService.NextCheckResult = new UpdateCheckResult.UpdateAvailable(package);
        harness.UpdateService.NextDownloadResult = new UpdateDownloadResult.Success(package, downloadedPath);

        await harness.ViewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        await harness.ViewModel.DownloadUpdateCommand.ExecuteAsync(null);

        Assert.Equal("Update ready", harness.ViewModel.UpdateStatusTitle);
        Assert.Contains(package.AssetName, harness.ViewModel.UpdateStatusMessage, StringComparison.Ordinal);
        Assert.False(harness.ViewModel.CanDownloadAvailableUpdate);
        Assert.True(harness.ViewModel.CanOpenDownloadedUpdate);
        Assert.Equal("Launch installer", harness.ViewModel.DownloadedUpdateActionLabel);
        Assert.Equal(downloadedPath, harness.ViewModel.DownloadedUpdatePath);
        Assert.Equal("Ready", harness.ViewModel.UpdateStateBadge);
    }

    [Fact]
    public async Task OpenDownloadedUpdateCommandWhenSuccessfulUpdatesStatus()
    {
        var harness = CreateHarness();
        var package = CreateUpdatePackage();
        var downloadedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", package.AssetName);
        harness.UpdateService.NextCheckResult = new UpdateCheckResult.UpdateAvailable(package);
        harness.UpdateService.NextDownloadResult = new UpdateDownloadResult.Success(package, downloadedPath);
        harness.UpdateService.NextPrepareResult =
            new UpdatePrepareResult.Success("Installer launched. Follow the native upgrade flow.");

        await harness.ViewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        await harness.ViewModel.DownloadUpdateCommand.ExecuteAsync(null);
        await harness.ViewModel.OpenDownloadedUpdateCommand.ExecuteAsync(null);

        Assert.Equal("Native update flow started", harness.ViewModel.UpdateStatusTitle);
        Assert.Equal(
            "Installer launched. Follow the native upgrade flow.",
            harness.ViewModel.UpdateStatusMessage);
    }

    private static MarkdownSource CreateSource(string path, string content)
        => new(path, Path.GetFileName(path), content);

    private static AppUpdatePackage CreateUpdatePackage()
        => new(
            CurrentVersion: "1.0.0",
            ReleaseVersion: "1.2.3",
            ReleaseTag: "v1.2.3",
            PublishedAt: DateTimeOffset.Parse("2026-04-19T12:00:00Z", CultureInfo.InvariantCulture),
            ReleasePageUrl: "https://github.com/dartdavros/MarkMello/releases/tag/v1.2.3",
            AssetName: "MarkMello-setup-win-x64.exe",
            DownloadUrl: "https://github.com/dartdavros/MarkMello/releases/download/v1.2.3/MarkMello-setup-win-x64.exe",
            PlatformName: "Windows",
            ArchitectureName: "x64",
            InstallAction: AppUpdateInstallAction.LaunchInstaller);

    private static TestHarness CreateHarness()
    {
        var loader = new StubDocumentLoader();
        var saver = new RecordingDocumentSaver();
        var picker = new StubFilePicker();
        var settings = new InMemorySettingsStore();
        var themeService = new RecordingThemeService();
        var startupMetrics = new RecordingStartupMetrics();
        var updateService = new StubUpdateService();
        var viewModel = new MainWindowViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(saver),
            picker,
            new StubCommandLineActivation(),
            settings,
            themeService,
            startupMetrics,
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer()),
            updateService);

        return new TestHarness(loader, saver, picker, startupMetrics, updateService, viewModel);
    }

    private sealed record TestHarness(
        StubDocumentLoader Loader,
        RecordingDocumentSaver DocumentSaver,
        StubFilePicker FilePicker,
        RecordingStartupMetrics StartupMetrics,
        StubUpdateService UpdateService,
        MainWindowViewModel ViewModel);
}
