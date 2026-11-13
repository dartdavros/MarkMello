using MarkMello.Infrastructure.Settings;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

public sealed class AutomaticUpdatePreferencesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MarkMello-updates-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void NewAndLegacyProfilesKeepAutomaticChecksEnabledByDefault()
    {
        Assert.True(new AutomaticUpdatePreferences(_root).Enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChoiceSurvivesAnewProcessAndDoesNotTouchOtherSettings(bool enabled)
    {
        Directory.CreateDirectory(_root);
        var settingsPath = Path.Combine(_root, "settings.json");
        File.WriteAllText(settingsPath, "existing application settings");
        var preferences = new AutomaticUpdatePreferences(_root);

        Assert.True(preferences.SetEnabled(enabled));

        Assert.Equal(enabled, new AutomaticUpdatePreferences(_root).Enabled);
        Assert.Equal("existing application settings", File.ReadAllText(settingsPath));
    }

    [Fact]
    public void SettingsWindowsShareTheChoice()
    {
        var preferences = new AutomaticUpdatePreferences(_root);
        var localization = new LocalizationService();
        using var first = new AutomaticUpdateSettingsViewModel(preferences, localization);
        using var second = new AutomaticUpdateSettingsViewModel(preferences, localization);
        var notified = false;
        second.PropertyChanged += (_, args) => notified |= args.PropertyName == nameof(second.IsDisabled);

        first.IsDisabled = true;

        Assert.True(notified);
        Assert.True(second.IsDisabled);
        Assert.False(second.IsEnabled);
        second.IsEnabled = true;
        Assert.True(first.IsEnabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
