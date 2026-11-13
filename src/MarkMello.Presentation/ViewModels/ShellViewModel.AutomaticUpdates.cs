namespace MarkMello.Presentation.ViewModels;

public partial class ShellViewModel
{
    private AutomaticUpdateSettingsViewModel? _automaticUpdateSettings;

    public AutomaticUpdateSettingsViewModel? AutomaticUpdateSettings
        => _deferredUpdateCheck?.Preferences is { } preferences
            ? _automaticUpdateSettings ??= new(preferences, _localization)
            : null;

    internal void ReleaseAutomaticUpdateSettings()
    {
        _automaticUpdateSettings?.Dispose();
        _automaticUpdateSettings = null;
    }
}
