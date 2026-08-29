using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Файловые операции дерева: создание и переименование инлайн, дублирование,
/// удаление через корзину и реакция открытых вкладок.
/// </summary>
public sealed class WorkspaceFileOperationsTests
{
    private const string Root = @"C:\docs";

    [Fact]
    public async Task CreatingAFileOpensItAsATab()
    {
        var harness = await CreateAsync();

        harness.Workspace.StartNewFileCommand.Execute(null);
        harness.Workspace.EditName = "meeting";
        await harness.Workspace.CommitEditCommand.ExecuteAsync(null);

        Assert.Contains(harness.Workspace.Roots, node => node.Name == "meeting.md");
        Assert.Equal(@"C:\docs\meeting.md", harness.ViewModel.CurrentDocumentPath);
        Assert.False(harness.Workspace.IsEditingName);
    }

    [Fact]
    public async Task CreatingAFolderDoesNotOpenAnything()
    {
        var harness = await CreateAsync();

        harness.Workspace.StartNewFolderCommand.Execute(null);
        harness.Workspace.EditName = "drafts";
        await harness.Workspace.CommitEditCommand.ExecuteAsync(null);

        Assert.Contains(harness.Workspace.Roots, node => node is { Name: "drafts", IsDirectory: true });
        Assert.Null(harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task TakenNameKeepsTheInputOpenWithAnError()
    {
        var harness = await CreateAsync();

        harness.Workspace.StartNewFileCommand.Execute(null);
        harness.Workspace.EditName = "first.md";
        await harness.Workspace.CommitEditCommand.ExecuteAsync(null);

        Assert.True(harness.Workspace.IsEditingName);
        Assert.True(harness.Workspace.HasEditError);
        Assert.Equal("A file with this name already exists", harness.Workspace.EditError);
    }

    [Fact]
    public async Task InvalidCharactersAreReportedUnderTheInput()
    {
        var harness = await CreateAsync();

        harness.Workspace.StartNewFileCommand.Execute(null);
        harness.Workspace.EditName = "a/b.md";
        await harness.Workspace.CommitEditCommand.ExecuteAsync(null);

        Assert.True(harness.Workspace.IsEditingName);
        Assert.Contains("aren't allowed", harness.Workspace.EditError);
    }

    [Fact]
    public async Task EscapeCancelsTheInput()
    {
        var harness = await CreateAsync();

        harness.Workspace.StartNewFileCommand.Execute(null);
        harness.Workspace.EditName = "draft";
        harness.Workspace.CancelEditCommand.Execute(null);

        Assert.False(harness.Workspace.IsEditingName);
        Assert.DoesNotContain(harness.Workspace.Roots, node => node.Name == "draft.md");
    }

    [Fact]
    public async Task RenamingFollowsTheOpenTab()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");

        var node = harness.Workspace.Roots.Single(candidate => candidate.Name == "first.md");
        harness.Workspace.StartRenameCommand.Execute(node);
        harness.Workspace.EditName = "renamed.md";
        await harness.Workspace.CommitEditCommand.ExecuteAsync(null);

        var tab = Assert.Single(harness.ViewModel.OpenDocuments.Tabs);
        Assert.Equal("renamed.md", tab.Title);
        Assert.Equal(@"C:\docs\renamed.md", tab.Path);
        Assert.Equal(@"C:\docs\renamed.md", harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task DuplicatingPicksAFreeNameAndDoesNotOpenIt()
    {
        var harness = await CreateAsync();
        var node = harness.Workspace.Roots.Single(candidate => candidate.Name == "first.md");

        await harness.Workspace.DuplicateCommand.ExecuteAsync(node);

        Assert.Contains(harness.Workspace.Roots, candidate => candidate.Name == "first copy.md");
        Assert.Null(harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task DeletingAsksFirstAndThenClosesTheTab()
    {
        var harness = await CreateAsync();
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");
        var node = harness.Workspace.Roots.Single(candidate => candidate.Name == "first.md");

        await harness.Workspace.RequestDeleteCommand.ExecuteAsync(node);

        Assert.True(harness.ViewModel.IsDeletePromptOpen);
        Assert.Equal("Delete \"first.md\"?", harness.ViewModel.DeletePromptTitle);
        Assert.NotEmpty(harness.ViewModel.OpenDocuments.Tabs);

        await harness.ViewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsDeletePromptOpen);
        Assert.Equal([@"C:\docs\first.md"], harness.Platform.TrashedPaths);
        Assert.Empty(harness.ViewModel.OpenDocuments.Tabs);
        Assert.DoesNotContain(harness.Workspace.Roots, candidate => candidate.Name == "first.md");
    }

    [Fact]
    public async Task CancellingDeleteChangesNothing()
    {
        var harness = await CreateAsync();
        var node = harness.Workspace.Roots.Single(candidate => candidate.Name == "first.md");

        await harness.Workspace.RequestDeleteCommand.ExecuteAsync(node);
        harness.ViewModel.CancelDeleteCommand.Execute(null);

        Assert.False(harness.ViewModel.IsDeletePromptOpen);
        Assert.Empty(harness.Platform.TrashedPaths);
        Assert.Contains(harness.Workspace.Roots, candidate => candidate.Name == "first.md");
    }

    [Fact]
    public async Task NonEmptyFolderMentionsHowManyItemsGo()
    {
        var harness = await CreateAsync();
        var node = harness.Workspace.Roots.Single(candidate => candidate.Name == "adr");

        await harness.Workspace.RequestDeleteCommand.ExecuteAsync(node);

        Assert.Equal("Delete folder \"adr\" and everything in it?", harness.ViewModel.DeletePromptTitle);
        Assert.Contains("has 1 items", harness.ViewModel.DeletePromptMessage);
    }

    [Fact]
    public async Task WithoutTrashTheDialogAsksAgainAboutPermanentDeletion()
    {
        var harness = await CreateAsync();
        harness.Platform.TrashResult = TrashResult.Unsupported;
        var node = harness.Workspace.Roots.Single(candidate => candidate.Name == "first.md");

        await harness.Workspace.RequestDeleteCommand.ExecuteAsync(node);
        await harness.ViewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        // Ничего не удалено: диалог остался и сменил текст на безвозвратное удаление.
        Assert.True(harness.ViewModel.IsDeletePromptOpen);
        Assert.True(harness.ViewModel.IsPermanentDeletePrompt);
        Assert.Contains("permanently", harness.ViewModel.DeletePromptMessage);
        Assert.Contains(harness.Workspace.Roots, candidate => candidate.Name == "first.md");

        await harness.ViewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\docs\first.md"], harness.FileSystem.DeletedPaths);
        Assert.DoesNotContain(harness.Workspace.Roots, candidate => candidate.Name == "first.md");
    }

    [Fact]
    public async Task RevealAsksThePlatformForTheRealPath()
    {
        var harness = await CreateAsync();
        var node = harness.Workspace.Roots.Single(candidate => candidate.Name == "first.md");

        await harness.Workspace.RevealCommand.ExecuteAsync(node);

        Assert.Equal([@"C:\docs\first.md"], harness.Platform.RevealedPaths);
    }

    private static async Task<OperationsHarness> CreateAsync()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForDirectory(@"C:\docs\adr", "adr"),
            WorkspaceEntry.ForFile(@"C:\docs\first.md", "first.md"));
        fileSystem.AddDirectory(
            @"C:\docs\adr",
            WorkspaceEntry.ForFile(@"C:\docs\adr\adr_0001.md", "adr_0001.md"));

        var platform = new FakePlatformServices(fileSystem);

        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\first.md"] = new MarkdownSource(@"C:\docs\first.md", "first.md", "# first");
        loader.Sources[@"C:\docs\meeting.md"] = new MarkdownSource(@"C:\docs\meeting.md", "meeting.md", string.Empty);
        loader.Sources[@"C:\docs\renamed.md"] = new MarkdownSource(@"C:\docs\renamed.md", "renamed.md", "# first");

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
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher());

        await viewModel.OpenFolderPathAsync(Root);

        return new OperationsHarness(fileSystem, platform, viewModel, viewModel.Workspace!);
    }

    private sealed record OperationsHarness(
        FakeWorkspaceFileSystem FileSystem,
        FakePlatformServices Platform,
        ShellViewModel ViewModel,
        WorkspaceViewModel Workspace);
}
