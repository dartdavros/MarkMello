using System.Text.Json;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Infrastructure.Serialization;

namespace MarkMello.Infrastructure.Settings;

/// <summary>
/// JSON-backed settings store for M4. Reads and writes a tiny settings file
/// from the platform config directory and falls back to safe defaults if the
/// file is missing or corrupted.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly Lock _gate = new();
    private readonly string _settingsFilePath;

    private bool _isLoaded;
    private ReadingPreferences _preferences = ReadingPreferences.Default;
    private ThemeMode _theme = ThemeMode.System;
    private AppLanguage _language = AppLanguage.System;
    private bool _alwaysOpenDocumentsInEditMode;
    private WindowPlacement? _windowPlacement;

    public JsonSettingsStore(string? settingsRootDirectory = null)
    {
        var rootDirectory = ResolveSettingsRootDirectory(settingsRootDirectory);
        _settingsFilePath = Path.Combine(rootDirectory, "settings.json");
    }

    public ValueTask<ReadingPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            return ValueTask.FromResult(_preferences);
        }
    }

    public ValueTask SavePreferencesAsync(ReadingPreferences preferences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            _preferences = ReadingPreferences.Normalize(preferences);
            PersistCore();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<ThemeMode> LoadThemeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            return ValueTask.FromResult(_theme);
        }
    }

    public ValueTask SaveThemeAsync(ThemeMode theme, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            _theme = NormalizeTheme(theme);
            PersistCore();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<AppLanguage> LoadLanguageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            return ValueTask.FromResult(_language);
        }
    }

    public ValueTask SaveLanguageAsync(AppLanguage language, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            _language = NormalizeLanguage(language);
            PersistCore();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> LoadAlwaysOpenDocumentsInEditModeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            return ValueTask.FromResult(_alwaysOpenDocumentsInEditMode);
        }
    }

    public ValueTask SaveAlwaysOpenDocumentsInEditModeAsync(
        bool value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            _alwaysOpenDocumentsInEditMode = value;
            PersistCore();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<WindowPlacement?> LoadWindowPlacementAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            return ValueTask.FromResult(_windowPlacement);
        }
    }

    public ValueTask SaveWindowPlacementAsync(
        WindowPlacement? placement,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureLoadedCore();
            _windowPlacement = WindowPlacement.Normalize(placement);
            PersistCore();
        }

        return ValueTask.CompletedTask;
    }

    private void EnsureLoadedCore()
    {
        if (_isLoaded)
        {
            return;
        }

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var fileModel = JsonSerializer.Deserialize(
                        json,
                        MarkMelloJsonSerializerContext.Default.SettingsFileModel);
                    if (fileModel is not null)
                    {
                        _theme = NormalizeTheme(fileModel.Theme);
                        _preferences = ReadingPreferences.Normalize(fileModel.Preferences);
                        _language = NormalizeLanguage(fileModel.Language);
                        _alwaysOpenDocumentsInEditMode = fileModel.AlwaysOpenDocumentsInEditMode;
                        _windowPlacement = WindowPlacement.Normalize(fileModel.WindowPlacement);
                    }
                }
            }
        }
        catch
        {
            _theme = ThemeMode.System;
            _preferences = ReadingPreferences.Default;
            _language = AppLanguage.System;
            _alwaysOpenDocumentsInEditMode = false;
            _windowPlacement = null;
        }
        finally
        {
            _isLoaded = true;
        }
    }

    private void PersistCore()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            var tempFilePath = _settingsFilePath + ".tmp";
            var fileModel = new SettingsFileModel(
                _theme,
                _preferences,
                _language,
                _alwaysOpenDocumentsInEditMode,
                _windowPlacement);
            var json = JsonSerializer.Serialize(
                fileModel,
                MarkMelloJsonSerializerContext.Default.SettingsFileModel);

            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _settingsFilePath, overwrite: true);
        }
        catch
        {
            // Persistence is best-effort: reading must keep working even if the
            // config directory is unavailable or unwritable on this machine.
        }
    }

    private static ThemeMode NormalizeTheme(ThemeMode theme)
        => theme switch
        {
            ThemeMode.Light => ThemeMode.Light,
            ThemeMode.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };

    private static AppLanguage NormalizeLanguage(AppLanguage language)
        => language switch
        {
            AppLanguage.English => AppLanguage.English,
            AppLanguage.Russian => AppLanguage.Russian,
            _ => AppLanguage.System
        };

    private static string ResolveSettingsRootDirectory(string? settingsRootDirectory)
    {
        if (!string.IsNullOrWhiteSpace(settingsRootDirectory))
        {
            return Path.GetFullPath(settingsRootDirectory);
        }

        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appDataDirectory))
        {
            return Path.Combine(AppContext.BaseDirectory, "MarkMello");
        }

        return Path.Combine(appDataDirectory, "MarkMello");
    }
}
