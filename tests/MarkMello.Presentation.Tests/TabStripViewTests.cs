using Avalonia.Controls;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Разметка полосы вкладок: биндинги на команды закрытия и активации идут через
/// $parent[ItemsControl] и в unit-тестах view-model не проверяются вовсе.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class TabStripViewTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public TabStripViewTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Разметка полосы собирается и биндится на реальную view-model.
    /// Сами вкладки в headless не материализуются (контейнеры списков там не строятся),
    /// поэтому состав проверяется по источнику, а не по отрисованным строкам.
    /// </summary>
    [Fact]
    public Task StripBindsToVisibleTabs()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = CreateViewModel();
            // Вкладки существуют только в режиме открытой папки (ADR-0007 Rule 4).
            await viewModel.OpenFolderPathAsync(@"C:\docs");
            await viewModel.OpenPathAsync(@"C:\docs\first.md");
            await viewModel.OpenPathAsync(@"C:\docs\second.md");

            var window = new Window
            {
                DataContext = viewModel,
                Width = 900,
                Height = 200,
                Content = new TabStripView()
            };

            window.Show();
            window.UpdateLayout();

            var strip = window.GetVisualDescendants().OfType<ItemsControl>().Single();

            Assert.Same(viewModel.OpenDocuments.VisibleTabs, strip.ItemsSource);
            Assert.Equal(
                ["first.md", "second.md"],
                viewModel.OpenDocuments.VisibleTabs.Select(tab => tab.Title));

            window.Close();
        });
    }

    [Fact]
    public Task ClosingATabLeavesTheOther()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = CreateViewModel();
            // Вкладки существуют только в режиме открытой папки (ADR-0007 Rule 4).
            await viewModel.OpenFolderPathAsync(@"C:\docs");
            await viewModel.OpenPathAsync(@"C:\docs\first.md");
            await viewModel.OpenPathAsync(@"C:\docs\second.md");

            var first = viewModel.OpenDocuments.Tabs.Single(tab => tab.Title == "first.md");
            await viewModel.OpenDocuments.CloseCommand.ExecuteAsync(first);

            Assert.Equal(["second.md"], viewModel.OpenDocuments.Tabs.Select(tab => tab.Title));
        });
    }

    private static ShellViewModel CreateViewModel()
    {
        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\first.md"] = new MarkdownSource(@"C:\docs\first.md", "first.md", "# first");
        loader.Sources[@"C:\docs\second.md"] = new MarkdownSource(@"C:\docs\second.md", "second.md", "# second");

        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            @"C:\docs",
            WorkspaceEntry.ForFile(@"C:\docs\first.md", "first.md"),
            WorkspaceEntry.ForFile(@"C:\docs\second.md", "second.md"));

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
