using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class OutlineSettingsViewTests(AvaloniaHeadlessFixture fixture)
{
    [Fact]
    public Task SideToggleUpdatesOutlinePreferencesAndKeepsExactlyOneChecked()
    {
        return fixture.RunAsync(() =>
        {
            var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();
            var view = new OutlineSettingsView { DataContext = shell };
            var window = new Window { Content = view };
            window.Show();
            try
            {
                var left = SideButton(view, shell.OutlineSideLeft);
                var right = SideButton(view, shell.OutlineSideRight);
                Assert.True(left.IsChecked);
                Assert.False(right.IsChecked);

                right.IsChecked = true;
                Assert.Equal(OutlinePanelSide.Right, shell.OutlinePreferences.Side);
                Assert.True(right.IsChecked);
                Assert.False(left.IsChecked);

                left.IsChecked = true;
                Assert.Equal(OutlinePanelSide.Left, shell.OutlinePreferences.Side);
                Assert.True(left.IsChecked);
                Assert.False(right.IsChecked);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task MaxLevelSegmentsUpdatePreferencesAndDimDeeperLevelRows()
    {
        return fixture.RunAsync(() =>
        {
            var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();
            var view = new OutlineSettingsView { DataContext = shell };
            // The headless harness does not materialize list containers: build the production templates by hand.
            var segments = BuildRows(Assert.IsType<ItemsControl>(FindItemsControl(view, 0)), shell.OutlineLevelSettings);
            var sizeRows = BuildRows(view.FindControl<ItemsControl>("LevelSizesList")!, shell.OutlineLevelSettings);
            var window = new Window { Content = new StackPanel { Children = { view, segments, sizeRows } } };
            window.Show();
            try
            {
                var toggles = segments.GetLogicalDescendants().OfType<ToggleButton>().ToArray();
                Assert.Equal(OutlinePreferences.MaxLevelLimit, toggles.Length);
                Assert.True(toggles[OutlinePreferences.DefaultMaxLevel - 1].IsChecked);

                toggles[1].IsChecked = true;

                Assert.Equal(2, shell.OutlinePreferences.MaxLevel);
                Assert.Equal(1, toggles.Count(t => t.IsChecked == true));
                Assert.True(toggles[1].IsChecked);

                var rows = sizeRows.GetLogicalChildren().OfType<Control>().ToArray();
                for (var i = 0; i < rows.Length; i++)
                {
                    Assert.Equal(i < 2 ? 1.0 : 0.5, rows[i].Opacity);
                }
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task StepperButtonsChangeLineHeightAndLevelFontSize()
    {
        return fixture.RunAsync(() =>
        {
            var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();
            var view = new OutlineSettingsView { DataContext = shell };
            var sizeRows = BuildRows(view.FindControl<ItemsControl>("LevelSizesList")!, shell.OutlineLevelSettings);
            var window = new Window { Content = new StackPanel { Children = { view, sizeRows } } };
            window.Show();
            try
            {
                var lineHeight = shell.OutlinePreferences.LineHeight;
                var plus = view.GetLogicalDescendants().OfType<Button>()
                    .First(b => ReferenceEquals(b.Command, shell.IncreaseOutlineLineHeightCommand));
                plus.Command!.Execute(plus.CommandParameter);
                Assert.Equal(lineHeight + OutlinePreferences.LineHeightStep, shell.OutlinePreferences.LineHeight, 3);

                var h1 = shell.OutlineLevelSettings[0];
                var before = shell.OutlinePreferences.GetFontSize(1);
                var h1Plus = sizeRows.GetLogicalDescendants().OfType<Button>()
                    .First(b => ReferenceEquals(b.Command, h1.IncreaseFontSizeCommand));
                h1Plus.Command!.Execute(h1Plus.CommandParameter);
                Assert.Equal(before + 1, shell.OutlinePreferences.GetFontSize(1));
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    private static ToggleButton SideButton(OutlineSettingsView view, string text)
        => view.GetLogicalDescendants().OfType<ToggleButton>()
            .First(b => b.Content is TextBlock { Text: var t } && t == text);

    private static ItemsControl FindItemsControl(OutlineSettingsView view, int index)
        => view.GetLogicalDescendants().OfType<ItemsControl>().ElementAt(index);

    private static StackPanel BuildRows(ItemsControl items, IEnumerable<OutlineLevelSetting> settings)
    {
        var panel = new StackPanel();
        foreach (var setting in settings)
        {
            var row = Assert.IsAssignableFrom<Control>(items.ItemTemplate!.Build(setting));
            row.DataContext = setting;
            panel.Children.Add(row);
        }

        return panel;
    }
}
