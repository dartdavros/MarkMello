using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Infrastructure.Platform.SingleInstance;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.Services;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Маршрутизация повторных запусков (#42, PR #51): уже открытый файл, папка вместе
/// с файлом из неё и одновременные запуски.
/// </summary>
public sealed class InstanceActivationRouterTests
{
    private const string FirstRoot = @"C:\first";
    private const string SecondRoot = @"C:\second";
    private const string NotePath = @"C:\notes\note.md";
    private const string OtherPath = @"C:\notes\other.md";
    private const string SecondFile = @"C:\second\file.md";
    private const string FirstFile = @"C:\first\a.md";

    [Fact]
    public async Task ReopeningAFileWithUnsavedEditsActivatesItsTabInsteadOfReloading()
    {
        // Ревью #51, п.1: повторное открытие не перечитывает файл и не сбрасывает editor-сессию.
        var context = new Context();
        var shell = context.CreateShell();
        await shell.OpenPathAsync(NotePath);
        shell.ToggleEditModeCommand.Execute(null);
        shell.EditorSession!.SourceText = "# unsaved";
        await shell.OpenPathAsync(OtherPath);
        var router = context.CreateRouter(shell);

        await router.RouteAsync([NotePath]);

        Assert.Equal(NotePath, shell.OpenDocuments.ActiveTab!.Path);
        Assert.Equal("# unsaved", shell.EditorSession!.SourceText);
        Assert.True(shell.IsDirty);
        Assert.Equal(2, shell.OpenDocuments.Tabs.Count);
    }

    [Fact]
    public async Task RelaunchWithAnOpenFolderKeepsItsTabsAndUnsavedEdits()
    {
        // Повторное ревью #51: «MarkMello C:\docs C:\docs\first.md» при уже открытой папке
        // только выводит окно вперёд — сессия папки не восстанавливается заново.
        var context = new Context();
        var shell = context.CreateShell();
        await shell.OpenFolderPathAsync(FirstRoot);
        await shell.OpenPathAsync(FirstFile);
        context.Settings.Session = new WorkspaceSessionState(FirstRoot, [FirstFile], FirstFile, []);
        shell.ToggleEditModeCommand.Execute(null);
        shell.EditorSession!.SourceText = "# unsaved";
        var router = context.CreateRouter(shell);

        await router.RouteAsync([FirstRoot, FirstFile]);

        Assert.Equal(FirstFile, shell.OpenDocuments.ActiveTab!.Path);
        Assert.Equal("# unsaved", shell.EditorSession!.SourceText);
        Assert.True(shell.IsDirty);
        Assert.Empty(context.Launcher.NewWindowFolders);
    }

    [Fact]
    public async Task FileFromAFolderOpenedInANewWindowGoesToThatWindow()
    {
        // Ревью #51, п.2: «B B/file.md» при открытой папке A — файл уходит в окно папки B.
        var context = new Context();
        var first = context.CreateShell();
        await first.OpenFolderPathAsync(FirstRoot);
        var router = context.CreateRouter(first);

        await router.RouteAsync([SecondRoot, SecondFile]);

        var second = Assert.Single(context.Launcher.NewWindowShells);
        Assert.Equal(SecondRoot, second.Workspace!.Folder.RootPath);
        Assert.Equal(SecondFile, second.OpenDocuments.ActiveTab!.Path);
        Assert.Null(first.OpenDocuments.FindByPath(SecondFile));
        Assert.Equal(FirstRoot, first.Workspace!.Folder.RootPath);
    }

    [Fact]
    public async Task SimultaneousLaunchesWithFoldersAreHandledOneByOne()
    {
        // Ревью #51, п.3: два запуска с папками одновременно — первая папка остаётся в окне,
        // вторая получает своё окно, а не заменяет первую.
        var context = new Context(yieldOnRead: true);
        var shell = context.CreateShell();
        var router = context.CreateRouter(shell);

        await Task.WhenAll(router.RouteAsync([FirstRoot]), router.RouteAsync([SecondRoot]));

        Assert.Equal(FirstRoot, shell.Workspace!.Folder.RootPath);
        Assert.Equal([SecondRoot], context.Launcher.NewWindowFolders);
    }

    [Fact]
    public async Task ConcurrentFolderOpensInOneWindowDoNotReplaceEachOther()
    {
        // Та же гонка без маршрутизатора: папка из аргумента старта и папка из повторного запуска.
        var context = new Context(yieldOnRead: true);
        var shell = context.CreateShell();

        await Task.WhenAll(shell.OpenFolderPathAsync(FirstRoot), shell.OpenFolderPathAsync(SecondRoot));

        Assert.Equal(FirstRoot, shell.Workspace!.Folder.RootPath);
        Assert.Equal([SecondRoot], context.Launcher.NewWindowFolders);
    }

    private sealed class Context
    {
        private readonly FakeWorkspaceFileSystem _fileSystem = new();
        private readonly IWorkspaceFileSystem _readFileSystem;
        private readonly CountingDocumentLoader _loader = new();

        public Context(bool yieldOnRead = false)
        {
            _fileSystem.AddDirectory(FirstRoot, WorkspaceEntry.ForFile(FirstFile, "a.md"));
            _fileSystem.AddDirectory(SecondRoot, WorkspaceEntry.ForFile(SecondFile, "file.md"));
            _readFileSystem = yieldOnRead ? new YieldingWorkspaceFileSystem(_fileSystem) : _fileSystem;

            foreach (var path in new[] { NotePath, OtherPath, SecondFile, FirstFile })
            {
                _loader.Sources[path] = new MarkdownSource(path, Path.GetFileName(path), $"# {Path.GetFileName(path)}");
            }

            Launcher.NewWindowFactory = CreateShell;
        }

        public RecordingWindowLauncher Launcher { get; } = new();

        /// <summary>Настройки окна: сохранённая сессия папки восстанавливается при её открытии.</summary>
        public InMemorySettingsStore Settings { get; } = new();

        public InstanceActivationRouter CreateRouter(ShellViewModel target)
        {
            Launcher.ActivationTarget = target;
            return new InstanceActivationRouter(
                new StubCommandLineActivation(),
                NullInstanceActivation.Instance,
                Launcher,
                _fileSystem.DirectoryExists);
        }

        public ShellViewModel CreateShell()
        {
            var platform = new FakePlatformServices(_fileSystem);
            return new ShellViewModel(
                new OpenDocumentUseCase(_loader),
                new SaveDocumentUseCase(new RecordingDocumentSaver()),
                new StubFilePicker(),
                new StubCommandLineActivation(),
                new LocalizationService(AppLanguage.English),
                Settings,
                new RecordingThemeService(),
                new RecordingStartupMetrics(),
                new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
                new StubUpdateService(),
                new OpenFolderUseCase(_readFileSystem),
                new ExpandFolderNodeUseCase(_fileSystem),
                new SearchWorkspaceFilesUseCase(_fileSystem),
                new WorkspaceFileOperationsUseCase(_fileSystem, platform),
                platform,
                static () => new FakeWorkspaceWatcher(),
                Launcher,
                fileExists: _fileSystem.Exists);
        }
    }

    /// <summary>
    /// Чтение каталога уступает поток, как настоящий диск: без этого открытия папки
    /// выполнялись бы синхронно, и гонка в тесте не воспроизводилась бы.
    /// </summary>
    private sealed class YieldingWorkspaceFileSystem(IWorkspaceFileSystem inner) : IWorkspaceFileSystem
    {
        public async ValueTask<IReadOnlyList<WorkspaceEntry>> EnumerateChildrenAsync(
            string directoryPath,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return await inner.EnumerateChildrenAsync(directoryPath, cancellationToken);
        }

        public bool DirectoryExists(string directoryPath) => inner.DirectoryExists(directoryPath);

        public ValueTask<WorkspaceEntry> CreateFileAsync(string directoryPath, string name, CancellationToken cancellationToken = default)
            => inner.CreateFileAsync(directoryPath, name, cancellationToken);

        public ValueTask<WorkspaceEntry> CreateDirectoryAsync(string directoryPath, string name, CancellationToken cancellationToken = default)
            => inner.CreateDirectoryAsync(directoryPath, name, cancellationToken);

        public ValueTask<WorkspaceEntry> RenameAsync(string path, string newName, CancellationToken cancellationToken = default)
            => inner.RenameAsync(path, newName, cancellationToken);

        public ValueTask<WorkspaceEntry> DuplicateAsync(string path, string duplicateName, CancellationToken cancellationToken = default)
            => inner.DuplicateAsync(path, duplicateName, cancellationToken);

        public ValueTask DeleteAsync(string path, CancellationToken cancellationToken = default)
            => inner.DeleteAsync(path, cancellationToken);

        public bool Exists(string path) => inner.Exists(path);

        public ValueTask<IReadOnlyList<string>> GetChildNamesAsync(string directoryPath, CancellationToken cancellationToken = default)
            => inner.GetChildNamesAsync(directoryPath, cancellationToken);

        public ValueTask<int> CountChildrenAsync(string directoryPath, CancellationToken cancellationToken = default)
            => inner.CountChildrenAsync(directoryPath, cancellationToken);

        public ValueTask<WorkspaceSearchResult> SearchByNameAsync(
            string rootPath,
            string query,
            WorkspaceSearchLimits limits,
            CancellationToken cancellationToken = default)
            => inner.SearchByNameAsync(rootPath, query, limits, cancellationToken);
    }
}
