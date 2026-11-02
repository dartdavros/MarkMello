using Avalonia;
using MarkMello.Application;
using MarkMello.Application.Abstractions;
using MarkMello.Domain.Diagnostics;
using MarkMello.Infrastructure;
using MarkMello.Infrastructure.Diagnostics;
using MarkMello.Infrastructure.Platform.SingleInstance;
using MarkMello.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Desktop;

internal static class Program
{
    /// <summary>
    /// Точка входа. Порядок шагов важен:
    /// 1) создаём <see cref="StopwatchStartupMetrics"/> первым делом, чтобы засечь весь startup;
    /// 2) повторный запуск пересылает файл первому экземпляру и сразу завершается;
    /// 3) собираем DI контейнер с args для command-line activation;
    /// 4) передаём провайдер в <see cref="App"/> до запуска Avalonia builder'а;
    /// 5) стартуем classic desktop lifetime.
    /// Никаких editor-зависимостей в этом графе не должно быть (constitution §4).
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var metrics = new StopwatchStartupMetrics();
        metrics.Mark(StartupStage.AppBootstrap);

        // Повторный запуск отдаёт файл уже открытому окну (там он откроется вкладкой)
        // и завершается до старта Avalonia — второго окна не появляется.
        using var instance = SingleInstanceGuard.Acquire(
            enabled: !Infrastructure.DependencyInjection.IsStartupSmokeTestRun(args));
        if (instance.TryForwardToPrimary(args))
        {
            return 0;
        }

        using var services = ConfigureServices(metrics, args, instance.CreateActivation());
        App.RegisterServices(services);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Используется Avalonia design-time previewer'ом — должно быть public и static.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static ServiceProvider ConfigureServices(
        IStartupMetrics metrics,
        string[] args,
        IInstanceActivation instanceActivation)
    {
        var collection = new ServiceCollection();
        collection.AddInfrastructure(metrics, args, instanceActivation);
        collection.AddApplication();
        collection.AddPresentation();
        return collection.BuildServiceProvider();
    }
}
