using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// M1: открытие папки, ленивое дерево и то, ради чего всё это ограничено —
/// single-file режим не должен инициализировать workspace-подсистему.
/// </summary>
public sealed class WorkspaceSidebarTests
{
    private const string Root = @"C:\docs";

    [Fact]
    public async Task StartingWithASingleFileDoesNotCreateWorkspace()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        var harness = CreateHarness(fileSystem);
        harness.CommandLine.ActivationPath = @"C:\docs\notes.md";
        harness.Loader.Sources[@"C:\docs\notes.md"] = new MarkdownSource(@"C:\docs\notes.md", "notes.md", "# notes");

        await harness.ViewModel.InitializeAsync();

        Assert.Null(harness.ViewModel.Workspace);
        Assert.False(harness.ViewModel.ShowsSidebar);
        Assert.True(harness.ViewModel.ShowsFloatingAppMenuButton);
        Assert.Empty(fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task OpeningFolderBuildsRootLevelOnly()
    {
        var fileSystem = CreateFileSystem();
        var harness = CreateHarness(fileSystem);

        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = Assert.IsType<WorkspaceViewModel>(harness.ViewModel.Workspace);
        Assert.True(harness.ViewModel.ShowsSidebar);
        Assert.False(harness.ViewModel.ShowsFloatingAppMenuButton);
        Assert.Equal("docs", workspace.RootDisplayName);
        Assert.Equal(["adr", "README.md", "pack.bat"], workspace.Roots.Select(node => node.Name));
        Assert.Equal([Root], fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task OpeningFolderOpensRootReadme()
    {
        var harness = CreateHarness(CreateFileSystem());

        await harness.ViewModel.OpenFolderPathAsync(Root);

        Assert.Equal(@"C:\docs\README.md", harness.ViewModel.CurrentDocumentPath);
        Assert.Equal("README.md — docs — MarkMello", harness.ViewModel.WindowTitle);

        var readmeNode = harness.ViewModel.Workspace!.Roots.Single(node => node.Name == "README.md");
        Assert.True(readmeNode.IsActiveDocument);
    }

    [Fact]
    public async Task OpeningFolderWithoutReadmeLeavesDocumentSurfaceAlone()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(@"C:\docs\notes.md", "notes.md"));
        var harness = CreateHarness(fileSystem);

        await harness.ViewModel.OpenFolderPathAsync(Root);

        Assert.True(harness.ViewModel.ShowsSidebar);
        Assert.Null(harness.ViewModel.CurrentDocumentPath);
        Assert.Equal("docs — MarkMello", harness.ViewModel.WindowTitle);
    }

    [Fact]
    public async Task ExpandingDirectoryReadsChildrenOnce()
    {
        var fileSystem = CreateFileSystem();
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var adr = harness.ViewModel.Workspace!.Roots.Single(node => node.Name == "adr");
        Assert.Single(adr.Children); // placeholder, каталог ещё не читался
        Assert.False(adr.HasLoadedChildren);

        adr.IsExpanded = true;
        await WaitForChildrenAsync(adr);

        Assert.Equal(["adr_0001.md"], adr.Children.Select(node => node.Name));
        Assert.Equal([Root, @"C:\docs\adr"], fileSystem.EnumeratedPaths);

        adr.IsExpanded = false;
        adr.IsExpanded = true;

        Assert.Equal([Root, @"C:\docs\adr"], fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task DirectoryFailureStaysInsideItsNode()
    {
        var fileSystem = CreateFileSystem();
        fileSystem.FailWith(@"C:\docs\adr", new UnauthorizedAccessException());
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var adr = harness.ViewModel.Workspace!.Roots.Single(node => node.Name == "adr");
        adr.IsExpanded = true;
        await WaitForLoadErrorAsync(adr);

        Assert.True(adr.HasLoadError);
        Assert.Equal("Access denied", adr.LoadError);
        Assert.True(harness.ViewModel.ShowsSidebar);
        Assert.Equal(ViewState.Viewing, harness.ViewModel.State);
    }

    [Fact]
    public async Task SelectingSupportedDocumentOpensIt()
    {
        var harness = CreateHarness(CreateFileSystem());
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        workspace.SelectedNode = workspace.Roots.Single(node => node.Name == "adr");
        workspace.SelectedNode = workspace.Roots.Single(node => node.Name == "README.md");

        Assert.Equal(@"C:\docs\README.md", harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task SelectingNonDocumentIsInert()
    {
        var harness = CreateHarness(CreateFileSystem());
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        var packBat = workspace.Roots.Single(node => node.Name == "pack.bat");

        Assert.True(packBat.IsInert);

        workspace.SelectedNode = packBat;

        Assert.Equal(@"C:\docs\README.md", harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(ViewState.Viewing, harness.ViewModel.State);
    }

    [Fact]
    public async Task MissingFolderFailsWithoutBreakingTheOpenDocument()
    {
        var harness = CreateHarness(new FakeWorkspaceFileSystem());
        harness.Loader.Sources[@"C:\notes.md"] = new MarkdownSource(@"C:\notes.md", "notes.md", "# notes");
        await harness.ViewModel.OpenPathAsync(@"C:\notes.md");

        await harness.ViewModel.OpenFolderPathAsync(@"C:\missing");

        Assert.Null(harness.ViewModel.Workspace);
        Assert.Equal(ViewState.LoadError, harness.ViewModel.State);
        Assert.Equal(@"C:\notes.md", harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task ClosingFolderClosesDocumentThatBelongsToIt()
    {
        var harness = CreateHarness(CreateFileSystem());
        await harness.ViewModel.OpenFolderPathAsync(Root);

        await harness.ViewModel.CloseFolderCommand.ExecuteAsync(null);

        Assert.Null(harness.ViewModel.Workspace);
        Assert.False(harness.ViewModel.ShowsSidebar);
        Assert.Null(harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(ViewState.NoDocument, harness.ViewModel.State);
    }

    [Fact]
    public async Task ClosingFolderKeepsDocumentOpenedOverIt()
    {
        var harness = CreateHarness(CreateFileSystem());
        await harness.ViewModel.OpenFolderPathAsync(Root);
        harness.Loader.Sources[@"C:\outside\notes.md"] =
            new MarkdownSource(@"C:\outside\notes.md", "notes.md", "# notes");
        await harness.ViewModel.OpenPathAsync(@"C:\outside\notes.md");

        await harness.ViewModel.CloseFolderCommand.ExecuteAsync(null);

        Assert.Null(harness.ViewModel.Workspace);
        Assert.Equal(@"C:\outside\notes.md", harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(ViewState.Viewing, harness.ViewModel.State);
    }

    [Fact]
    public async Task SidebarWidthIsRestoredAndPersistedClamped()
    {
        var harness = CreateHarness(CreateFileSystem());
        harness.Settings.SidebarWidth = 300;

        await harness.ViewModel.OpenFolderPathAsync(Root);
        Assert.Equal(300, harness.ViewModel.SidebarWidth);

        harness.ViewModel.SidebarWidth = 900;
        await harness.ViewModel.PersistSidebarWidthAsync();

        Assert.Equal(WorkspaceSidebarWidth.Maximum, harness.ViewModel.SidebarWidth);
        Assert.Equal(WorkspaceSidebarWidth.Maximum, harness.Settings.SidebarWidth);
    }

    private static FakeWorkspaceFileSystem CreateFileSystem()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForDirectory(@"C:\docs\adr", "adr"),
            WorkspaceEntry.ForFile(@"C:\docs\README.md", "README.md"),
            WorkspaceEntry.ForFile(@"C:\docs\pack.bat", "pack.bat"));
        fileSystem.AddDirectory(
            @"C:\docs\adr",
            WorkspaceEntry.ForFile(@"C:\docs\adr\adr_0001.md", "adr_0001.md"));
        return fileSystem;
    }

    private static async Task WaitForChildrenAsync(FileTreeNodeViewModel node)
    {
        for (var attempt = 0; attempt < 50 && !node.HasLoadedChildren; attempt++)
        {
            await Task.Yield();
        }
    }

    private static async Task WaitForLoadErrorAsync(FileTreeNodeViewModel node)
    {
        for (var attempt = 0; attempt < 50 && !node.HasLoadError; attempt++)
        {
            await Task.Yield();
        }
    }

    private static WorkspaceTestHarness CreateHarness(FakeWorkspaceFileSystem fileSystem)
    {
        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\README.md"] =
            new MarkdownSource(@"C:\docs\README.md", "README.md", "# readme");
        var settings = new InMemorySettingsStore();
        var commandLine = new StubCommandLineActivation();
        var viewModel = new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            new StubFilePicker(),
            commandLine,
            new LocalizationService(AppLanguage.English),
            settings,
            new RecordingThemeService(),
            new RecordingStartupMetrics(),
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            new StubUpdateService(),
            new OpenFolderUseCase(fileSystem),
            new ExpandFolderNodeUseCase(fileSystem),
            new SearchWorkspaceFilesUseCase(fileSystem),
            new WorkspaceFileOperationsUseCase(fileSystem, new FakePlatformServices()),
            new FakePlatformServices());

        return new WorkspaceTestHarness(loader, settings, commandLine, viewModel);
    }

    private sealed record WorkspaceTestHarness(
        StubDocumentLoader Loader,
        InMemorySettingsStore Settings,
        StubCommandLineActivation CommandLine,
        ShellViewModel ViewModel);
}
