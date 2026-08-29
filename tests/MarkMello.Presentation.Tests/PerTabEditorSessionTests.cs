using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Editor-сессия принадлежит вкладке: уход на соседнюю не должен терять несохранённый
/// текст, а возврат — показывать его же и в том же режиме.
/// </summary>
public sealed class PerTabEditorSessionTests
{
    [Fact]
    public async Task EditingSurvivesSwitchingToAnotherTabAndBack()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");

        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# first edited";

        var first = harness.ViewModel.OpenDocuments.Tabs[0];
        Assert.True(first.IsDirty);

        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        Assert.False(harness.ViewModel.IsEditMode);
        Assert.True(first.IsDirty);

        await harness.ViewModel.OpenDocuments.ActivateCommand.ExecuteAsync(first);

        Assert.True(harness.ViewModel.IsEditMode);
        Assert.Equal("# first edited", harness.ViewModel.EditorSession!.SourceText);
        Assert.True(harness.ViewModel.IsDirty);
    }

    [Fact]
    public async Task SwitchingAwayFromDirtyTabDoesNotAskAnything()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# edited";

        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Equal(@"C:\docs\second.md", harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task ClosingDirtyBackgroundTabShowsItAndAsks()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# edited";
        var first = harness.ViewModel.OpenDocuments.Tabs[0];

        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");
        await harness.ViewModel.OpenDocuments.CloseCommand.ExecuteAsync(first);

        // Пользователю показали именно ту вкладку, о правках которой спрашивают.
        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Same(first, harness.ViewModel.OpenDocuments.ActiveTab);
        Assert.Equal(2, harness.ViewModel.OpenDocuments.Tabs.Count);
    }

    [Fact]
    public async Task DiscardingChangesClosesTheTab()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# edited";
        var first = harness.ViewModel.OpenDocuments.Tabs[0];

        await harness.ViewModel.OpenDocuments.CloseCommand.ExecuteAsync(first);
        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.Empty(harness.ViewModel.OpenDocuments.Tabs);
        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
    }

    [Fact]
    public async Task ClosingWindowAsksAboutADirtyBackgroundTab()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        harness.ViewModel.ToggleEditModeCommand.Execute(null);
        harness.ViewModel.EditorSession!.SourceText = "# edited";

        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        var queued = harness.ViewModel.TryQueueCloseRequest();

        Assert.True(queued);
        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
    }

    [Fact]
    public async Task CleanTabsDoNotBlockClosingTheWindow()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        await harness.ViewModel.OpenPathAsync(@"C:\docs\second.md");

        Assert.False(harness.ViewModel.TryQueueCloseRequest());
    }

    private static EditorTestHarness CreateHarness()
    {
        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\first.md"] = new MarkdownSource(@"C:\docs\first.md", "first.md", "# first");
        loader.Sources[@"C:\docs\second.md"] = new MarkdownSource(@"C:\docs\second.md", "second.md", "# second");

        var fileSystem = new FakeWorkspaceFileSystem();

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
            new FakePlatformServices());

        return new EditorTestHarness(loader, viewModel);
    }

    private sealed record EditorTestHarness(StubDocumentLoader Loader, ShellViewModel ViewModel);
}
