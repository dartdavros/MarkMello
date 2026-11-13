namespace MarkMello.Infrastructure.Settings;

/// <summary>Runs before Avalonia so Setup can read/write the original user's preference without a window.</summary>
public static class InstallerUpdatePreferencesCommand
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(args);
        exitCode = 0;
        if (args.Length != 1)
        {
            return false;
        }
        if (args[0] == "--get-automatic-updates")
        {
            exitCode = new AutomaticUpdatePreferences().Enabled ? 0 : 10;
            return true;
        }
        bool enabled;
        switch (args[0])
        {
            case "--automatic-updates=on": enabled = true; break;
            case "--automatic-updates=off": enabled = false; break;
            default: return false;
        }
        exitCode = new AutomaticUpdatePreferences().SetEnabled(enabled) ? 0 : 1;
        return true;
    }
}
