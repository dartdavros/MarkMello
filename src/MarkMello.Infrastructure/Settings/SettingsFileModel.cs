using MarkMello.Domain;

namespace MarkMello.Infrastructure.Settings;

internal sealed record SettingsFileModel(
    ThemeMode Theme,
    ReadingPreferences Preferences,
    AppLanguage Language,
    WindowPlacement? WindowPlacement,
    WindowBorderMode WindowBorder = WindowBorderMode.Auto,
    double? SidebarWidth = null);
