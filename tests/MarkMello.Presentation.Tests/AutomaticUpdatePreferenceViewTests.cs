using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Infrastructure.Settings;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class AutomaticUpdatePreferenceViewTests(AvaloniaHeadlessFixture fixture)
{
    [Fact]
    public Task SelectedChoiceCannotBeUncheckedWithoutSelectingTheOtherChoice()
    {
        return fixture.RunAsync(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "MarkMello-updates-" + Guid.NewGuid().ToString("N"));
            using var viewModel = new AutomaticUpdateSettingsViewModel(
                new AutomaticUpdatePreferences(root), new LocalizationService());
            var view = new AutomaticUpdatePreferenceView { DataContext = viewModel };
            var window = new Window { Content = view, Width = 700, Height = 180 };
            try
            {
                window.Show();
                window.UpdateLayout();
                var choices = view.GetVisualDescendants().OfType<ToggleButton>().ToArray();
                Assert.Equal(2, choices.Length);
                Assert.True(choices[0].IsChecked);

                Click(choices[0]);
                Dispatcher.UIThread.RunJobs();
                Assert.True(choices[0].IsChecked);
                Assert.False(choices[1].IsChecked);

                Click(choices[1]);
                Dispatcher.UIThread.RunJobs();
                Assert.False(choices[0].IsChecked);
                Assert.True(choices[1].IsChecked);
                Assert.False(new AutomaticUpdatePreferences(root).Enabled);

                Click(choices[1]);
                Dispatcher.UIThread.RunJobs();
                Assert.True(choices[1].IsChecked);
                Assert.False(choices[0].IsChecked);

                void Click(ToggleButton choice)
                {
                    choice.Focus();
                    window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                }
            }
            finally
            {
                window.Close();
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            return Task.CompletedTask;
        });
    }
}
