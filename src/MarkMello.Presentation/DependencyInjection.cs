using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MarkMello.Application.Abstractions;
using MarkMello.Presentation.Editing;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.Services;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TopLevel accessor: к моменту, когда FilePicker реально вызывается, MainWindow уже создан.
        // На этапе DI build окно ещё не существует — поэтому только Func, не значение.
        services.AddSingleton<Func<TopLevel?>>(_ => static () =>
        {
            var lifetime = global::Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime;
            return lifetime?.MainWindow;
        });

        services.AddSingleton<IFilePicker, FilePicker>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Каждая editor-сессия получает свой планировщик preview: он держит
        // DispatcherTimer и номер поколения, которые нельзя делить между сессиями.
        services.AddSingleton<Func<IEditorPreviewScheduler>>(
            _ => static () => new DebouncedEditorPreviewScheduler());

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
