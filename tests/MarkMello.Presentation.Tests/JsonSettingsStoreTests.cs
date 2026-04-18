using MarkMello.Domain;
using MarkMello.Infrastructure.Settings;

namespace MarkMello.Presentation.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripsThemeAndReadingPreferences()
    {
        var rootDirectory = CreateTempDirectory();
        try
        {
            var store = new JsonSettingsStore(rootDirectory);
            var expectedPreferences = new ReadingPreferences(FontFamilyMode.Mono, 19, 1.8, 860);

            await store.SavePreferencesAsync(expectedPreferences);
            await store.SaveThemeAsync(ThemeMode.Dark);

            var reloadedStore = new JsonSettingsStore(rootDirectory);
            var actualPreferences = await reloadedStore.LoadPreferencesAsync();
            var actualTheme = await reloadedStore.LoadThemeAsync();

            Assert.Equal(expectedPreferences, actualPreferences);
            Assert.Equal(ThemeMode.Dark, actualTheme);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadFallsBackToDefaultsWhenSettingsFileIsCorrupted()
    {
        var rootDirectory = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "settings.json"), "{ invalid json");

            var store = new JsonSettingsStore(rootDirectory);

            var preferences = await store.LoadPreferencesAsync();
            var theme = await store.LoadThemeAsync();

            Assert.Equal(ReadingPreferences.Default, preferences);
            Assert.Equal(ThemeMode.System, theme);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadNormalizesOutOfRangePreferenceValues()
    {
        var rootDirectory = CreateTempDirectory();
        const string json = """
        {
          "theme": "Light",
          "preferences": {
            "fontFamily": "Mono",
            "fontSize": 4,
            "lineHeight": 9.0,
            "contentWidth": 1700
          }
        }
        """;

        try
        {
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "settings.json"), json);

            var store = new JsonSettingsStore(rootDirectory);
            var preferences = await store.LoadPreferencesAsync();
            var theme = await store.LoadThemeAsync();

            Assert.Equal(ThemeMode.Light, theme);
            Assert.Equal(FontFamilyMode.Mono, preferences.FontFamily);
            Assert.Equal(ReadingPreferences.MinFontSize, preferences.FontSize);
            Assert.Equal(ReadingPreferences.MaxLineHeight, preferences.LineHeight);
            Assert.Equal(ReadingPreferences.MaxContentWidth, preferences.ContentWidth);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
