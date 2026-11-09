using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Перетаскивание границ в <see cref="MainWindow"/>: файловый сайдбар и оглавление.
/// Тянет GridSplitter так же, как Thumb: DragStarted, DragDelta, DragCompleted.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MainWindowSplitterTests(AvaloniaHeadlessFixture fixture)
{
    private const string Root = @"C:\docs";

    [Fact]
    public Task FileSplitterGrowsFileColumnAndLeavesDocumentWithTheRest()
        => RunAsync(OutlinePanelSide.Left, outlineVisible: false, (shell, window) =>
        {
            var file = FileColumn(window);
            var documentBefore = DocumentArea(window).Bounds.Width;
            var before = file.ActualWidth;

            Drag(FileSplitter(window), window, 60);

            Assert.Equal(before + 60, file.ActualWidth, 1);
            Assert.Equal(file.ActualWidth, file.Width.Value, 1);
            Assert.Equal(documentBefore - 60, DocumentArea(window).Bounds.Width, 1);
            Assert.Equal(file.Width.Value, shell.SidebarWidth, 1);
            AssertCollapsed(OutlineColumn(window, OutlinePanelSide.Left));
            AssertCollapsed(OutlineColumn(window, OutlinePanelSide.Right));
        });

    [Fact]
    public Task FileSplitterShrinksFileColumnInsideTheAllowedRange()
        => RunAsync(OutlinePanelSide.Left, outlineVisible: false, (shell, window) =>
        {
            var file = FileColumn(window);
            var documentBefore = DocumentArea(window).Bounds.Width;
            var before = file.ActualWidth;

            Drag(FileSplitter(window), window, -40);

            Assert.Equal(before - 40, file.ActualWidth, 1);
            Assert.Equal(documentBefore + 40, DocumentArea(window).Bounds.Width, 1);
            Assert.InRange(file.Width.Value, WorkspaceSidebarWidth.Minimum, WorkspaceSidebarWidth.Maximum);
            Assert.Equal(file.Width.Value, shell.SidebarWidth, 1);
            AssertCollapsed(OutlineColumn(window, OutlinePanelSide.Left));
            AssertCollapsed(OutlineColumn(window, OutlinePanelSide.Right));
        });

    [Theory]
    [InlineData(OutlinePanelSide.Left)]
    [InlineData(OutlinePanelSide.Right)]
    public Task FileSplitterDoesNotTouchVisibleOutline(OutlinePanelSide side)
        => RunAsync(side, outlineVisible: true, (shell, window) =>
        {
            var file = FileColumn(window);
            var outline = OutlineColumn(window, side);
            var outlineBefore = outline.ActualWidth;
            var preferenceBefore = shell.OutlinePreferences.Width;
            var before = file.ActualWidth;

            Drag(FileSplitter(window), window, 60);

            Assert.Equal(before + 60, file.ActualWidth, 1);
            Assert.Equal(outlineBefore, outline.ActualWidth, 1);
            Assert.Equal(preferenceBefore, shell.OutlinePreferences.Width);
            AssertCollapsed(OutlineColumn(window, side == OutlinePanelSide.Left ? OutlinePanelSide.Right : OutlinePanelSide.Left));
        });

    [Theory]
    [InlineData(OutlinePanelSide.Left, 40)]
    [InlineData(OutlinePanelSide.Left, -30)]
    [InlineData(OutlinePanelSide.Right, 40)]
    [InlineData(OutlinePanelSide.Right, -30)]
    public Task OutlineSplitterResizesOutlineAndStoresWidth(OutlinePanelSide side, double delta)
        => RunAsync(side, outlineVisible: true, (shell, window) =>
        {
            var outline = OutlineColumn(window, side);
            var fileBefore = FileColumn(window).ActualWidth;
            var before = outline.ActualWidth;
            // Левая граница тянется вправо - панель шире, правая - наоборот.
            var effect = side == OutlinePanelSide.Left ? delta : -delta;

            Drag(OutlineSplitter(window, side), window, delta);

            Assert.Equal(before + effect, outline.ActualWidth, 1);
            Assert.InRange(outline.Width.Value, OutlinePreferences.MinWidth, OutlinePreferences.MaxWidth);
            Assert.Equal(outline.Width.Value, shell.OutlinePreferences.Width, 1);
            Assert.Equal(fileBefore, FileColumn(window).ActualWidth, 1);
        });

    [Theory]
    [InlineData(OutlinePanelSide.Left)]
    [InlineData(OutlinePanelSide.Right)]
    public Task OutlineSplitterGrowsPastFileSidebarMaximum(OutlinePanelSide side)
        => RunAsync(side, outlineVisible: true, (shell, window) =>
        {
            var outline = OutlineColumn(window, side);
            var delta = side == OutlinePanelSide.Left ? 300d : -300d;

            Drag(OutlineSplitter(window, side), window, delta);

            Assert.True(outline.Width.Value > 340, $"column {outline.Width.Value}");
            Assert.InRange(outline.Width.Value, 341, OutlinePreferences.MaxWidth);
            Assert.True(shell.OutlinePreferences.Width > 340, $"pref {shell.OutlinePreferences.Width}");
            Assert.InRange(shell.OutlinePreferences.Width, 341, OutlinePreferences.MaxWidth);
        }, WideWindowWidth);

    [Theory]
    [InlineData(OutlinePanelSide.Left)]
    [InlineData(OutlinePanelSide.Right)]
    public Task OutlineSplitterClampsAtOutlineMaximum(OutlinePanelSide side)
        => RunAsync(side, outlineVisible: true, (shell, window) =>
        {
            var outline = OutlineColumn(window, side);
            var delta = side == OutlinePanelSide.Left ? 800d : -800d;

            Drag(OutlineSplitter(window, side), window, delta);

            Assert.Equal(OutlinePreferences.MaxWidth, outline.Width.Value, 1);
            Assert.Equal(OutlinePreferences.MaxWidth, shell.OutlinePreferences.Width, 1);
        }, WideWindowWidth);

    private const double WideWindowWidth = 1600;

    // Узкое окно: файловая панель до 340 и оглавление до 600 оставили бы документу меньше 320.
    private const double NarrowWindowWidth = 900;
    private const double DocumentMinWidth = 320;

    [Fact]
    public Task FileSplitterKeepsDocumentWideWithLeftOutline()
        => RunAsync(OutlinePanelSide.Left, outlineVisible: true, (shell, window) =>
        {
            var outlineBefore = OutlineColumn(window, OutlinePanelSide.Left).ActualWidth;

            Drag(FileSplitter(window), window, 1000);

            Assert.True(NestedDocumentColumn(window).ActualWidth >= DocumentMinWidth - 1, $"document {NestedDocumentColumn(window).ActualWidth}");
            Assert.True(DocumentArea(window).Bounds.Width >= DocumentMinWidth - 1, $"area {DocumentArea(window).Bounds.Width}");
            Assert.Equal(outlineBefore, OutlineColumn(window, OutlinePanelSide.Left).ActualWidth, 1);
        }, NarrowWindowWidth);

    [Fact]
    public Task RightOutlineSplitterKeepsDocumentWide()
        => RunAsync(OutlinePanelSide.Right, outlineVisible: true, (shell, window) =>
        {
            Drag(OutlineSplitter(window, OutlinePanelSide.Right), window, -1000);

            Assert.True(DocumentArea(window).Bounds.Width >= DocumentMinWidth - 1, $"area {DocumentArea(window).Bounds.Width}");
        }, NarrowWindowWidth);

    [Fact]
    public Task LeftOutlineSplitterKeepsDocumentWide()
        => RunAsync(OutlinePanelSide.Left, outlineVisible: true, (shell, window) =>
        {
            Drag(OutlineSplitter(window, OutlinePanelSide.Left), window, 1000);

            Assert.True(DocumentArea(window).Bounds.Width >= DocumentMinWidth - 1, $"area {DocumentArea(window).Bounds.Width}");
        }, NarrowWindowWidth);

    [Theory]
    [InlineData(OutlinePanelSide.Left, true, 320d, 320d + 280d)]
    [InlineData(OutlinePanelSide.Right, true, 0d, 320d)]
    [InlineData(OutlinePanelSide.Left, false, 0d, 0d)]
    [InlineData(OutlinePanelSide.Right, false, 0d, 0d)]
    public Task DocumentColumnsMinWidthFollowsOutline(OutlinePanelSide side, bool visible, double nested, double top)
        => RunAsync(side, visible, (shell, window) =>
        {
            Assert.Equal(nested, NestedDocumentColumn(window).MinWidth);
            Assert.Equal(top, SidebarLayout(window).ColumnDefinitions[2].MinWidth);

            shell.OutlinePreferences = shell.OutlinePreferences with { IsVisible = false };
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0d, NestedDocumentColumn(window).MinWidth);
            Assert.Equal(0d, SidebarLayout(window).ColumnDefinitions[2].MinWidth);
        });

    private static ColumnDefinition NestedDocumentColumn(MainWindow window)
        => window.FindControl<Grid>("OutlineLeftLayout")!.ColumnDefinitions[2];

    private Task RunAsync(OutlinePanelSide side, bool outlineVisible, Action<ShellViewModel, MainWindow> body, double? windowWidth = null)
        => fixture.RunAsync(async () =>
        {
            var shell = await CreateWorkspaceShellAsync();
            var window = new MainWindow(
                shell,
                StartupSmokeTestOptions.Disabled,
                new InMemorySettingsStore(),
                new RecordingStartupMetrics());
            if (windowWidth is { } width)
            {
                window.Width = width;
            }

            window.Show();
            try
            {
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
                shell.OutlinePreferences = shell.OutlinePreferences with
                {
                    IsVisible = outlineVisible,
                    Side = side,
                    Width = 280
                };
                Dispatcher.UIThread.RunJobs();
                Assert.True(shell.ShowsSidebar);
                Layout(window);

                body(shell, window);
            }
            finally
            {
                window.Close();
            }
        });

    private static void Layout(MainWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Drag(GridSplitter splitter, MainWindow window, double delta)
    {
        Layout(window);
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragStartedEvent, Vector = default });
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(delta, 0) });
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = new Vector(delta, 0) });
        Layout(window);
    }

    private static Grid SidebarLayout(MainWindow window) => window.FindControl<Grid>("SidebarLayout")!;

    private static ColumnDefinition FileColumn(MainWindow window) => SidebarLayout(window).ColumnDefinitions[0];

    private static GridSplitter FileSplitter(MainWindow window)
        => SidebarLayout(window).Children.OfType<GridSplitter>().Single(s => Grid.GetColumn(s) == 1 && s.Name is null);

    private static GridSplitter OutlineSplitter(MainWindow window, OutlinePanelSide side)
        => window.FindControl<GridSplitter>(side == OutlinePanelSide.Left ? "OutlineLeftSplitter" : "OutlineRightSplitter")!;

    private static ColumnDefinition OutlineColumn(MainWindow window, OutlinePanelSide side)
    {
        var splitter = OutlineSplitter(window, side);
        var grid = (Grid)splitter.Parent!;
        // Колонка панели соседняя с границей: слева от левой границы и справа от правой.
        var index = Grid.GetColumn(splitter) + (side == OutlinePanelSide.Left ? -1 : 1);
        return grid.ColumnDefinitions[index];
    }

    private static Control DocumentArea(MainWindow window)
    {
        var body = window.FindControl<Panel>("BodyPanel")!;
        return (Control)body.Parent!;
    }

    private static void AssertCollapsed(ColumnDefinition column)
    {
        Assert.Equal(0d, column.Width.Value);
        Assert.Equal(0d, column.MinWidth);
    }

    private static async Task<ShellViewModel> CreateWorkspaceShellAsync()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForFile(@"C:\docs\README.md", "README.md"));

        var loader = new CountingDocumentLoader();
        loader.Sources[@"C:\docs\README.md"] = new MarkdownSource(@"C:\docs\README.md", "README.md", "# One\n\ntext\n\n# Two\n");

        var shell = new ShellViewModel(
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
        await shell.OpenFolderPathAsync(Root);
        return shell;
    }
}
