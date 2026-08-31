using MarkMello.Domain;
using MarkMello.Infrastructure.Settings;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

public sealed class WindowBorderTests
{
    [Theory]
    // Auto draws the outline only where the app replaces the system chrome.
    [InlineData(WindowBorderMode.Auto, true, false, true)]
    [InlineData(WindowBorderMode.Auto, false, false, false)]
    // On and Off override the platform either way.
    [InlineData(WindowBorderMode.On, false, false, true)]
    [InlineData(WindowBorderMode.Off, true, false, false)]
    // A maximized window sits against the screen edges: no outline in any mode.
    [InlineData(WindowBorderMode.Auto, true, true, false)]
    [InlineData(WindowBorderMode.On, true, true, false)]
    [InlineData(WindowBorderMode.On, false, true, false)]
    public void ShouldDrawWindowBorderFollowsModePlatformAndWindowState(
        WindowBorderMode mode,
        bool isWindows,
        bool isMaximized,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldDrawWindowBorder(mode, isWindows, isMaximized));
    }

    [Fact]
    public async Task WindowBorderModeRoundTripsThroughTheSettingsFile()
    {
        var directory = CreateSettingsDirectory();
        try
        {
            var store = new JsonSettingsStore(directory);
            Assert.Equal(WindowBorderMode.Auto, await store.LoadWindowBorderModeAsync());

            await store.SaveWindowBorderModeAsync(WindowBorderMode.Off);

            var reopened = new JsonSettingsStore(directory);
            Assert.Equal(WindowBorderMode.Off, await reopened.LoadWindowBorderModeAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SettingsFileWrittenBeforeTheOptionExistedLoadsAsAuto()
    {
        var directory = CreateSettingsDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "settings.json"),
                """{"theme":"Dark","language":"English"}""");

            var store = new JsonSettingsStore(directory);

            Assert.Equal(WindowBorderMode.Auto, await store.LoadWindowBorderModeAsync());
            Assert.Equal(ThemeMode.Dark, await store.LoadThemeAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UnknownWindowBorderValueFallsBackToAuto()
    {
        var directory = CreateSettingsDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "settings.json"),
                """{"windowBorder":"Sideways"}""");

            var store = new JsonSettingsStore(directory);

            Assert.Equal(WindowBorderMode.Auto, await store.LoadWindowBorderModeAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateSettingsDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MarkMello.Tests",
            "window-border",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
