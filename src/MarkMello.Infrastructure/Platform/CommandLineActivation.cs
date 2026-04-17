using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Ищет первый аргумент командной строки, который существует на диске
/// и имеет известное расширение (.md/.markdown/.txt).
/// </summary>
public sealed class CommandLineActivation : ICommandLineActivation
{
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

            if (SupportedDocumentTypes.IsSupportedPath(arg))
            {
                return Path.GetFullPath(arg);
            }
        }

        return null;
    }
}
