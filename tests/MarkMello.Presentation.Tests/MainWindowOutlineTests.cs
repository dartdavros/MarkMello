using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MainWindowOutlineTests(AvaloniaHeadlessFixture fixture)
{
    private const int FileColumn = 0;
    private const int OutlineLeftColumn = 0;
    private const int OutlineRightColumn = 4;

    [Fact]
    public Task HiddenPanelKeepsOutlineColumnsCollapsedAndFileColumnUntouched()
    {
        return RunWithWindowAsync(outlineVisible: false, (shell, window) =>
        {
            var columns = GetColumns(window);
            var left = GetLeftColumns(window);

            Assert.Equal(5, columns.Count);
            Assert.Equal(3, left.Count);
            AssertCollapsed(left[OutlineLeftColumn]);
            AssertCollapsed(columns[OutlineRightColumn]);

            var (minWidth, width) = MainWindow.CalculateSidebarColumn(shell.ShowsSidebar, shell.SidebarWidth);
            Assert.Equal(minWidth, columns[FileColumn].MinWidth);
            Assert.Equal(width, columns[FileColumn].Width);
        });
    }

    [Fact]
    public Task PanelOnTheLeftGivesWidthToLeftColumnOnly()
    {
        return RunWithWindowAsync(outlineVisible: false, (shell, window) =>
        {
            shell.OutlinePreferences = shell.OutlinePreferences with
            {
                IsVisible = true,
                Side = OutlinePanelSide.Left,
                Width = 300
            };

            var columns = GetColumns(window);
            var left = GetLeftColumns(window);
            Assert.Equal(300d, left[OutlineLeftColumn].Width.Value);
            Assert.True(left[OutlineLeftColumn].Width.IsAbsolute);
            Assert.Equal(OutlinePreferences.MinWidth, left[OutlineLeftColumn].MinWidth);
            AssertCollapsed(columns[OutlineRightColumn]);
        });
    }

    [Fact]
    public Task PanelOnTheRightGivesWidthToRightColumnOnly()
    {
        return RunWithWindowAsync(outlineVisible: false, (shell, window) =>
        {
            shell.OutlinePreferences = shell.OutlinePreferences with
            {
                IsVisible = true,
                Side = OutlinePanelSide.Right,
                Width = 260
            };

            var columns = GetColumns(window);
            var left = GetLeftColumns(window);
            Assert.Equal(260d, columns[OutlineRightColumn].Width.Value);
            Assert.Equal(OutlinePreferences.MinWidth, columns[OutlineRightColumn].MinWidth);
            AssertCollapsed(left[OutlineLeftColumn]);
        });
    }

    [Fact]
    public Task ToggleCommandShowsAndHidesTheColumns()
    {
        return RunWithWindowAsync(outlineVisible: false, (shell, window) =>
        {
            var columns = GetColumns(window);
            var left = GetLeftColumns(window);
            AssertCollapsed(left[OutlineLeftColumn]);

            shell.ToggleOutlineCommand.Execute(null);

            Assert.Equal(shell.OutlinePreferences.Width, left[OutlineLeftColumn].Width.Value);
            Assert.Equal(OutlinePreferences.MinWidth, left[OutlineLeftColumn].MinWidth);

            shell.ToggleOutlineCommand.Execute(null);

            AssertCollapsed(left[OutlineLeftColumn]);
            AssertCollapsed(columns[OutlineRightColumn]);
        });
    }

    [Fact]
    public Task OutlinePanelIsCreatedInTheColumnOfItsSide()
    {
        return RunWithWindowAsync(outlineVisible: true, (shell, window) =>
        {
            var leftPanel = window.GetVisualDescendants().OfType<OutlinePanelView>().SingleOrDefault();
            Assert.NotNull(leftPanel);
            Assert.Contains(leftPanel.GetVisualAncestors(), a => a is Grid { Name: "OutlineLeftLayout" });

            shell.OutlinePreferences = shell.OutlinePreferences with { Side = OutlinePanelSide.Right };
            var panel = window.GetVisualDescendants().OfType<OutlinePanelView>().Single();

            Assert.Same(shell, panel.DataContext);
            Assert.Equal(OutlineRightColumn, GetGridColumn(panel));
        });
    }

    [Fact]
    public Task FloatingGroupIsVisibleInViewer()
    {
        return RunWithWindowAsync(outlineVisible: false, (shell, window) =>
        {
            var group = FindFloatingGroup(window);
            Assert.True(group.IsVisible);
            Assert.True(shell.ShowsFloatingOutlineButton);
            Assert.True(shell.ShowsFloatingChromeGroup);
        });
    }

    [Fact]
    public Task MinimapHostCarriesNoMinimapWhileOutlineIsOnTheRight()
    {
        return RunWithWindowAsync(outlineVisible: false, (shell, window) =>
        {
            shell.OutlinePreferences = shell.OutlinePreferences with
            {
                IsVisible = true,
                Side = OutlinePanelSide.Right
            };
            Dispatcher.UIThread.RunJobs();

            var host = window.GetVisualDescendants()
                .OfType<ViewerView>()
                .Single()
                .FindControl<ContentControl>("MinimapHost");

            Assert.NotNull(host);
            // Короткий документ в headless не даёт настоящей прокрутки, поэтому проверяется
            // только отсутствие миникарты и перехвата указателя; снятие уже построенной миникарты
            // здесь не воспроизвести.
            Assert.Null(host.Content);
            Assert.False(host.IsHitTestVisible);
        });
    }

    private Task RunWithWindowAsync(bool outlineVisible, Action<ShellViewModel, MainWindow> body)
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(withHeadings: true, outlineVisible: outlineVisible);
            var window = new MainWindow(
                shell,
                StartupSmokeTestOptions.Disabled,
                new InMemorySettingsStore(),
                new RecordingStartupMetrics());
            window.Show();
            try
            {
                // Стартовая инициализация VM может перечитать настройки; даём ей закончиться.
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
                shell.OutlinePreferences = shell.OutlinePreferences with { IsVisible = outlineVisible };
                Dispatcher.UIThread.RunJobs();

                body(shell, window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ColumnDefinitions GetColumns(MainWindow window)
        => window.FindControl<Grid>("SidebarLayout")!.ColumnDefinitions;

    private static ColumnDefinitions GetLeftColumns(MainWindow window)
        => window.FindControl<Grid>("OutlineLeftLayout")!.ColumnDefinitions;

    private static void AssertCollapsed(ColumnDefinition column)
    {
        Assert.Equal(0d, column.Width.Value);
        Assert.Equal(0d, column.MinWidth);
    }

    private static int GetGridColumn(Control control)
    {
        for (Avalonia.Visual? current = control; current is not null; current = current.GetVisualParent())
        {
            if (current is Control { Parent: Grid { Name: "SidebarLayout" } } child)
            {
                return Grid.GetColumn(child);
            }
        }

        return -1;
    }

    private static Border FindFloatingGroup(MainWindow window)
        => window.GetVisualDescendants()
            .OfType<Border>()
            .First(border => border.Classes.Contains("mm-top-chrome")
                && border.HorizontalAlignment == HorizontalAlignment.Left);
}
