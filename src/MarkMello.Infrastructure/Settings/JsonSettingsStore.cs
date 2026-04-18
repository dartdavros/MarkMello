using System.Text.Json;
using System.Text.Json.Serialization;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Infrastructure.Settings;

/// <summary>
/// JSON-backed settings store for M4. Reads and writes a tiny settings file
/// from the platform config directory and falls back to safe defaults if the
/// file is missing or corrupted.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly Lock _gate = new();
    private readonly string _settingsFilePath;

    private bool _isLoaded;
    private ReadingPreferences _preferences = ReadingPreferences.Default;
    private ThemeMode _theme = ThemeMode.System;

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
                    var fileModel = JsonSerializer.Deserialize<SettingsFileModel>(json, JsonOptions);
                    if (fileModel is not null)
                    {
                        _theme = NormalizeTheme(fileModel.Theme);
                        _preferences = ReadingPreferences.Normalize(fileModel.Preferences);
                    }
                }
            }
        }
        catch
        {
            _theme = ThemeMode.System;
            _preferences = ReadingPreferences.Default;
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
            var fileModel = new SettingsFileModel(_theme, _preferences);
            var json = JsonSerializer.Serialize(fileModel, JsonOptions);

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

    private static JsonSerializerOptions CreateJsonOptions()
        => new()
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    private sealed record SettingsFileModel(
        ThemeMode Theme,
        ReadingPreferences Preferences);
}
