using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MarkMello.Application.Abstractions;
using MarkMello.Domain.Diagnostics;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Presentation;

public partial class App : global::Avalonia.Application
{
    /// <summary>
    /// Сервис-провайдер, передаваемый из Program.Main до создания AppBuilder.
    /// Statiс — обусловлено тем, что Avalonia сама создаёт инстанс App.
    /// </summary>
    public static IServiceProvider? Services { get; private set; }

    public static void RegisterServices(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var localization = Services?.GetService<ILocalizationService>() ?? new LocalizationService();
        Resources["Localization"] = localization;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Services is null)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Приложение живёт, пока открыто хотя бы одно окно: вторая папка получает своё.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnLastWindowClose;

            var metrics = Services.GetRequiredService<IStartupMetrics>();
            var window = Services.GetRequiredService<MainWindow>();

            // Stage 2 фиксируем после первого Opened — это момент, когда окно реально показалось пользователю,
            // а не просто инстанцировано.
            window.Opened += (_, _) => metrics.Mark(StartupStage.FirstWindow);

            desktop.MainWindow = window;
        }

        WireFileActivationFromAvalonia();

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Bridges Avalonia's <c>IActivatableLifetime.Activated</c> into the
    /// app's <see cref="IFileActivationPublisher"/>. On macOS Finder
    /// sends an <c>odoc</c> Apple Event instead of populating <c>argv</c>,
    /// both at cold-start and while the app is already running — both
    /// paths surface here as <c>ActivationKind.File</c>. Windows and
    /// Linux still receive their files through <c>argv</c>, so this
    /// hook is a no-op there.
    /// </summary>
    private void WireFileActivationFromAvalonia()
    {
        if (Services is null)
        {
            return;
        }

        if (TryGetFeature(typeof(IActivatableLifetime)) is not IActivatableLifetime activatable)
        {
            return;
        }

        var publisher = Services.GetRequiredService<IFileActivationPublisher>();
        activatable.Activated += (_, e) =>
        {
            if (e.Kind != ActivationKind.File || e is not FileActivatedEventArgs fileArgs)
            {
                return;
            }

            foreach (var file in fileArgs.Files)
            {
                if (file.Path is { IsFile: true } uri)
                {
                    publisher.NotifyFileActivated(uri.LocalPath);
                }
            }
        };
    }
}
