using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// M4: вторая папка уходит в отдельное окно, состояние сессии сохраняется и
/// возвращается при повторном открытии той же папки — но не при холодном старте.
/// </summary>
public sealed class SecondWindowAndSessionTests
{
    private const string FirstRoot = @"C:\docs";
    private const string SecondRoot = @"C:\notes";

    [Fact]
    public async Task SecondFolderOpensInANewWindow()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);

        await harness.ViewModel.OpenFolderPathAsync(SecondRoot);

        // Дерево текущего окна не подменяется: у второй папки своё окно.
        Assert.Equal(FirstRoot, harness.ViewModel.Workspace!.Folder.RootPath);
        Assert.Equal([SecondRoot], harness.Launcher.NewWindowFolders);
    }

    [Fact]
    public async Task AlreadyOpenFolderJustGetsFocus()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);
        harness.Launcher.OpenFolders.Add(SecondRoot);

        await harness.ViewModel.OpenFolderPathAsync(SecondRoot);

        Assert.Equal([SecondRoot], harness.Launcher.FocusedFolders);
        Assert.Empty(harness.Launcher.NewWindowFolders);
    }

    [Fact]
    public async Task ReopeningTheSameFolderStaysInTheSameWindow()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);

        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);

        Assert.Empty(harness.Launcher.NewWindowFolders);
        Assert.Empty(harness.Launcher.FocusedFolders);
        Assert.NotNull(harness.ViewModel.Workspace);
    }

    [Fact]
    public async Task OpenTabsAreStoredInTheSession()
    {
        var harness = CreateHarness();
        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);
        await harness.ViewModel.OpenPathAsync(@"C:\docs\first.md");

        // Запись отложена и уходит с UI-потока, поэтому ждём её, а не читаем сразу.
        var session = await WaitForSessionAsync(harness.Settings, state => state.FolderPath is not null);

        Assert.Equal(FirstRoot, session.FolderPath);
        Assert.Contains(@"C:\docs\first.md", session.OpenDocumentPaths);
        Assert.Equal(@"C:\docs\first.md", session.ActiveDocumentPath);
    }

    [Fact]
    public async Task ReopeningAFolderRestoresItsTabs()
    {
        var harness = CreateHarness();
        harness.Settings.Session = new WorkspaceSessionState(
            FirstRoot,
            [@"C:\docs\first.md", @"C:\docs\second.md"],
            @"C:\docs\second.md",
            []);

        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);

        Assert.Equal(
            ["first.md", "second.md"],
            harness.ViewModel.OpenDocuments.Tabs.Select(tab => tab.Title));
        Assert.Equal(@"C:\docs\second.md", harness.ViewModel.OpenDocuments.ActiveTab!.Path);
    }

    [Fact]
    public async Task SessionOfAnotherFolderIsIgnored()
    {
        var harness = CreateHarness();
        harness.Settings.Session = new WorkspaceSessionState(
            SecondRoot,
            [@"C:\notes\other.md"],
            @"C:\notes\other.md",
            []);

        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);

        // Чужая сессия не должна тащить в папку посторонние вкладки.
        Assert.DoesNotContain(
            harness.ViewModel.OpenDocuments.Tabs,
            tab => tab.Path == @"C:\notes\other.md");
    }

    [Fact]
    public async Task VanishedPathsAreDroppedFromTheRestoredSession()
    {
        var harness = CreateHarness();
        harness.Settings.Session = new WorkspaceSessionState(
            FirstRoot,
            [@"C:\docs\first.md", @"C:\docs\gone.md"],
            @"C:\docs\gone.md",
            []);

        await harness.ViewModel.OpenFolderPathAsync(FirstRoot);

        Assert.Equal(["first.md"], harness.ViewModel.OpenDocuments.Tabs.Select(tab => tab.Title));
    }

    [Fact]
    public async Task ColdStartWithoutArgumentsDoesNotOpenTheStoredFolder()
    {
        var harness = CreateHarness();
        harness.Settings.Session = new WorkspaceSessionState(FirstRoot, [@"C:\docs\first.md"], null, []);

        await harness.ViewModel.InitializeAsync();

        Assert.Null(harness.ViewModel.Workspace);
        Assert.Empty(harness.ViewModel.OpenDocuments.Tabs);
    }

    [Fact]
    public async Task DirectoryArgumentOpensTheFolder()
    {
        var harness = CreateHarness();
        harness.CommandLine.ActivationFolderPath = FirstRoot;

        await harness.ViewModel.InitializeAsync();

        Assert.NotNull(harness.ViewModel.Workspace);
        Assert.Equal(FirstRoot, harness.ViewModel.Workspace!.Folder.RootPath);
    }

    private static async Task<WorkspaceSessionState> WaitForSessionAsync(
        InMemorySettingsStore settings,
        Func<WorkspaceSessionState, bool> predicate)
    {
        for (var attempt = 0; attempt < 60 && !predicate(settings.Session); attempt++)
        {
            await Task.Delay(25);
        }

        return settings.Session;
    }

    private static SessionHarness CreateHarness()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            FirstRoot,
            WorkspaceEntry.ForFile(@"C:\docs\first.md", "first.md"),
            WorkspaceEntry.ForFile(@"C:\docs\second.md", "second.md"));
        fileSystem.AddDirectory(SecondRoot, WorkspaceEntry.ForFile(@"C:\notes\other.md", "other.md"));

        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\first.md"] = new MarkdownSource(@"C:\docs\first.md", "first.md", "# first");
        loader.Sources[@"C:\docs\second.md"] = new MarkdownSource(@"C:\docs\second.md", "second.md", "# second");
        loader.Sources[@"C:\notes\other.md"] = new MarkdownSource(@"C:\notes\other.md", "other.md", "# other");

        var settings = new InMemorySettingsStore();
        var launcher = new RecordingWindowLauncher();
        var commandLine = new StubCommandLineActivation();
        var platform = new FakePlatformServices(fileSystem);

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
            new WorkspaceFileOperationsUseCase(fileSystem, platform),
            platform,
            static () => new FakeWorkspaceWatcher(),
            launcher,
            fileExists: path => fileSystem.Exists(path));

        return new SessionHarness(settings, launcher, commandLine, viewModel);
    }

    private sealed record SessionHarness(
        InMemorySettingsStore Settings,
        RecordingWindowLauncher Launcher,
        StubCommandLineActivation CommandLine,
        ShellViewModel ViewModel);
}
