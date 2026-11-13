using System.Text.Json;
using System.Text.Json.Serialization;
using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Settings;

/// <summary>One per-user preference used by the application and the installer command.</summary>
public sealed class AutomaticUpdatePreferences : IAutomaticUpdatePreferences
{
    private readonly object _gate = new();
    private readonly string _path;
    private bool? _enabled;

    public AutomaticUpdatePreferences(string? settingsRootDirectory = null)
    {
        var root = settingsRootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkMello");
        _path = Path.Combine(root, "automatic-updates.json");
    }

    public event EventHandler? Changed;

    public bool Enabled
    {
        get { lock (_gate) { return LoadCore(); } }
    }

    public bool SetEnabled(bool enabled)
    {
        bool changed;
        bool saved;
        lock (_gate)
        {
            changed = LoadCore() != enabled;
            _enabled = enabled;
            saved = PersistCore(enabled);
        }
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return saved;
    }

    private bool LoadCore()
    {
        if (_enabled is { } cached)
        {
            return cached;
        }
        try
        {
            if (File.Exists(_path))
            {
                var record = JsonSerializer.Deserialize(
                    File.ReadAllText(_path), AutomaticUpdateJsonContext.Default.AutomaticUpdatePreference);
                _enabled = record?.Enabled ?? true;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Preserve the existing enabled default for absent or unreadable settings.
        }
        return _enabled ??= true;
    }

    private bool PersistCore(bool enabled)
    {
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var json = JsonSerializer.Serialize(
                new AutomaticUpdatePreference(enabled), AutomaticUpdateJsonContext.Default.AutomaticUpdatePreference);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }
}

internal sealed record AutomaticUpdatePreference(bool Enabled = true);

[JsonSerializable(typeof(AutomaticUpdatePreference))]
internal sealed partial class AutomaticUpdateJsonContext : JsonSerializerContext;
