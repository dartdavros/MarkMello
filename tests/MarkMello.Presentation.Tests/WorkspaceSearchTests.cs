using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Infrastructure.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Поиск по именам: обход с лимитами на реальной файловой системе и состояния
/// сайдбара — пустая выдача, обрезанная выдача, сброс по Esc.
/// </summary>
public sealed class WorkspaceSearchTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "markmello-search-tests",
        Guid.NewGuid().ToString("N"));

    public WorkspaceSearchTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task SearchFindsDocumentsAndFoldersByName()
    {
        CreateFile("release-plan.md");
        CreateFile("notes.md");
        Directory.CreateDirectory(Path.Combine(_root, "plans"));
        CreateFile(Path.Combine("plans", "old-plan.md"));

        var result = await Search("plan");

        Assert.Equal(
            ["old-plan.md", "plans", "release-plan.md"],
            result.Hits.Select(hit => hit.Entry.Name).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.False(result.IsTruncated);
    }

    [Fact]
    public async Task NonDocumentsNeverAppearInResults()
    {
        CreateFile("plan.md");
        CreateFile("plan.bat");
        CreateFile("plan.pdf");

        var result = await Search("plan");

        Assert.Equal(["plan.md"], result.Hits.Select(hit => hit.Entry.Name));
    }

    [Fact]
    public async Task MatchPositionIsReportedForHighlighting()
    {
        CreateFile("release-plan.md");

        var hit = (await Search("plan")).Hits.Single();

        Assert.Equal("release-", hit.NameBeforeMatch);
        Assert.Equal("plan", hit.MatchedName);
        Assert.Equal(".md", hit.NameAfterMatch);
    }

    [Fact]
    public async Task NestedHitsCarryTheirRelativeDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs", "adr"));
        CreateFile(Path.Combine("docs", "adr", "plan.md"));

        var hit = (await Search("plan")).Hits.Single();

        Assert.Equal("docs/adr/", hit.RelativeDirectory);
    }

    [Fact]
    public async Task ResultsAreCappedAndMarkedTruncated()
    {
        for (var index = 0; index < 12; index++)
        {
            CreateFile($"plan-{index}.md");
        }

        var limits = new WorkspaceSearchLimits(MaxMatches: 5, MaxScannedEntries: 1000, MaxDepth: 4);
        var result = await new DirectoryWorkspaceFileSystem().SearchByNameAsync(_root, "plan", limits);

        Assert.Equal(5, result.Hits.Count);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public async Task ScanBudgetStopsTheWalk()
    {
        for (var index = 0; index < 30; index++)
        {
            CreateFile($"note-{index}.md");
        }

        CreateFile("plan.md");

        var limits = new WorkspaceSearchLimits(MaxMatches: 100, MaxScannedEntries: 5, MaxDepth: 4);
        var result = await new DirectoryWorkspaceFileSystem().SearchByNameAsync(_root, "plan", limits);

        Assert.True(result.IsTruncated);
    }

    [Fact]
    public async Task QueryReplacesTheTreeAndClearingBringsItBack()
    {
        var harness = await CreateWorkspaceAsync();
        harness.FileSystem.SearchResult = new WorkspaceSearchResult(
            [
                new WorkspaceSearchHit(
                    WorkspaceEntry.ForFile(@"C:\docs\plan.md", "plan.md"),
                    string.Empty,
                    0,
                    4)
            ],
            IsTruncated: false);

        harness.Workspace.SearchQuery = "plan";
        await WaitForAsync(() => harness.Workspace.SearchHits.Count == 1);

        Assert.False(harness.Workspace.ShowsTree);
        Assert.Equal("1", harness.Workspace.SearchCountLabel);
        Assert.False(harness.Workspace.ShowsSearchEmptyState);

        harness.Workspace.ClearSearchCommand.Execute(null);

        Assert.True(harness.Workspace.ShowsTree);
        Assert.Empty(harness.Workspace.SearchHits);
    }

    [Fact]
    public async Task EmptyResultShowsItsOwnState()
    {
        var harness = await CreateWorkspaceAsync();
        harness.FileSystem.SearchResult = WorkspaceSearchResult.Empty;

        harness.Workspace.SearchQuery = "missing";
        await WaitForAsync(() => harness.Workspace.ShowsSearchEmptyState);

        Assert.True(harness.Workspace.ShowsSearchEmptyState);
        Assert.Equal("0", harness.Workspace.SearchCountLabel);
    }

    [Fact]
    public async Task TruncatedResultIsMarkedWithAPlus()
    {
        var harness = await CreateWorkspaceAsync();
        harness.FileSystem.SearchResult = new WorkspaceSearchResult(
            [
                new WorkspaceSearchHit(
                    WorkspaceEntry.ForFile(@"C:\docs\plan.md", "plan.md"),
                    string.Empty,
                    0,
                    4)
            ],
            IsTruncated: true);

        harness.Workspace.SearchQuery = "plan";
        await WaitForAsync(() => harness.Workspace.IsSearchTruncated);

        Assert.Equal("1+", harness.Workspace.SearchCountLabel);
    }

    [Fact]
    public async Task OpeningAFileHitOpensTheDocument()
    {
        var harness = await CreateWorkspaceAsync();
        var hit = new WorkspaceSearchHit(
            WorkspaceEntry.ForFile(@"C:\docs\first.md", "first.md"),
            string.Empty,
            0,
            5);

        await harness.Workspace.ActivateSearchHitCommand.ExecuteAsync(hit);

        Assert.Equal(@"C:\docs\first.md", harness.ViewModel.CurrentDocumentPath);
    }

    private async Task<WorkspaceSearchResult> Search(string query)
        => await new DirectoryWorkspaceFileSystem()
            .SearchByNameAsync(_root, query, WorkspaceSearchLimits.Default);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Yield();
        }
    }

    private static async Task<SearchHarness> CreateWorkspaceAsync()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            @"C:\docs",
            WorkspaceEntry.ForFile(@"C:\docs\first.md", "first.md"));

        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\first.md"] = new MarkdownSource(@"C:\docs\first.md", "first.md", "# first");

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
            new WorkspaceFileOperationsUseCase(fileSystem, new FakePlatformServices()),
            new FakePlatformServices(),
            static () => new FakeWorkspaceWatcher());

        await viewModel.OpenFolderPathAsync(@"C:\docs");

        return new SearchHarness(fileSystem, viewModel, viewModel.Workspace!);
    }

    private string CreateFile(string relativeName)
    {
        var path = Path.Combine(_root, relativeName);
        File.WriteAllText(path, "# test");
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Мусор в temp не повод валить прогон.
        }
    }

    private sealed record SearchHarness(
        FakeWorkspaceFileSystem FileSystem,
        ShellViewModel ViewModel,
        WorkspaceViewModel Workspace);
}
