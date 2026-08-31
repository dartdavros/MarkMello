using MarkMello.Domain;
using MarkMello.Domain.Workspace;

namespace MarkMello.Infrastructure.Settings;

internal sealed record SettingsFileModel(
    ThemeMode Theme,
    ReadingPreferences Preferences,
    AppLanguage Language,
    WindowPlacement? WindowPlacement,
    WindowBorderMode WindowBorder = WindowBorderMode.Auto,
    double? SidebarWidth = null,
    WorkspaceSessionState? Session = null,
    bool AlwaysOpenDocumentsInEditMode = false);
