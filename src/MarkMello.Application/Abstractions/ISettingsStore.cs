using MarkMello.Domain;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Хранилище пользовательских настроек. В M4 реализуется как маленький JSON-файл
/// в платформенном config-каталоге с безопасным fallback на defaults.
/// </summary>
public interface ISettingsStore
{
    ValueTask<ReadingPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default);
    ValueTask SavePreferencesAsync(ReadingPreferences preferences, CancellationToken cancellationToken = default);

    ValueTask<ThemeMode> LoadThemeAsync(CancellationToken cancellationToken = default);
    ValueTask SaveThemeAsync(ThemeMode theme, CancellationToken cancellationToken = default);
}
