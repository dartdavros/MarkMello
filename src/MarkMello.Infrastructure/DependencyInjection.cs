using System.Globalization;
using MarkMello.Application.Abstractions;
using MarkMello.Infrastructure.Diagrams;
using MarkMello.Infrastructure.Documents;
using MarkMello.Infrastructure.Images;
using MarkMello.Infrastructure.Markdown;
using MarkMello.Infrastructure.Pdf;
using MarkMello.Infrastructure.Platform;
using MarkMello.Infrastructure.Settings;
using MarkMello.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует инфраструктурные сервисы. <paramref name="metrics"/> и <paramref name="commandLineArgs"/>
    /// передаются извне, потому что создаются в Program.Main до построения контейнера.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IStartupMetrics metrics,
        string[] commandLineArgs)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(commandLineArgs);

        services.AddSingleton(metrics);
        services.AddSingleton(CreateStartupSmokeTestOptions(commandLineArgs));
        services.AddSingleton<IDocumentLoader, FileDocumentLoader>();
        services.AddSingleton<IDocumentSaver, FileDocumentSaver>();
        services.AddSingleton<IPdfExporter, SkiaPdfExporter>();
        services.AddSingleton<IMarkdownDocumentRenderer, MarkdigMarkdownDocumentRenderer>();
        services.AddSingleton<IDiagramRenderer, MermaidDiagramRenderer>();
        services.AddSingleton<IImageSourceResolver, DefaultImageSourceResolver>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IPlatformServices, DefaultPlatformServices>();
        services.AddSingleton(_ => new CommandLineActivation(commandLineArgs));
        services.AddSingleton<ICommandLineActivation>(sp => sp.GetRequiredService<CommandLineActivation>());
        services.AddSingleton<IFileActivationPublisher>(sp => sp.GetRequiredService<CommandLineActivation>());
        services.AddSingleton(static _ =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MarkMello/updates");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
            return client;
        });
        services.AddSingleton<IUpdateService, GitHubReleaseUpdateService>();

        return services;
    }

    private static StartupSmokeTestOptions CreateStartupSmokeTestOptions(string[] commandLineArgs)
    {
        var delayMilliseconds = TryGetSmokeExitDelayFromArguments(commandLineArgs)
            ?? TryGetSmokeExitDelayFromEnvironment();

        return delayMilliseconds is null
            ? StartupSmokeTestOptions.Disabled
            : new StartupSmokeTestOptions(
                IsEnabled: true,
                ExitAfterOpenDelay: TimeSpan.FromMilliseconds(delayMilliseconds.Value));
    }

    private static int? TryGetSmokeExitDelayFromArguments(string[] commandLineArgs)
    {
        for (var index = 0; index < commandLineArgs.Length; index++)
        {
            var argument = commandLineArgs[index];

            if (string.Equals(argument, "--smoke-exit-after-open", StringComparison.Ordinal))
            {
                return 1500;
            }

            if (argument.StartsWith("--smoke-exit-after-open-ms=", StringComparison.Ordinal))
            {
                var value = argument["--smoke-exit-after-open-ms=".Length..];
                return TryParsePositiveMilliseconds(value);
            }

            if (string.Equals(argument, "--smoke-exit-after-open-ms", StringComparison.Ordinal)
                && index + 1 < commandLineArgs.Length)
            {
                return TryParsePositiveMilliseconds(commandLineArgs[index + 1]);
            }
        }

        return null;
    }

    private static int? TryGetSmokeExitDelayFromEnvironment()
    {
        var milliseconds = Environment.GetEnvironmentVariable("MARKMELLO_SMOKE_EXIT_AFTER_OPEN_MS");
        if (!string.IsNullOrWhiteSpace(milliseconds))
        {
            return TryParsePositiveMilliseconds(milliseconds);
        }

        var enabled = Environment.GetEnvironmentVariable("MARKMELLO_SMOKE_EXIT_AFTER_OPEN");
        if (string.Equals(enabled, "1", StringComparison.Ordinal)
            || string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return 1500;
        }

        return null;
    }

    private static int? TryParsePositiveMilliseconds(string value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)
            && milliseconds > 0
            ? milliseconds
            : null;
    }
}

