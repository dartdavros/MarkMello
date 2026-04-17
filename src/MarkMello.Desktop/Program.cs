using Avalonia;
using MarkMello.Application.Abstractions;
using MarkMello.Domain.Diagnostics;
using MarkMello.Infrastructure;
using MarkMello.Infrastructure.Diagnostics;
using MarkMello.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Desktop;

internal static class Program
{
    /// <summary>
    /// Точка входа. Порядок шагов важен:
    /// 1) создаём <see cref="StopwatchStartupMetrics"/> первым делом, чтобы засечь весь startup;
    /// 2) собираем DI контейнер;
    /// 3) передаём провайдер в <see cref="App"/> до запуска Avalonia builder'а;
    /// 4) стартуем classic desktop lifetime.
    /// Никаких editor-зависимостей в этом графе не должно быть (constitution §4).
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var metrics = new StopwatchStartupMetrics();
        metrics.Mark(StartupStage.AppBootstrap);

        var services = ConfigureServices(metrics);
        App.RegisterServices(services);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Используется Avalonia design-time previewer'ом — должно быть public и static.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static ServiceProvider ConfigureServices(IStartupMetrics metrics)
    {
        var collection = new ServiceCollection();
        collection.AddInfrastructure(metrics);
        collection.AddPresentation();
        return collection.BuildServiceProvider();
    }
}
