using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Infrastructure.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Внешние изменения файлов, открытых без папки (#47): двойным кликом, через «Открыть».
/// Реакция вкладок та же, что в папке, плюс атомарное сохранение редакторов и агентов.
/// </summary>
public sealed class StandaloneDocumentWatchTests
{
    private const string NotePath = @"C:\notes\note.md";
    private const string OtherPath = @"C:\other\other.md";

    [Fact]
    public async Task OpenedFilesAreWatchedUntilTheirTabsClose()
    {
        var harness = Create();

        await harness.ViewModel.OpenPathAsync(NotePath);
        await harness.ViewModel.OpenPathAsync(OtherPath);
        Assert.Equal(new[] { NotePath, OtherPath }, harness.Watcher.Watched);

        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);
        Assert.Equal(new[] { NotePath }, harness.Watcher.Watched);
    }

    [Fact]
    public async Task ChangedFileIsReloadedSilently()
    {
        var harness = Create();
        await harness.ViewModel.OpenPathAsync(NotePath);
        harness.Loader.Sources[NotePath] = new MarkdownSource(NotePath, "note.md", "# written by agent");

        await harness.ViewModel.ApplyDocumentChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Changed, NotePath)]);

        Assert.Equal("# written by agent", harness.ViewModel.Document!.Content);
        Assert.False(harness.ViewModel.ShowsExternalChangeBanner);
    }

    [Fact]
    public async Task AtomicSaveOverOpenFileReloadsInsteadOfMovingTheTab()
    {
        // Редактор пишет во временный файл и переименовывает его поверх открытого.
        var harness = Create();
        await harness.ViewModel.OpenPathAsync(NotePath);
        harness.Loader.Sources[NotePath] = new MarkdownSource(NotePath, "note.md", "# saved atomically");

        await harness.ViewModel.ApplyDocumentChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Renamed, NotePath, @"C:\notes\note.md.tmp")]);

        Assert.Equal(NotePath, harness.ViewModel.OpenDocuments.ActiveTab!.Path);
        Assert.Equal("# saved atomically", harness.ViewModel.Document!.Content);
    }

    [Fact]
    public async Task DeleteFollowedByRewriteKeepsTheTab()
    {
        // Удаление и тут же новая запись приходят одной пачкой: файл на месте — вкладку не закрываем.
        var harness = Create(fileExists: static _ => true);
        await harness.ViewModel.OpenPathAsync(NotePath);
        harness.Loader.Sources[NotePath] = new MarkdownSource(NotePath, "note.md", "# rewritten");

        await harness.ViewModel.ApplyDocumentChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Deleted, NotePath)]);

        Assert.Single(harness.ViewModel.OpenDocuments.Tabs);
        Assert.Equal("# rewritten", harness.ViewModel.Document!.Content);
    }

    [Fact]
    public async Task OwnSaveDoesNotThrowTheUserOutOfEditMode()
    {
        // Сохранение из редактора watcher тоже видит; на диске ровно текст редактора.
        var harness = Create();
        await harness.ViewModel.OpenPathAsync(NotePath);
        harness.ViewModel.ToggleEditModeCommand.Execute(null);

        await harness.ViewModel.ApplyDocumentChangesAsync(
            [new WorkspaceChange(WorkspaceChangeKind.Changed, NotePath)]);

        Assert.True(harness.ViewModel.IsEditMode);
        Assert.NotNull(harness.ViewModel.EditorSession);
    }

    [Fact]
    public async Task FileSystemWatcherReportsAtomicReplaceOfWatchedFile()
    {
        var directory = Directory.CreateTempSubdirectory("markmello-watch-").FullName;
        try
        {
            var path = Path.Combine(directory, "note.md");
            var temp = Path.Combine(directory, "note.md.tmp");
            await File.WriteAllTextAsync(path, "# first");
            await File.WriteAllTextAsync(Path.Combine(directory, "unrelated.md"), "# skip");

            using var watcher = new FileSystemDocumentWatcher();
            var received = new TaskCompletionSource<IReadOnlyList<WorkspaceChange>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Changed += (_, batch) => received.TrySetResult(batch);
            watcher.Watch([path]);

            await File.WriteAllTextAsync(temp, "# second");
            File.Move(temp, path, overwrite: true);

            var batch = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Contains(batch, change => string.Equals(change.Path, path, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(batch, change => change.Path.EndsWith("unrelated.md", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Harness Create(Func<string, bool>? fileExists = null)
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        var loader = new CountingDocumentLoader();
        loader.Sources[NotePath] = new MarkdownSource(NotePath, "note.md", "# note");
        loader.Sources[OtherPath] = new MarkdownSource(OtherPath, "other.md", "# other");

        var watcher = new RecordingDocumentWatcher();
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
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher(),
            fileExists: fileExists ?? (static _ => false),
            documentWatcherFactory: () => watcher);

        return new Harness(loader, watcher, viewModel);
    }

    private sealed record Harness(
        CountingDocumentLoader Loader,
        RecordingDocumentWatcher Watcher,
        ShellViewModel ViewModel);
}

/// <summary>Watcher без файловой системы: тест видит только набор отслеживаемых файлов.</summary>
internal sealed class RecordingDocumentWatcher : IDocumentWatcher
{
    public IReadOnlyCollection<string> Watched { get; private set; } = [];

    public event EventHandler<IReadOnlyList<WorkspaceChange>>? Changed
    {
        add { }
        remove { }
    }

    public void Watch(IReadOnlyCollection<string> filePaths) => Watched = [.. filePaths];

    public void Dispose() => Watched = [];
}
