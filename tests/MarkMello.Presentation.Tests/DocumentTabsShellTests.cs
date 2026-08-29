using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// M2: вкладки на уровне окна — открытие, переключение без перечитывания файла,
/// закрытие и пустое состояние при открытой папке.
/// </summary>
public sealed class DocumentTabsShellTests
{
    private const string Root = @"C:\docs";

    [Fact]
    public async Task OpeningTwoDocumentsKeepsBothAsTabs()
    {
        var harness = CreateHarness();

        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        Assert.Equal(["first.md", "second.md"], harness.ViewModel.OpenDocuments.Tabs.Select(tab => tab.Title));
        Assert.Equal(@"C:\docs\second.md", harness.ViewModel.OpenDocuments.ActiveTab!.Path);
        Assert.True(harness.ViewModel.ShowsTabStrip);
    }

    /// <summary>
    /// Подпись «ещё N» собирает shell, а число вкладок в переполнении считает
    /// OpenDocuments: без уведомления кнопка появлялась с текстом «0 more».
    /// </summary>
    [Fact]
    public async Task OverflowLabelIsNotifiedWhenTabsStopFitting()
    {
        var harness = CreateHarness();
        harness.ViewModel.OpenDocuments.AvailableWidth = 1000;

        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        var notified = 0;
        harness.ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.TabsOverflowLabel))
            {
                notified++;
            }
        };

        harness.ViewModel.OpenDocuments.AvailableWidth = 120;

        Assert.True(harness.ViewModel.OpenDocuments.HasOverflow);
        Assert.True(notified > 0);
        Assert.Equal(
            $"{harness.ViewModel.OpenDocuments.OverflowTabs.Count} more",
            harness.ViewModel.TabsOverflowLabel);
    }

    [Fact]
    public async Task ReopeningTheSameFileReusesItsTab()
    {
        var harness = CreateHarness();

        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");

        Assert.Equal(2, harness.ViewModel.OpenDocuments.Tabs.Count);
        Assert.Equal(@"C:\docs\first.md", harness.ViewModel.OpenDocuments.ActiveTab!.Path);
    }

    [Fact]
    public async Task SwitchingTabsRestoresContentWithoutReadingTheFileAgain()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        var readsBefore = harness.Loader.LoadCount;
        var first = harness.ViewModel.OpenDocuments.Tabs[0];

        await harness.ViewModel.OpenDocuments.ActivateCommand.ExecuteAsync(first);

        Assert.Equal(readsBefore, harness.Loader.LoadCount);
        Assert.Equal(@"C:\docs\first.md", harness.ViewModel.CurrentDocumentPath);
        Assert.Equal("first.md — MarkMello", harness.ViewModel.WindowTitle);
    }

    [Fact]
    public async Task ScrollOffsetIsRememberedPerTab()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        harness.ViewModel.ReportScrollOffset(420);

        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");
        harness.ViewModel.ReportScrollOffset(80);

        var first = harness.ViewModel.OpenDocuments.Tabs[0];
        await harness.ViewModel.OpenDocuments.ActivateCommand.ExecuteAsync(first);

        Assert.Equal(420, harness.ViewModel.TakePendingScrollOffset());
        Assert.Null(harness.ViewModel.TakePendingScrollOffset());
    }

    [Fact]
    public async Task ClosingTabActivatesNeighbourAndRestoresIt()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);

        Assert.Single(harness.ViewModel.OpenDocuments.Tabs);
        Assert.Equal(@"C:\docs\first.md", harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(ViewState.Viewing, harness.ViewModel.State);
    }

    [Fact]
    public async Task ClosingTheLastTabWithoutFolderReturnsToWelcome()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");

        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);

        Assert.Empty(harness.ViewModel.OpenDocuments.Tabs);
        Assert.False(harness.ViewModel.ShowsTabStrip);
        Assert.True(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.IsEmptyDocumentSurface);
    }

    [Fact]
    public async Task ClosingTheLastTabInsideFolderShowsEmptySurface()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenFolderPathAsync(Root);
        Assert.Single(harness.ViewModel.OpenDocuments.Tabs);

        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);

        Assert.Empty(harness.ViewModel.OpenDocuments.Tabs);
        Assert.True(harness.ViewModel.IsEmptyDocumentSurface);
        Assert.False(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.ShowsTabStrip);
    }

    [Fact]
    public async Task TabsKnowWhetherTheyBelongToTheOpenFolder()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenFolderPathAsync(Root);
        await harness.ViewModel.OpenPathAsync(@"C:\outside\notes.md");

        var readme = harness.ViewModel.OpenDocuments.Tabs.Single(tab => tab.Title == "README.md");
        var outside = harness.ViewModel.OpenDocuments.Tabs.Single(tab => tab.Title == "notes.md");

        Assert.True(readme.BelongsToWorkspace);
        Assert.False(outside.BelongsToWorkspace);
        Assert.Equal("README.md", readme.Tooltip);
        Assert.Equal(@"C:\outside\notes.md", outside.Tooltip);
    }

    [Fact]
    public async Task CtrlTabWalksTabsInStripOrder()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        await harness.ViewModel.ActivateNextTabCommand.ExecuteAsync(null);
        Assert.Equal(@"C:\docs\first.md", harness.ViewModel.CurrentDocumentPath);

        await harness.ViewModel.ActivatePreviousTabCommand.ExecuteAsync(null);
        Assert.Equal(@"C:\docs\second.md", harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task ClosingFolderDropsItsTabsAndKeepsOutsideOnes()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenFolderPathAsync(Root);
        await harness.ViewModel.OpenPathAsync(@"C:\outside\notes.md");

        await harness.ViewModel.CloseFolderCommand.ExecuteAsync(null);

        Assert.Null(harness.ViewModel.Workspace);
        Assert.Equal(@"C:\outside\notes.md", harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(["notes.md"], harness.ViewModel.OpenDocuments.Tabs.Select(tab => tab.Title));
    }

    private static TabsTestHarness CreateHarness()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForFile(@"C:\docs\README.md", "README.md"),
            WorkspaceEntry.ForFile(@"C:\docs\first.md", "first.md"));

        var loader = new CountingDocumentLoader();
        loader.Sources[@"C:\docs\README.md"] = new MarkdownSource(@"C:\docs\README.md", "README.md", "# readme");
        loader.Sources[@"C:\docs\first.md"] = new MarkdownSource(@"C:\docs\first.md", "first.md", "# first");
        loader.Sources[@"C:\docs\second.md"] = new MarkdownSource(@"C:\docs\second.md", "second.md", "# second");
        loader.Sources[@"C:\outside\notes.md"] = new MarkdownSource(@"C:\outside\notes.md", "notes.md", "# notes");

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
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher());

        return new TabsTestHarness(loader, viewModel);
    }

    private sealed record TabsTestHarness(CountingDocumentLoader Loader, ShellViewModel ViewModel);
}
