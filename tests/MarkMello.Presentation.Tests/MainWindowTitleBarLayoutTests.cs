using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MarkMello.Application.Abstractions;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MainWindowTitleBarLayoutTests(AvaloniaHeadlessFixture fixture)
{
    [Theory]
    [InlineData(640, false)]
    [InlineData(640, true)]
    [InlineData(1280, false)]
    [InlineData(1280, true)]
    public Task TitleStaysAtTheWindowCenterAndDoesNotOverlapButtons(double width, bool longTitle)
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(
                withHeadings: true, outlineVisible: false);
            var window = new MainWindow(shell, StartupSmokeTestOptions.Disabled,
                new InMemorySettingsStore(), new RecordingStartupMetrics())
            {
                Width = width,
                Height = 600
            };
            window.Show();
            try
            {
                await Task.Yield();
                var titleText = window.FindControl<TextBlock>("TitleBarTitleText")!;
                Assert.Equal(shell.FileName, titleText.Text);
                titleText.Text = longTitle
                    ? new string('W', 200) + ".md"
                    : "sample.md";
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var title = window.FindControl<DockPanel>("TitleBarTitle")!;
                var menu = window.FindControl<Button>("AppMenuTriggerButton")!;
                var buttons = window.FindControl<StackPanel>("TitleBarCaptionButtons")!;
                var titleOrigin = title.TranslatePoint(default, window)
                    ?? throw new InvalidOperationException("Title is detached from the window.");
                var menuOrigin = menu.TranslatePoint(default, window)
                    ?? throw new InvalidOperationException("Menu is detached from the window.");
                var buttonsOrigin = buttons.TranslatePoint(default, window)
                    ?? throw new InvalidOperationException("Caption buttons are detached from the window.");

                // Layout rounding can put an odd-width title half a pixel off the center.
                var centerOffset = titleOrigin.X + title.Bounds.Width / 2 - window.ClientSize.Width / 2;
                Assert.InRange(Math.Abs(centerOffset), 0, 0.5);
                Assert.True(titleOrigin.X >= menuOrigin.X + menu.Bounds.Width);
                Assert.True(titleOrigin.X + title.Bounds.Width <= buttonsOrigin.X);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
