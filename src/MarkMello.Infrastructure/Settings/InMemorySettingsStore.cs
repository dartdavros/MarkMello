using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Infrastructure.Settings;

/// <summary>
/// In-memory заглушка хранилища настроек. Не сохраняет ничего между запусками.
/// JSON-persistence в платформенный config-каталог приходит в M4.
/// </summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Lock _gate = new();
    private ReadingPreferences _preferences = ReadingPreferences.Default;
    private ThemeMode _theme = ThemeMode.System;

    public ValueTask<ReadingPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_preferences);
        }
    }

    public ValueTask SavePreferencesAsync(ReadingPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        lock (_gate)
        {
            _preferences = preferences;
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<ThemeMode> LoadThemeAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_theme);
        }
    }

    public ValueTask SaveThemeAsync(ThemeMode theme, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _theme = theme;
        }
        return ValueTask.CompletedTask;
    }
}
