using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Реакция открытых вкладок на изменения, сделанные мимо приложения.
/// Таблица поведения — из раздела «Внешние изменения файла» дизайн-бандла.
/// </summary>
public sealed class ExternalChangesTests
{
    private const string Root = @"C:\docs";
    private const string FirstPath = @"C:\docs\first.md";
    private const string SecondPath = @"C:\docs\second.md";

    [Fact]
    public async Task CleanActiveTabIsReloadedSilently()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);
        harness.ViewModel.ReportScrollOffset(300);

        harness.Loader.Sources[FirstPath] = new MarkdownSource(FirstPath, "first.md", "# first changed");

        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Changed, FirstPath)]);

        Assert.Equal("# first changed", harness.ViewModel.Document!.Content);
        Assert.False(harness.ViewModel.ShowsExternalChangeBanner);

        // Позиция чтения не должна прыгать в начало из-за чужого сохранения.
        Assert.Equal(300, harness.ViewModel.TakePendingScrollOffset());
    }

    [Fact]
    public async Task DirtyTabAsksInsteadOfReloading()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# my edits";

        harness.Loader.Sources[FirstPath] = new MarkdownSource(FirstPath, "first.md", "# changed outside");

        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Changed, FirstPath)]);

        Assert.True(harness.ViewModel.ShowsExternalChangeBanner);
        Assert.Equal("# my edits", harness.ViewModel.EditorSession!.SourceText);
    }

    [Fact]
    public async Task KeepingEditsDismissesTheBannerAndLeavesTheText()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# my edits";
        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Changed, FirstPath)]);

        harness.ViewModel.KeepExternalChangeCommand.Execute(null);

        Assert.False(harness.ViewModel.ShowsExternalChangeBanner);
        Assert.Equal("# my edits", harness.ViewModel.EditorSession!.SourceText);
    }

    [Fact]
    public async Task ReloadingDropsTheEditsAndTakesTheDiskVersion()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# my edits";

        harness.Loader.Sources[FirstPath] = new MarkdownSource(FirstPath, "first.md", "# changed outside");
        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Changed, FirstPath)]);

        await harness.ViewModel.ReloadExternalChangeCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.ShowsExternalChangeBanner);
        Assert.Equal("# changed outside", harness.ViewModel.Document!.Content);
        Assert.False(harness.ViewModel.IsDirty);
    }

    [Fact]
    public async Task BackgroundTabIsRereadOnlyWhenItComesBack()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);
        await harness.ViewModel.OpenPathAsync(SecondPath);

        harness.Loader.Sources[FirstPath] = new MarkdownSource(FirstPath, "first.md", "# first changed");
        var readsBefore = harness.Loader.LoadCount;

        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Changed, FirstPath)]);

        // Фоновую вкладку не перечитываем: на неё никто не смотрит.
        Assert.Equal(readsBefore, harness.Loader.LoadCount);

        var first = harness.ViewModel.OpenDocuments.Tabs[0];
        await harness.ViewModel.OpenDocuments.ActivateCommand.ExecuteAsync(first);

        Assert.Equal("# first changed", harness.ViewModel.Document!.Content);
    }

    [Fact]
    public async Task ExternallyDeletedCleanTabCloses()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);

        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Deleted, FirstPath)]);

        Assert.Empty(harness.ViewModel.OpenDocuments.Tabs);
    }

    [Fact]
    public async Task ExternallyDeletedDirtyTabSurvivesWithAMark()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# unsaved work";

        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Deleted, FirstPath)]);

        var tab = Assert.Single(harness.ViewModel.OpenDocuments.Tabs);
        Assert.Equal("first.md (deleted)", tab.DisplayTitle);
        Assert.Equal("# unsaved work", harness.ViewModel.EditorSession!.SourceText);
    }

    [Fact]
    public async Task ExternalRenameMovesTheTabToTheNewPath()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(FirstPath);

        await harness.ViewModel.ApplyWorkspaceChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Renamed, @"C:\docs\moved.md", FirstPath)]);

        var tab = Assert.Single(harness.ViewModel.OpenDocuments.Tabs);
        Assert.Equal(@"C:\docs\moved.md", tab.Path);
        Assert.Equal("moved.md", tab.Title);
    }

    [Fact]
    public async Task WatcherStartsWithTheFolderAndStopsWithIt()
    {
        var harness = await CreateAsync();

        Assert.Equal([Root], harness.Watcher.StartedRoots);

        await harness.ViewModel.CloseFolderCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Watcher.StopCount);
    }

    [Fact]
    public async Task ChangesInOneDirectoryRefreshItOnlyOnce()
    {
        var harness = await CreateAsync();
        harness.FileSystem.EnumeratedPaths.Clear();

        await harness.ViewModel.ApplyWorkspaceChangesAsync(
        [
            new WorkspaceChange(WorkspaceChangeKind.Created, @"C:\docs\a.md"),
            new WorkspaceChange(WorkspaceChangeKind.Created, @"C:\docs\b.md"),
            new WorkspaceChange(WorkspaceChangeKind.Changed, @"C:\docs\c.md")
        ]);

        Assert.Equal([Root], harness.FileSystem.EnumeratedPaths);
    }

    private static async Task<WatcherHarness> CreateAsync()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForFile(FirstPath, "first.md"),
            WorkspaceEntry.ForFile(SecondPath, "second.md"));

        var loader = new CountingDocumentLoader();
        loader.Sources[FirstPath] = new MarkdownSource(FirstPath, "first.md", "# first");
        loader.Sources[SecondPath] = new MarkdownSource(SecondPath, "second.md", "# second");

        var watcher = new FakeWorkspaceWatcher();
        var platform = new FakePlatformServices(fileSystem);

        var viewModel = new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            new StubFilePicker(),
            new StubCommandLineActivation(),
            new LocalizationService(AppLanguage.English),
            new InMemorySettingsStore(),
            new RecordingThemeService(),
            new RecordingStartupMetrics(),
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            new StubUpdateService(),
            new OpenFolderUseCase(fileSystem),
            new ExpandFolderNodeUseCase(fileSystem),
            new SearchWorkspaceFilesUseCase(fileSystem),
            new WorkspaceFileOperationsUseCase(fileSystem, platform),
            platform,
            () => watcher,
            new RecordingWindowLauncher());

        await viewModel.OpenFolderPathAsync(Root);

        return new WatcherHarness(fileSystem, loader, watcher, viewModel);
    }

    private sealed record WatcherHarness(
        FakeWorkspaceFileSystem FileSystem,
        CountingDocumentLoader Loader,
        FakeWorkspaceWatcher Watcher,
        ShellViewModel ViewModel);
}

/// <summary>Watcher без файловой системы: события подаются тестом напрямую.</summary>
internal sealed class FakeWorkspaceWatcher : IWorkspaceWatcher
{
    public List<string> StartedRoots { get; } = [];

    public int StopCount { get; private set; }

    public event EventHandler<IReadOnlyList<WorkspaceChange>>? Changed;

    public void Start(string rootPath) => StartedRoots.Add(rootPath);

    public void StopWatching() => StopCount++;

    public void Raise(params WorkspaceChange[] changes) => Changed?.Invoke(this, changes);

    public void Dispose() => StopWatching();
}
