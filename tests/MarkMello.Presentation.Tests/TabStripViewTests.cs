using Avalonia.Controls;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
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

    [Fact]
    public Task StripRendersOneItemPerVisibleTab()
    {
        return _fixture.Session.Dispatch(async () =>
        {
            var viewModel = CreateViewModel();
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

            var titles = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.Classes.Contains("mm-tab-title"))
                .Select(block => block.Text)
                .ToList();

            Assert.Equal(["first.md", "second.md"], titles);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task CloseButtonInsideTabClosesThatTab()
    {
        return _fixture.Session.Dispatch(async () =>
        {
            var viewModel = CreateViewModel();
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

            var closeButton = window.GetVisualDescendants()
                .OfType<Button>()
                .First(button => button.Classes.Contains("mm-tab-close"));

            Assert.NotNull(closeButton.Command);

            closeButton.Command!.Execute(closeButton.CommandParameter);

            Assert.Equal(["second.md"], viewModel.OpenDocuments.Tabs.Select(tab => tab.Title));

            window.Close();
        }, CancellationToken.None);
    }

    private static ShellViewModel CreateViewModel()
    {
        var loader = new StubDocumentLoader();
        loader.Sources[@"C:\docs\first.md"] = new MarkdownSource(@"C:\docs\first.md", "first.md", "# first");
        loader.Sources[@"C:\docs\second.md"] = new MarkdownSource(@"C:\docs\second.md", "second.md", "# second");

        var fileSystem = new FakeWorkspaceFileSystem();

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
            new ExpandFolderNodeUseCase(fileSystem));
    }
}
