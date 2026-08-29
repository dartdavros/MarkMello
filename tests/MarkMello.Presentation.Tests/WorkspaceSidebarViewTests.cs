using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Проверяет, что разметка сайдбара действительно собирается и биндится:
/// unit-тесты view-model этого не ловят — сломанный XAML падает только в рантайме.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class WorkspaceSidebarViewTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public WorkspaceSidebarViewTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task SidebarRendersRootLevelOfTheTree()
    {
        return _fixture.Session.Dispatch(async () =>
        {
            var viewModel = CreateViewModel();
            await viewModel.OpenFolderPathAsync(@"C:\docs");

            var window = new Window
            {
                DataContext = viewModel,
                Width = 400,
                Height = 600,
                Content = new WorkspaceSidebarView()
            };

            window.Show();

            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var nodes = Assert.IsAssignableFrom<IEnumerable<FileTreeNodeViewModel>>(tree.ItemsSource);

            Assert.Equal(["adr", "README.md", "pack.bat"], nodes.Select(node => node.Name));

            var rootLabel = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(block => block.Classes.Contains("mm-sidebar-root"));

            Assert.Equal("docs", rootLabel.Text);

            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Левый клик открывает документ, правый только показывает меню.
    /// Проверка на самой разметке: выделение строки документ не открывает,
    /// а обработчик клика висит на дереве, а не на view-model.
    /// </summary>
    [Theory]
    [InlineData(MouseButton.Left, @"C:\docs\README.md")]
    [InlineData(MouseButton.Right, null)]
    public Task OnlyTheLeftClickOpensTheDocument(MouseButton button, string? expectedPath)
    {
        return _fixture.Session.Dispatch(async () =>
        {
            var viewModel = CreateViewModel();
            await viewModel.OpenFolderPathAsync(@"C:\docs");

            var window = new Window
            {
                DataContext = viewModel,
                Width = 400,
                Height = 600,
                Content = new WorkspaceSidebarView()
            };

            window.Show();
            window.UpdateLayout();

            var row = window.GetVisualDescendants()
                .OfType<TreeViewItem>()
                .Single(item => item.DataContext is FileTreeNodeViewModel { Name: "README.md" });

            row.RaiseEvent(new PointerReleasedEventArgs(
                row,
                new Pointer(0, PointerType.Mouse, isPrimary: true),
                window,
                default,
                0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
                KeyModifiers.None,
                button));

            await Task.Yield();

            Assert.Equal(expectedPath, viewModel.CurrentDocumentPath);

            window.Close();
        }, CancellationToken.None);
    }

    private static ShellViewModel CreateViewModel()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            @"C:\docs",
            WorkspaceEntry.ForDirectory(@"C:\docs\adr", "adr"),
            WorkspaceEntry.ForFile(@"C:\docs\README.md", "README.md"),
            WorkspaceEntry.ForFile(@"C:\docs\pack.bat", "pack.bat"));

        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\README.md"] =
            new MarkdownSource(@"C:\docs\README.md", "README.md", "# readme");

        return new ShellViewModel(
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
    }
}
