using MarkMello.Domain;
using MarkMello.Infrastructure.Settings;

namespace MarkMello.Presentation.Tests;

public sealed class JsonSettingsStoreOutlineTests
{
    [Fact]
    public async Task OutlinePreferencesRoundTrip()
    {
        var root = Directory.CreateTempSubdirectory("mm-outline-").FullName;
        try
        {
            var expected = OutlinePreferences.Default with
            {
                IsVisible = true,
                Side = OutlinePanelSide.Right,
                MaxLevel = 4,
                LineHeight = 1.8,
                Width = 300,
                H2FontSize = 17
            };

            await new JsonSettingsStore(root).SaveOutlinePreferencesAsync(expected);
            var actual = await new JsonSettingsStore(root).LoadOutlinePreferencesAsync();

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SettingsFileWithoutOutlineLoadsDefaults()
    {
        var root = Directory.CreateTempSubdirectory("mm-outline-").FullName;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "settings.json"),
                """{ "theme": "Dark", "preferences": { "fontFamily": "Serif", "fontSize": 18, "lineHeight": 1.7, "contentWidth": 820 }, "language": "Russian" }""");

            var store = new JsonSettingsStore(root);

            Assert.Equal(OutlinePreferences.Default, await store.LoadOutlinePreferencesAsync());
            Assert.Equal(ThemeMode.Dark, await store.LoadThemeAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
