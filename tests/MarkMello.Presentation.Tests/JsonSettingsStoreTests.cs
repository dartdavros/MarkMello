using MarkMello.Domain;
using MarkMello.Infrastructure.Settings;

namespace MarkMello.Presentation.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripsSettings()
    {
        var rootDirectory = CreateTempDirectory();
        try
        {
            var store = new JsonSettingsStore(rootDirectory);
            var expectedPreferences = new ReadingPreferences(
                FontFamilyMode.Mono,
                19,
                1.8,
                ReadingPreferences.WideContentWidth,
                DocumentMinimapMode.On);

            await store.SavePreferencesAsync(expectedPreferences);
            await store.SaveThemeAsync(ThemeMode.Dark);
            await store.SaveLanguageAsync(AppLanguage.Russian);
            await store.SaveAlwaysOpenDocumentsInEditModeAsync(true);
            await store.SaveWindowPlacementAsync(new WindowPlacement(120, 80, 900, 700, IsMaximized: true));

            var reloadedStore = new JsonSettingsStore(rootDirectory);
            var actualPreferences = await reloadedStore.LoadPreferencesAsync();
            var actualTheme = await reloadedStore.LoadThemeAsync();
            var actualLanguage = await reloadedStore.LoadLanguageAsync();
            var actualAlwaysOpenDocumentsInEditMode = await reloadedStore.LoadAlwaysOpenDocumentsInEditModeAsync();
            var actualWindowPlacement = await reloadedStore.LoadWindowPlacementAsync();

            Assert.Equal(expectedPreferences, actualPreferences);
            Assert.Equal(ThemeMode.Dark, actualTheme);
            Assert.Equal(AppLanguage.Russian, actualLanguage);
            Assert.True(actualAlwaysOpenDocumentsInEditMode);
            Assert.Equal(new WindowPlacement(120, 80, 900, 700, IsMaximized: true), actualWindowPlacement);
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
            var language = await store.LoadLanguageAsync();
            var alwaysOpenDocumentsInEditMode = await store.LoadAlwaysOpenDocumentsInEditModeAsync();
            var windowPlacement = await store.LoadWindowPlacementAsync();

            Assert.Equal(ReadingPreferences.Default, preferences);
            Assert.Equal(ThemeMode.System, theme);
            Assert.Equal(DocumentMinimapMode.Auto, preferences.DocumentMinimapMode);
            Assert.Equal(AppLanguage.System, language);
            Assert.False(alwaysOpenDocumentsInEditMode);
            Assert.Null(windowPlacement);
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
            "contentWidth": 1700,
            "documentMinimapMode": "Off"
          }
        }
        """;

        try
        {
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "settings.json"), json);

            var store = new JsonSettingsStore(rootDirectory);
            var preferences = await store.LoadPreferencesAsync();
            var theme = await store.LoadThemeAsync();
            var language = await store.LoadLanguageAsync();
            var alwaysOpenDocumentsInEditMode = await store.LoadAlwaysOpenDocumentsInEditModeAsync();
            var windowPlacement = await store.LoadWindowPlacementAsync();

            Assert.Equal(ThemeMode.Light, theme);
            Assert.Equal(FontFamilyMode.Mono, preferences.FontFamily);
            Assert.Equal(ReadingPreferences.MinFontSize, preferences.FontSize);
            Assert.Equal(ReadingPreferences.MaxLineHeight, preferences.LineHeight);
            Assert.Equal(ReadingPreferences.MaxContentWidth, preferences.ContentWidth);
            Assert.Equal(DocumentMinimapMode.Off, preferences.DocumentMinimapMode);
            Assert.Equal(AppLanguage.System, language);
            Assert.False(alwaysOpenDocumentsInEditMode);
            Assert.Null(windowPlacement);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }


    [Fact]
    public async Task LoadUsesAutoMinimapModeWhenLegacySettingsHaveNoMinimapMode()
    {
        var rootDirectory = CreateTempDirectory();
        const string json = """
        {
          "theme": "Light",
          "preferences": {
            "fontFamily": "Serif",
            "fontSize": 18,
            "lineHeight": 1.7,
            "contentWidth": 820
          }
        }
        """;

        try
        {
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "settings.json"), json);

            var store = new JsonSettingsStore(rootDirectory);
            var preferences = await store.LoadPreferencesAsync();

            Assert.Equal(DocumentMinimapMode.Auto, preferences.DocumentMinimapMode);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadFallsBackToNullWindowPlacementWhenPlacementIsInvalid()
    {
        var rootDirectory = CreateTempDirectory();
        const string json = """
        {
          "theme": "Light",
          "preferences": {
            "fontFamily": "Serif",
            "fontSize": 18,
            "lineHeight": 1.7,
            "contentWidth": 720
          },
          "language": "English",
          "windowPlacement": {
            "x": 100,
            "y": 100,
            "width": 0,
            "height": 640,
            "isMaximized": false
          }
        }
        """;

        try
        {
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "settings.json"), json);

            var store = new JsonSettingsStore(rootDirectory);
            var windowPlacement = await store.LoadWindowPlacementAsync();

            Assert.Null(windowPlacement);
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
