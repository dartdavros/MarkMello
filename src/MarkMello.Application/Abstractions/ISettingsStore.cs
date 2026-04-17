using MarkMello.Domain;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Хранилище пользовательских настроек. В M0 — in-memory заглушка.
/// JSON-persistence в %APPDATA%/MarkMello (или платформенный аналог) приходит в M4.
/// </summary>
public interface ISettingsStore
{
    ValueTask<ReadingPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default);
    ValueTask SavePreferencesAsync(ReadingPreferences preferences, CancellationToken cancellationToken = default);

    ValueTask<ThemeMode> LoadThemeAsync(CancellationToken cancellationToken = default);
    ValueTask SaveThemeAsync(ThemeMode theme, CancellationToken cancellationToken = default);
}
