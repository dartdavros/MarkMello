using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Ищет первый аргумент командной строки, который существует на диске
/// и имеет известное расширение (.md/.markdown/.txt).
/// </summary>
public sealed class CommandLineActivation : ICommandLineActivation
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt"
    };

    private readonly string[] _args;

    public CommandLineActivation(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        _args = args;
    }

    public string? GetActivationFilePath()
    {
        foreach (var arg in _args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (!File.Exists(arg))
            {
                continue;
            }

            var extension = Path.GetExtension(arg);
            if (SupportedExtensions.Contains(extension))
            {
                return Path.GetFullPath(arg);
            }
        }

        return null;
    }
}
