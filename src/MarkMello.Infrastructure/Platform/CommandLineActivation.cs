using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Infrastructure.Platform;

/// <summary>
/// Двойственный источник «открой файл» сигналов:
///
/// <list type="bullet">
///   <item>На Windows и Linux путь приходит через <c>argv</c> — резолвится
///   один раз в конструкторе.</item>
///   <item>На macOS Finder вместо <c>argv</c> присылает Apple Event
///   <c>odoc</c>. Avalonia 12 пробрасывает его через <c>IActivatableLifetime</c>;
///   платформенный мост в <c>App.axaml.cs</c> вызывает
///   <see cref="NotifyFileActivated"/> сюда.</item>
/// </list>
///
/// До того как view-model впервые запросит <see cref="GetActivationFilePath"/>,
/// поступивший runtime-сигнал кэшируется как стартовый путь — это нужно,
/// потому что на macOS AppleEvent приходит после <c>OnFrameworkInitializationCompleted</c>,
/// но до того как окно дойдёт до своей инициализации. После того как стартовый
/// путь забран, последующие сигналы превращаются в событие
/// <see cref="FileActivated"/>, и обрабатываются уже работающим приложением.
/// </summary>
public sealed class CommandLineActivation : ICommandLineActivation, IFileActivationPublisher
{
    private readonly Lock _gate = new();
    private readonly string? _argvActivationPath;
    private readonly string? _argvActivationFolderPath;
    private string? _cachedRuntimePath;
    private bool _initialConsumed;

    public CommandLineActivation(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        _argvActivationPath = ResolveFromArgs(args);
        _argvActivationFolderPath = ResolveFolderFromArgs(args);
    }

    public event EventHandler<FileActivationEventArgs>? FileActivated;

    public string? GetActivationFilePath()
    {
        lock (_gate)
        {
            _initialConsumed = true;
            return _argvActivationPath ?? _cachedRuntimePath;
        }
    }

    /// <summary>
    /// Каталог из аргументов. В отличие от файла, он не кэшируется как runtime-сигнал:
    /// платформы присылают через события только файлы.
    /// </summary>
    public string? GetActivationFolderPath() => _argvActivationFolderPath;

    public void NotifyFileActivated(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalised = TryNormalisePath(path);
        if (normalised is null)
        {
            return;
        }

        bool shouldRaiseEvent;
        lock (_gate)
        {
            if (!_initialConsumed)
            {
                // The view-model has not yet asked for the activation
                // path. macOS cold-start hits this branch — the AppleEvent
                // arrives before the main window finishes initializing.
                // Cache it so the upcoming GetActivationFilePath() call
                // returns this path as if it had been on argv.
                _cachedRuntimePath = normalised;
                return;
            }

            shouldRaiseEvent = true;
        }

        if (shouldRaiseEvent)
        {
            FileActivated?.Invoke(this, new FileActivationEventArgs(normalised));
        }
    }

    private static string? ResolveFromArgs(string[] args)
    {
        foreach (var arg in args)
        {
            var resolved = TryNormalisePath(arg);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? ResolveFolderFromArgs(string[] args)
    {
        foreach (var arg in args)
        {
            // Диагностические ключи smoke-режима каталогами не являются.
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
            {
                continue;
            }

            if (Directory.Exists(arg))
            {
                return Path.GetFullPath(arg);
            }
        }

        return null;
    }

    private static string? TryNormalisePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!File.Exists(value))
        {
            return null;
        }

        if (!SupportedDocumentTypes.IsSupportedPath(value))
        {
            return null;
        }

        return Path.GetFullPath(value);
    }
}
