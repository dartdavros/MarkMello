using Avalonia.Controls;
using Avalonia.LogicalTree;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class OutlinePanelViewTests(AvaloniaHeadlessFixture fixture)
{
    [Fact]
    public Task LoadsWithoutExceptionsWhileOutlineIsHidden()
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(withHeadings: true, outlineVisible: false);
            Assert.Null(shell.Outline);
            var panel = new OutlinePanelView();
            var window = new Window { DataContext = shell, Content = panel };
            window.Show();
            try
            {
                Assert.False(FindByName<TextBlock>(panel, "OutlineEmptyText").IsVisible);
                Assert.Equal(shell.OutlineHeader, FindByName<TextBlock>(panel, "OutlineHeaderText").Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public Task BindsTreeAndHidesEmptyStateWhenDocumentHasHeadings()
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(withHeadings: true, outlineVisible: true);
            Assert.NotNull(shell.Outline);
            var panel = new OutlinePanelView();
            var window = new Window { DataContext = shell, Content = panel };
            window.Show();
            try
            {
                var tree = FindByName<TreeView>(panel, "OutlineTree");
                Assert.Same(shell.Outline.Items, tree.ItemsSource);
                Assert.False(FindByName<TextBlock>(panel, "OutlineEmptyText").IsVisible);
                Assert.Equal(shell.OutlineHeader, FindByName<TextBlock>(panel, "OutlineHeaderText").Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public Task ShowsEmptyStateWhenDocumentHasNoHeadings()
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(withHeadings: false, outlineVisible: true);
            Assert.True(shell.Outline!.IsEmpty);
            var panel = new OutlinePanelView();
            var window = new Window { DataContext = shell, Content = panel };
            window.Show();
            try
            {
                var empty = FindByName<TextBlock>(panel, "OutlineEmptyText");
                Assert.True(empty.IsVisible);
                Assert.Equal(shell.OutlineEmpty, empty.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public Task NavigateRaisesRequestForTheItemLine()
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(withHeadings: true, outlineVisible: true);
            var panel = new OutlinePanelView { DataContext = shell };
            var requested = new List<int>();
            shell.Outline!.NavigateRequested += (_, line) => requested.Add(line);

            panel.Navigate(shell.Outline.Items[1]);

            Assert.Equal([shell.Outline.Items[1].SourceLine], requested);
        });
    }

    private static T FindByName<T>(Control root, string name)
        where T : Control
        => root.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
}
