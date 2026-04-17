using MarkMello.Application.Abstractions;
using MarkMello.Infrastructure.Documents;
using MarkMello.Infrastructure.Platform;
using MarkMello.Infrastructure.Settings;
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
        services.AddSingleton<IDocumentLoader, FileDocumentLoader>();
        services.AddSingleton<ISettingsStore, InMemorySettingsStore>();
        services.AddSingleton<IPlatformServices, DefaultPlatformServices>();
        services.AddSingleton<ICommandLineActivation>(_ => new CommandLineActivation(commandLineArgs));

        return services;
    }
}
