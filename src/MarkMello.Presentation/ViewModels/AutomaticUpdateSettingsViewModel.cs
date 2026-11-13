using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Application.Abstractions;
using MarkMello.Presentation.Localization;

namespace MarkMello.Presentation.ViewModels;

public sealed class AutomaticUpdateSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IAutomaticUpdatePreferences _preferences;
    private readonly ILocalizationService _localization;

    public AutomaticUpdateSettingsViewModel(
        IAutomaticUpdatePreferences preferences, ILocalizationService localization)
    {
        _preferences = preferences;
        _localization = localization;
        _preferences.Changed += OnPreferenceChanged;
        _localization.PropertyChanged += OnLanguageChanged;
    }

    public bool IsEnabled
    {
        get => _preferences.Enabled;
        set
        {
            if (value)
            {
                _preferences.SetEnabled(true);
            }
        }
    }

    public bool IsDisabled
    {
        get => !_preferences.Enabled;
        set
        {
            if (value)
            {
                _preferences.SetEnabled(false);
            }
        }
    }

    public string Label => _localization["AutomaticUpdatesLabel"];
    public string Hint => _localization["AutomaticUpdatesHint"];
    public string OnLabel => _localization["AutomaticUpdatesOn"];
    public string OffLabel => _localization["AutomaticUpdatesOff"];

    private void OnPreferenceChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsDisabled));
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(OnLabel));
        OnPropertyChanged(nameof(OffLabel));
    }

    public void Dispose()
    {
        _preferences.Changed -= OnPreferenceChanged;
        _localization.PropertyChanged -= OnLanguageChanged;
    }
}
