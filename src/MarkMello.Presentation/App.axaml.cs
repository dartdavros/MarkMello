using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MarkMello.Application.Abstractions;
using MarkMello.Domain.Diagnostics;
using MarkMello.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Presentation;

public partial class App : global::Avalonia.Application
{
    /// <summary>
    /// Сервис-провайдер, передаваемый из Program.Main до создания AppBuilder.
    /// Statiс — обусловлено тем, что Avalonia сама создаёт инстанс App.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = default!;

    public static void RegisterServices(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var metrics = Services.GetRequiredService<IStartupMetrics>();
            var window = Services.GetRequiredService<MainWindow>();

            // Stage 2 фиксируем после первого Opened — это момент, когда окно реально показалось пользователю,
            // а не просто инстанцировано.
            window.Opened += (_, _) => metrics.Mark(StartupStage.FirstWindow);

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
