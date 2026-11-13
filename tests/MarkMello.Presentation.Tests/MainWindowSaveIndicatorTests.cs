using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MarkMello.Application.Abstractions;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MainWindowSaveIndicatorTests(AvaloniaHeadlessFixture fixture)
{
    [Fact]
    public Task IndicatorChangesFromGreenToDarkYellowAndBackAfterSaving()
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(
                withHeadings: true, outlineVisible: false);
            var window = new MainWindow(shell, StartupSmokeTestOptions.Disabled,
                new InMemorySettingsStore(), new RecordingStartupMetrics())
            {
                RequestedThemeVariant = ThemeVariant.Light
            };
            window.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://MarkMello.Presentation/"))
            {
                Source = new Uri("avares://MarkMello.Presentation/Themes/Colors.axaml")
            });
            window.Show();
            try
            {
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
                var indicator = window.FindControl<Ellipse>("DocumentSaveIndicator")!;
                Assert.Equal(Color.Parse("#83B86F"), ColorOf(indicator));

                await shell.ToggleEditModeCommand.ExecuteAsync(null);
                shell.EditorSession!.SourceText += "\nchanged";
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Color.Parse("#956C23"), ColorOf(indicator));

                await shell.SaveCommand.ExecuteAsync(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Color.Parse("#83B86F"), ColorOf(indicator));
                Assert.Equal(shell.FileName, window.FindControl<TextBlock>("TitleBarTitleText")!.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Color ColorOf(Ellipse indicator)
        => Assert.IsAssignableFrom<ISolidColorBrush>(indicator.Fill).Color;
}
