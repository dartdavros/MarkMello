using MarkMello.Domain;

namespace MarkMello.Infrastructure.Settings;

internal sealed record SettingsFileModel(
    ThemeMode Theme,
    ReadingPreferences Preferences,
    AppLanguage Language,
    bool AlwaysOpenDocumentsInEditMode,
    WindowPlacement? WindowPlacement);
